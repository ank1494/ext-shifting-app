using ExtShiftingApp.Analysis;

namespace ExtShiftingApp.Tests.Analysis;

public class AnalysisJobTests : IDisposable
{
    private readonly string _m2Dir = Path.Combine(Path.GetTempPath(), $"m2_{Guid.NewGuid():N}");
    private readonly string _outDir = Path.Combine(Path.GetTempPath(), $"out_{Guid.NewGuid():N}");

    public AnalysisJobTests()
    {
        Directory.CreateDirectory(_m2Dir);
        Directory.CreateDirectory(_outDir);
        Directory.CreateDirectory(Path.Combine(_m2Dir, "scripts"));
        File.WriteAllText(Path.Combine(_m2Dir, "scripts", "runQueue.m2"), "");
    }

    public void Dispose()
    {
        Directory.Delete(_m2Dir, recursive: true);
        Directory.Delete(_outDir, recursive: true);
    }

    private AnalysisJob Build(
        ControllableFakeM2Runner runner,
        MemoryJobStateStore? store = null,
        StubQueueStateReader? reader = null) =>
        new(runner, store ?? new MemoryJobStateStore(), reader ?? new StubQueueStateReader(), _outDir, _m2Dir);

    private AnalysisJob Build(
        FakeM2Runner runner,
        MemoryJobStateStore? store = null,
        StubQueueStateReader? reader = null) =>
        new(runner, store ?? new MemoryJobStateStore(), reader ?? new StubQueueStateReader(), _outDir, _m2Dir);

    // --- Issue 110: state machine + GetSnapshot ---

    [Fact]
    public async Task StartAsync_GetSnapshot_ReturnsRunning()
    {
        var runner = new ControllableFakeM2Runner();
        var job = Build(runner);

        await job.StartAsync("my-run", "/input.m2");

        Assert.Equal(JobStatus.Running, job.GetSnapshot().Status);

        runner.Release(0);
        await job.WaitAsync();
    }

    [Fact]
    public async Task StartAsync_WhileRunning_Throws()
    {
        var runner = new ControllableFakeM2Runner();
        var job = Build(runner);

        await job.StartAsync("run1", "/input.m2");

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.StartAsync("run2", "/input.m2"));

