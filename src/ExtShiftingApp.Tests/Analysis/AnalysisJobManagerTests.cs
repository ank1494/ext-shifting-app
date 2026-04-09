using ExtShiftingApp.Analysis;
using ExtShiftingApp.M2;
using ExtShiftingApp.Tests.M2;

namespace ExtShiftingApp.Tests.Analysis;

public class AnalysisJobManagerTests : IDisposable
{
    private readonly string _m2Dir = Path.Combine(Path.GetTempPath(), $"m2_{Guid.NewGuid():N}");
    private readonly string _outDir = Path.Combine(Path.GetTempPath(), $"out_{Guid.NewGuid():N}");

    public AnalysisJobManagerTests()
    {
        Directory.CreateDirectory(_m2Dir);
        Directory.CreateDirectory(_outDir);
        Directory.CreateDirectory(Path.Combine(_m2Dir, "scripts"));
        File.WriteAllText(Path.Combine(_m2Dir, "scripts", "runAnalysis.m2"), "");
        File.WriteAllText(Path.Combine(_m2Dir, "scripts", "runQueue.m2"), "");
    }

    public void Dispose()
    {
        Directory.Delete(_m2Dir, recursive: true);
        Directory.Delete(_outDir, recursive: true);
    }

    private AnalysisJobManager Build(FakeProcessFactory fake) =>
        new(new M2ProcessRunner(fake, _m2Dir), _m2Dir, _outDir);

    private AnalysisJobManager Build(ControllableFakeProcessFactory factory) =>
        new(new M2ProcessRunner(factory, _m2Dir), _m2Dir, _outDir);

    [Fact]
    public async Task Start_CompletedJob_TransitionsToComplete()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var manager = Build(fake);

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        Assert.Equal(JobStatus.Complete, manager.GetState().Status);
    }

    [Fact]
    public async Task Start_CannotStartWhileRunning()
    {
        var factory = new ControllableFakeProcessFactory();
        var manager = Build(factory);

        manager.Start("run1", "/input/tori.m2");

        Assert.Throws<InvalidOperationException>(() => manager.Start("run2", "/input/tori.m2"));

        factory.LastProcess!.Release(0, "");
        await manager.WaitAsync();
    }

    [Fact]
    public async Task Start_M2Failure_TransitionsToFailed()
    {
        var fake = new FakeProcessFactory(exitCode: 2, output: "", error: "M2 crashed");
        var manager = Build(fake);

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        Assert.Equal(JobStatus.Failed, manager.GetState().Status);
        Assert.NotNull(manager.GetState().Error);
    }

    [Fact]
    public async Task Stop_CancelsRunningJob_TransitionsToPaused()
    {
        var factory = new ControllableFakeProcessFactory();
        var manager = Build(factory);

        manager.Start("my-run", "/input/tori.m2");
        manager.Stop();
        await manager.WaitAsync();

        Assert.Equal(JobStatus.Paused, manager.GetState().Status);
    }

    [Fact]
    public async Task Start_PersistsStateToDisk()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var manager = Build(fake);

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        var stateFile = Path.Combine(_outDir, "my-run", "job_state.json");
        Assert.True(File.Exists(stateFile));
    }

    [Fact]
    public async Task Start_InvokesM2WithScriptsRunQueuePath()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var manager = Build(fake);

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        Assert.Contains(Path.Combine("scripts", "runQueue.m2"), fake.LastArguments);
    }

    [Fact]
    public async Task Start_WritesConfigToOutputDirectory()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var manager = Build(fake);

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        Assert.False(File.Exists(Path.Combine(_m2Dir, "analysis config.m2")));
        Assert.True(File.Exists(Path.Combine(_outDir, "my-run", "analysis config.m2")));
    }

    [Fact]
    public async Task Start_ConfigContainsAbsoluteAnalysisOutputDir()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var manager = Build(fake);

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        var config = File.ReadAllText(Path.Combine(_outDir, "my-run", "analysis config.m2"));
        var expectedRunDir = Path.Combine(_outDir, "my-run");
        Assert.Contains($"analysisOutputDir = \"{expectedRunDir}\"", config);
    }

    [Fact]
    public async Task Start_PassesConfigPathAsM2Argument()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var manager = Build(fake);

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        var expectedConfigPath = Path.Combine(_outDir, "my-run", "analysis config.m2");
        Assert.Contains(expectedConfigPath, fake.LastArguments);
    }

    [Fact]
    public async Task Start_ExitCode0_Converged_TransitionsToComplete()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var manager = Build(fake);

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        Assert.Equal(JobStatus.Complete, manager.GetState().Status);
    }

    [Fact]
    public async Task Start_ExitCode2_Error_TransitionsToFailed()
    {
        var fake = new FakeProcessFactory(exitCode: 2, output: "", error: "");
        var manager = Build(fake);

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        Assert.Equal(JobStatus.Failed, manager.GetState().Status);
    }

    [Fact]
    public async Task Start_UnexpectedExitCode_TransitionsToFailed()
    {
        var fake = new FakeProcessFactory(exitCode: 99, output: "", error: "");
        var manager = Build(fake);

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        Assert.Equal(JobStatus.Failed, manager.GetState().Status);
    }

    [Fact]
    public async Task Start_BroadcastsOutputLines()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "line1", error: "");
        var manager = Build(fake);
        var received = new List<string>();
        manager.Subscribe((_, line) => received.Add(line));

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        Assert.Contains("line1", received);
    }

    // --- Queue-based manager tests (issue #49) ---

    [Fact]
    public async Task Start_InvokesRunQueueNotRunAnalysis()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var manager = Build(fake);

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        Assert.Contains(Path.Combine("scripts", "runQueue.m2"), fake.LastArguments ?? "");
    }

    [Fact]
    public async Task Start_PassesBatchParamsToRunQueue()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var manager = Build(fake);
        var batch = new BatchParameters(ItemCap: 5, MaxVertexCount: 12, Timeout: TimeSpan.FromSeconds(30));

        manager.Start("my-run", "/input/tori.m2", batch);
        await manager.WaitAsync();

        Assert.Contains("5", fake.LastArguments ?? "");
        Assert.Contains("12", fake.LastArguments ?? "");
        Assert.Contains("30", fake.LastArguments ?? "");
    }

    [Fact]
    public async Task Stop_TransitionsToPaused_NotIdle()
    {
        var factory = new ControllableFakeProcessFactory();
        var manager = Build(factory);

        manager.Start("my-run", "/input/tori.m2");
        manager.Stop();
        await manager.WaitAsync();

        Assert.Equal(JobStatus.Paused, manager.GetState().Status);
    }

    [Fact]
    public async Task Resume_TransitionsFromPausedToRunning_ThenComplete()
    {
        var factory = new ControllableFakeProcessFactory();
        var manager = Build(factory);

        // Start and stop to reach Paused
        manager.Start("my-run", "/input/tori.m2");
        manager.Stop();
        await manager.WaitAsync();
        Assert.Equal(JobStatus.Paused, manager.GetState().Status);

        // Resume — fake process completes immediately
        var resumeFake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var manager2 = new AnalysisJobManager(new M2ProcessRunner(resumeFake, _m2Dir), _m2Dir, _outDir);
        // Seed state as paused
        manager2.SetPausedState("my-run");
        manager2.Resume("my-run");
        await manager2.WaitAsync();

        Assert.Equal(JobStatus.Complete, manager2.GetState().Status);
    }

    // --- Bug #58: Resume() stale state ---

    [Fact]
    public async Task Resume_UpdatesRunName()
    {
        var factory = new ControllableFakeProcessFactory();
        var manager = Build(factory);

        manager.Start("run-A", "/input/tori.m2");
        manager.Stop();
        factory.LastProcess!.Release(0, "");
        await manager.WaitAsync();

        var resumeFake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var manager2 = new AnalysisJobManager(new M2ProcessRunner(resumeFake, _m2Dir), _m2Dir, _outDir);
        manager2.SetPausedState("run-A");
        manager2.Resume("run-B");
        await manager2.WaitAsync();

        Assert.Equal("run-B", manager2.GetState().RunName);
    }

    [Fact]
    public async Task Resume_ClearsOutputLog()
    {
        var factory = new ControllableFakeProcessFactory();
        var manager = Build(factory);

        // Run to completion so the output log has lines
        manager.Start("run-A", "/input/tori.m2");
        factory.LastProcess!.Release(0, "old output");
        await manager.WaitAsync();
        manager.SetPausedState("run-A");
        Assert.True(manager.GetOutputLog().Count > 0, "Precondition: log should have lines");

        // Resume — factory creates a new blocking process; log should be cleared before it runs
        manager.Resume("run-A");
        Assert.Equal(0, manager.GetOutputLog().Count);

        factory.LastProcess!.Release(0, "");
        await manager.WaitAsync();
    }

    // --- Bug #57: EVENT lines routed via stderr ---

    [Fact]
    public async Task Start_EventLinesOnStderr_AreBroadcast()
    {
        // Simulates M2 emitting EVENT: lines via stderr (unbuffered channel)
        var fake = new FakeProcessFactory(exitCode: 0, output: "",
            error: "EVENT:{\"type\":\"item_started\",\"item\":\"0001\",\"depth\":0,\"parent\":\"\"}");
        var manager = Build(fake);
        var received = new List<string>();
        manager.Subscribe((_, line) => received.Add(line));

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        Assert.Contains(received, line => line.Contains("item_started"));
    }

    // --- Bug #59: state recovery on restart ---

    [Fact]
    public void Constructor_RestoresPausedState_FromDisk()
    {
        var runDir = Path.Combine(_outDir, "my-run");
        Directory.CreateDirectory(runDir);
        var state = new JobState("my-run", JobStatus.Paused, 0, null);
        File.WriteAllText(Path.Combine(runDir, "job_state.json"),
            System.Text.Json.JsonSerializer.Serialize(state));

        var manager = Build(new FakeProcessFactory(0, "", ""));

        Assert.Equal(JobStatus.Paused, manager.GetState().Status);
        Assert.Equal("my-run", manager.GetState().RunName);
    }

    [Fact]
    public void Constructor_RestoredPausedState_AllowsResume()
    {
        var runDir = Path.Combine(_outDir, "my-run");
        Directory.CreateDirectory(runDir);
        var state = new JobState("my-run", JobStatus.Paused, 0, null);
        File.WriteAllText(Path.Combine(runDir, "job_state.json"),
            System.Text.Json.JsonSerializer.Serialize(state));

        var manager = Build(new FakeProcessFactory(0, "", ""));

        var ex = Record.Exception(() => manager.Resume("my-run"));
        Assert.Null(ex);
    }

    [Fact]
    public void Constructor_RunningStateOnDisk_IsNotRestored()
    {
        var runDir = Path.Combine(_outDir, "crashed-run");
        Directory.CreateDirectory(runDir);
        var state = new JobState("crashed-run", JobStatus.Running, 0, null);
        File.WriteAllText(Path.Combine(runDir, "job_state.json"),
            System.Text.Json.JsonSerializer.Serialize(state));

        var manager = Build(new FakeProcessFactory(0, "", ""));

        Assert.NotEqual(JobStatus.Running, manager.GetState().Status);
    }

    // --- Bug #60: periodic Poll() timer ---

    [Fact]
    public async Task PollingTimer_AutoFires_DuringRun_WithoutManualFirePoll()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var manager = new AnalysisJobManager(
            new M2ProcessRunner(fake, _m2Dir), _m2Dir, _outDir,
            pollingInterval: TimeSpan.FromMilliseconds(30));
        var received = new List<string>();
        manager.Subscribe((_, line) => received.Add(line));

        var runDir = Path.Combine(_outDir, "my-run");
        Directory.CreateDirectory(Path.Combine(runDir, "pending"));
        Directory.CreateDirectory(Path.Combine(runDir, "done"));
        File.WriteAllText(Path.Combine(runDir, "pending", "0001"),
            "new HashTable from {\n  \"depth\" => 0,\n}");

        // Use a blocking process so the timer has time to fire before the run ends
        var blockingFake = new ControllableFakeProcessFactory();
        var manager2 = new AnalysisJobManager(
            new M2ProcessRunner(blockingFake, _m2Dir), _m2Dir, _outDir,
            pollingInterval: TimeSpan.FromMilliseconds(30));
        manager2.Subscribe((_, line) => received.Add(line));

        var runDir2 = Path.Combine(_outDir, "run2");
        Directory.CreateDirectory(Path.Combine(runDir2, "pending"));
        File.WriteAllText(Path.Combine(runDir2, "pending", "0001"),
            "new HashTable from {\n  \"depth\" => 0,\n}");

        manager2.Start("run2", "/input/tori.m2");
        await Task.Delay(100); // allow timer to fire at least once
        blockingFake.LastProcess!.Release(0, "");
        await manager2.WaitAsync();

        Assert.Contains(received, line => line.Contains("queue_state"));
    }

    [Fact]
    public async Task PollingTimer_StopsAfterRunEnds()
    {
        var blockingFake = new ControllableFakeProcessFactory();
        var received = new List<string>();
        var manager = new AnalysisJobManager(
            new M2ProcessRunner(blockingFake, _m2Dir), _m2Dir, _outDir,
            pollingInterval: TimeSpan.FromMilliseconds(30));
        manager.Subscribe((_, line) => received.Add(line));

        var runDir = Path.Combine(_outDir, "run-timer");
        Directory.CreateDirectory(Path.Combine(runDir, "pending"));
        File.WriteAllText(Path.Combine(runDir, "pending", "0001"),
            "new HashTable from {\n  \"depth\" => 0,\n}");

        manager.Start("run-timer", "/input/tori.m2");
        await Task.Delay(100);
        blockingFake.LastProcess!.Release(0, "");
        await manager.WaitAsync();

        var countAtEnd = received.Count(line => line.Contains("queue_state"));
        await Task.Delay(100); // 3× polling interval — timer should be dead
        var countAfterWait = received.Count(line => line.Contains("queue_state"));

        Assert.Equal(countAtEnd, countAfterWait);
    }

    // --- Issue #19: Race condition in Start() ---

    [Fact]
    public async Task Start_ConcurrentCalls_ExactlyOneProcessLaunches()
    {
        var factory = new ControllableFakeProcessFactory();
        var manager = Build(factory);

        var barrier = new Barrier(2);
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        var t1 = Task.Run(() =>
        {
            barrier.SignalAndWait();
            try { manager.Start("run1", "/input/tori.m2"); }
            catch (Exception ex) { exceptions.Add(ex); }
        });
        var t2 = Task.Run(() =>
        {
            barrier.SignalAndWait();
            try { manager.Start("run1", "/input/tori.m2"); }
            catch (Exception ex) { exceptions.Add(ex); }
        });

        await Task.WhenAll(t1, t2);

        Assert.Equal(1, factory.StartCount);
        factory.LastProcess?.Release(0, "");
        await manager.WaitAsync();
    }

    [Fact]
    public async Task Start_AfterStop_DrainsOldTaskBeforeStartingNew()
    {
        var factory = new ControllableFakeProcessFactory();
        var manager = Build(factory);

        manager.Start("run1", "/input/tori.m2");
        var oldProcess = factory.LastProcess!;
        oldProcess.HoldTeardown(); // prevent teardown from completing until we say so

        manager.Stop(); // cancels CTS; old task is now stuck in teardown hold

        // Run Start("run2") on a background thread so we can control teardown timing
        var startTask = Task.Run(() => manager.Start("run2", "/input/tori.m2"));

        // Give startTask time to reach the drain/throw point
        await Task.Delay(50);

        // Release the old teardown — with fix this unblocks the drain, then "run2" starts
        // Without fix, startTask has already thrown InvalidOperationException (status was Running)
        oldProcess.ReleaseTeardown();

        await startTask; // throws if Start("run2") threw InvalidOperationException
        factory.LastProcess!.Release(0, "");
        await manager.WaitAsync();

        Assert.Equal(JobStatus.Complete, manager.GetState().Status);
        Assert.Equal("run2", manager.GetState().RunName);
    }

    // --- Bug #56: run_paused event ---

    [Fact]
    public async Task Start_RunPausedEvent_TransitionsToPaused()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "EVENT:{\"type\":\"run_paused\"}", error: "");
        var manager = Build(fake);

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        Assert.Equal(JobStatus.Paused, manager.GetState().Status);
    }

    [Fact]
    public async Task Start_RunCompleteEvent_TransitionsToComplete()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "EVENT:{\"type\":\"run_complete\"}", error: "");
        var manager = Build(fake);

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        Assert.Equal(JobStatus.Complete, manager.GetState().Status);
    }

    [Fact]
    public async Task PollingTimer_BroadcastsQueueState_WhenCountsChange()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var manager = Build(fake);
        var received = new List<string>();
        manager.Subscribe((_, line) => received.Add(line));

        // Seed a run directory with a pending file so QueueStateReader returns non-zero counts
        var runDir = Path.Combine(_outDir, "my-run");
        Directory.CreateDirectory(Path.Combine(runDir, "pending"));
        Directory.CreateDirectory(Path.Combine(runDir, "done"));
        File.WriteAllText(Path.Combine(runDir, "pending", "0001"),
            "new HashTable from {\n  \"depth\" => 0,\n}");

        manager.Start("my-run", "/input/tori.m2");
        manager.FirePollForTest();
        await manager.WaitAsync();

        Assert.Contains(received, line => line.Contains("queue_state"));
    }

    [Fact]
    public async Task PollingTimer_DoesNotBroadcast_WhenCountsUnchanged()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var manager = Build(fake);
        var received = new List<string>();
        manager.Subscribe((_, line) => received.Add(line));

        manager.Start("my-run", "/input/tori.m2");
        manager.FirePollForTest(); // first poll — establishes baseline
        manager.FirePollForTest(); // second poll — same counts, should NOT broadcast
        await manager.WaitAsync();

        Assert.Equal(1, received.Count(line => line.Contains("queue_state")));
    }
}

/// <summary>Cycles through a list of outputs, one per process invocation.</summary>
public class MultiResponseFakeProcessFactory(string[] outputs) : IProcessFactory
{
    private int _callIndex;
    public IRunningProcess Start(string executable, string arguments, string workingDirectory, bool redirectStdin = false)
    {
        var output = _callIndex < outputs.Length ? outputs[_callIndex] : outputs[^1];
        _callIndex++;
        return new FakeProcess(0, output, "");
    }
}

/// <summary>Cycles through a list of exit codes, one per process invocation.</summary>
public class MultiResponseExitCodeFactory(int[] exitCodes) : IProcessFactory
{
    private int _callIndex;
    public IRunningProcess Start(string executable, string arguments, string workingDirectory, bool redirectStdin = false)
    {
        var code = _callIndex < exitCodes.Length ? exitCodes[_callIndex] : exitCodes[^1];
        _callIndex++;
        return new FakeProcess(code, "", "");
    }
}