        runner.Release(0);
        await job.WaitAsync();
    }

    [Fact]
    public async Task ResumeAsync_WhileRunning_Throws()
    {
        var runner = new ControllableFakeM2Runner();
        var job = Build(runner);

        await job.StartAsync("my-run", "/input.m2");

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.ResumeAsync("my-run"));

        runner.Release(0);
        await job.WaitAsync();
    }

    [Fact]
    public async Task StopAsync_SetsStopFlag_M2ExitsCleanly_TransitionsToPaused()
    {
        var runner = new ControllableFakeM2Runner();
        var store = new MemoryJobStateStore();
        var job = Build(runner, store);

        await job.StartAsync("my-run", "/input.m2");
        await job.StopAsync();
        runner.Release(0);
        await job.WaitAsync();

        Assert.Equal(JobStatus.Paused, job.GetSnapshot().Status);
    }

    [Fact]
    public async Task StartAsync_M2ExitsCleanly_NoStop_TransitionsToComplete()
    {
        var job = Build(new FakeM2Runner(exitCode: 0));

        await job.StartAsync("my-run", "/input.m2");
        await job.WaitAsync();

        Assert.Equal(JobStatus.Complete, job.GetSnapshot().Status);
    }

    [Fact]
    public async Task StartAsync_M2ExitsWithError_TransitionsToFailed()
    {
        var job = Build(new FakeM2Runner(exitCode: 2));

        await job.StartAsync("my-run", "/input.m2");
        await job.WaitAsync();

        Assert.Equal(JobStatus.Failed, job.GetSnapshot().Status);
    }

    [Fact]
    public async Task ResumeAsync_FromPaused_TransitionsToRunning_ThenComplete()
    {
        var runner = new ControllableFakeM2Runner();
        var store = new MemoryJobStateStore(new JobState("my-run", JobStatus.Paused, 0, null));
        var job = Build(runner, store);

        var resumeTask = job.ResumeAsync("my-run");
        runner.Release(0);
        await resumeTask;
        await job.WaitAsync();

        Assert.Equal(JobStatus.Complete, job.GetSnapshot().Status);
    }

    [Fact]
    public async Task GetSnapshot_ReturnsRunName()
    {
        var runner = new ControllableFakeM2Runner();
        var job = Build(runner);

        await job.StartAsync("named-run", "/input.m2");

        Assert.Equal("named-run", job.GetSnapshot().RunName);

        runner.Release(0);
        await job.WaitAsync();
    }

    [Fact]
    public void Constructor_RestoresPausedStateFromStore()
    {
        var store = new MemoryJobStateStore(new JobState("saved-run", JobStatus.Paused, 0, null));
        var job = Build(new FakeM2Runner(), store);

        Assert.Equal(JobStatus.Paused, job.GetSnapshot().Status);
        Assert.Equal("saved-run", job.GetSnapshot().RunName);
    }

    [Fact]
    public async Task GetSnapshot_WhenRunning_UsesLiveQueueCounts()
    {
        var runner = new ControllableFakeM2Runner();
        var queueReader = new StubQueueStateReader();
        queueReader.SetState(new QueueState(3, 7, 2));
        var job = Build(runner, reader: queueReader);

        await job.StartAsync("my-run", "/input.m2");

        var snap = job.GetSnapshot();
        Assert.Equal(3, snap.PendingCount);
        Assert.Equal(7, snap.DoneCount);
        Assert.Equal(2, snap.CurrentItemDepth);

        runner.Release(0);
        await job.WaitAsync();
    }

    // --- Issue 111: OpenOutputStream Channel broadcast + replay buffer ---

    [Fact]
    public async Task OpenOutputStream_EmittedLines_ReachReader()
    {
        var runner = new ControllableFakeM2Runner();
        var job = Build(runner);
        await job.StartAsync("my-run", "/input.m2");

        var reader = job.OpenOutputStream();
        runner.EmitLine("hello");
        runner.Release(0);
        await job.WaitAsync();

        var lines = new List<string>();
        await foreach (var line in reader.ReadAllAsync())
            lines.Add(line);

        Assert.Contains("hello", lines);
    }

    [Fact]
    public async Task OpenOutputStream_LateJoin_ReceivesReplayThenLiveLines()
    {
        var runner = new ControllableFakeM2Runner();
        var job = Build(runner);
        await job.StartAsync("my-run", "/input.m2");

        runner.EmitLine("replay-line");

        // late join after replay-line was emitted
        var reader = job.OpenOutputStream();

        runner.EmitLine("live-line");
        runner.Release(0);
        await job.WaitAsync();

        var lines = new List<string>();
        await foreach (var line in reader.ReadAllAsync())
            lines.Add(line);

        var replayIdx = lines.IndexOf("replay-line");
        var liveIdx   = lines.IndexOf("live-line");
        Assert.True(replayIdx >= 0, "replay-line should be present");
        Assert.True(liveIdx   >= 0, "live-line should be present");
        Assert.True(replayIdx < liveIdx, "replay-line should come before live-line");
    }

    [Fact]
    public async Task OpenOutputStream_AfterJobComplete_ReturnsReplayAndCompletes()
    {
        var job = Build(new FakeM2Runner(exitCode: 0, output: "output-line"));
        await job.StartAsync("my-run", "/input.m2");
        await job.WaitAsync();

        var reader = job.OpenOutputStream();

        var lines = new List<string>();
        await foreach (var line in reader.ReadAllAsync())
            lines.Add(line);

        Assert.Contains("output-line", lines);
    }

    [Fact]
    public async Task OpenOutputStream_MultipleCallers_EachReceiveSameLines()
    {
        var runner = new ControllableFakeM2Runner();
        var job = Build(runner);
        await job.StartAsync("my-run", "/input.m2");

        var r1 = job.OpenOutputStream();
        var r2 = job.OpenOutputStream();

        runner.EmitLine("shared");
        runner.Release(0);
        await job.WaitAsync();

        var lines1 = new List<string>();
        await foreach (var l in r1.ReadAllAsync()) lines1.Add(l);

        var lines2 = new List<string>();
        await foreach (var l in r2.ReadAllAsync()) lines2.Add(l);

        Assert.Contains("shared", lines1);
        Assert.Contains("shared", lines2);
    }

    // --- Issue 112: M2 process wiring + polling timer ---

    [Fact]
    public async Task M2EmitsRunPaused_GetSnapshot_ReturnsPaused()
    {
        var runner = new ControllableFakeM2Runner();
        var job = Build(runner);
        await job.StartAsync("my-run", "/input.m2");

        runner.Release(0, "EVENT:{\"type\":\"run_paused\"}");
        await job.WaitAsync();

        Assert.Equal(JobStatus.Paused, job.GetSnapshot().Status);
    }

    [Fact]
    public async Task StopAsync_WritesStopFile_DoesNotBlockOnM2Exit()
    {
        var runner = new ControllableFakeM2Runner();
        var job = Build(runner);
        await job.StartAsync("my-run", "/input.m2");

        await job.StopAsync();

        Assert.True(File.Exists(Path.Combine(_outDir, "my-run", "stop_requested")),
            "stop_requested file should be written");
        Assert.False(job.WaitAsync().IsCompleted,
            "WaitAsync should not complete until M2 exits");

        runner.Release(0, "EVENT:{\"type\":\"run_paused\"}");
        await job.WaitAsync();
    }

    [Fact]
    public async Task ResumeAsync_AfterStopAndM2Exit_DoesNotRaceWithTeardown()
    {
        var runner = new ControllableFakeM2Runner();
        var store = new MemoryJobStateStore();
        var job = Build(runner, store);

        await job.StartAsync("my-run", "/input.m2");
        await job.StopAsync();

        // Let M2 exit naturally → state transitions to Paused, RunCoreAsync finally completes
        runner.Release(0, "EVENT:{\"type\":\"run_paused\"}");
        await job.WaitAsync();

        Assert.Equal(JobStatus.Paused, job.GetSnapshot().Status);

        // ResumeAsync awaits priorTask (already complete) before launching new run
        var resumeTask = job.ResumeAsync("my-run");
        runner.Release(0);
        await resumeTask;
        await job.WaitAsync();

        Assert.Equal(JobStatus.Complete, job.GetSnapshot().Status);
    }

    [Fact]
    public async Task PollingTimer_EmitsQueueStateSSE_DuringRun()
    {
        var runner = new ControllableFakeM2Runner();
        var queueReader = new StubQueueStateReader();
        queueReader.SetState(new QueueState(5, 2, 1));
        var job = new AnalysisJob(runner, new MemoryJobStateStore(), queueReader, _outDir, _m2Dir,
            pollingInterval: TimeSpan.FromMilliseconds(20));

        await job.StartAsync("my-run", "/input.m2");
        var outputReader = job.OpenOutputStream(); // subscribe after start so we're in _subscribers

        await Task.Delay(80); // allow timer to fire

        runner.Release(0);
        await job.WaitAsync();

        var lines = new List<string>();
        await foreach (var l in outputReader.ReadAllAsync()) lines.Add(l);

        Assert.Contains(lines, l => l.Contains("queue_state"));
    }

    [Fact]
    public async Task PollingTimer_StopsAfterRunEnds()
    {
        var runner = new ControllableFakeM2Runner();
        var queueReader = new StubQueueStateReader();
        queueReader.SetState(new QueueState(1, 0, null));
        var job = new AnalysisJob(runner, new MemoryJobStateStore(), queueReader, _outDir, _m2Dir,
            pollingInterval: TimeSpan.FromMilliseconds(20));

        await job.StartAsync("my-run", "/input.m2");
        var outputReader = job.OpenOutputStream();
        await Task.Delay(80);
        runner.Release(0);
        await job.WaitAsync();

        var countAtEnd = 0;
        await foreach (var l in outputReader.ReadAllAsync())
            if (l.Contains("queue_state")) countAtEnd++;

        Assert.True(countAtEnd > 0, "at least one poll should have fired during the run");
    }

    [Fact]
    public async Task GetSnapshot_DuringRun_ReflectsLiveQueueCountsFromReader()
    {
        var runner = new ControllableFakeM2Runner();
        var queueReader = new StubQueueStateReader();
        var job = new AnalysisJob(runner, new MemoryJobStateStore(), queueReader, _outDir, _m2Dir,
            pollingInterval: TimeSpan.FromHours(1));

        await job.StartAsync("my-run", "/input.m2");

        queueReader.SetState(new QueueState(10, 5, 3));
        var snap = job.GetSnapshot();

        Assert.Equal(10, snap.PendingCount);
        Assert.Equal(5, snap.DoneCount);
        Assert.Equal(3, snap.CurrentItemDepth);

        runner.Release(0);
        await job.WaitAsync();
    }
}
