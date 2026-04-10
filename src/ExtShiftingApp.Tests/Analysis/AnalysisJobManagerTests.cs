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

        // Stop() no longer hard-kills; M2 must exit naturally after seeing the signal.
        factory.LastProcess!.Release(0, "EVENT:{\"type\":\"run_paused\"}");
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
        factory.LastProcess!.Release(0, "EVENT:{\"type\":\"run_paused\"}");
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
        factory.LastProcess!.Release(0, "EVENT:{\"type\":\"run_paused\"}");
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

        // Resume — factory creates a new blocking process; prior output must not appear again
        manager.Resume("run-A");
        // The immediate poll timer (dueTime=0) may add a queue_state line before this assertion,
        // so we check for absence of old content rather than exact count == 0.
        Assert.DoesNotContain(manager.GetOutputLog(), l => l.Contains("old output"));

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

    // --- Issue #63: stale status counts ---

    [Fact]
    public async Task PollingTimer_FiresImmediatelyOnRunStart_EmitsQueueStateSSE()
    {
        // Fails before fix: dueTime was _pollingInterval (60s), so no SSE fires in 100ms.
        // Passes after fix: dueTime = TimeSpan.Zero, timer fires immediately.
        var blockingFake = new ControllableFakeProcessFactory();
        var received = new List<string>();
        var manager = new AnalysisJobManager(
            new M2ProcessRunner(blockingFake, _m2Dir), _m2Dir, _outDir,
            pollingInterval: TimeSpan.FromSeconds(60)); // long — won't auto-repeat within test
        manager.Subscribe((_, line) => received.Add(line));

        var runDir = Path.Combine(_outDir, "my-run");
        Directory.CreateDirectory(Path.Combine(runDir, "pending"));
        Directory.CreateDirectory(Path.Combine(runDir, "done"));
        File.WriteAllText(Path.Combine(runDir, "pending", "0001"),
            "new HashTable from {\n  \"depth\" => 0,\n}");

        manager.Start("my-run", "/input/tori.m2");
        await Task.Delay(100); // well under 60s pollingInterval — only fires if dueTime=0

        Assert.Contains(received, line => line.Contains("queue_state"));

        blockingFake.LastProcess!.Release(0, "");
        await manager.WaitAsync();
    }

    [Fact]
    public async Task Start_ResetsLastPolledState_AllowsFirstPollToBroadcastOnRestart()
    {
        // Fails before fix: _lastPolledState not cleared on Start(), so same counts are suppressed.
        // Passes after fix: _lastPolledState = null reset in Start().
        var received = new List<string>();
        var blockingFake = new ControllableFakeProcessFactory();
        var manager = new AnalysisJobManager(
            new M2ProcessRunner(blockingFake, _m2Dir), _m2Dir, _outDir,
            pollingInterval: TimeSpan.FromHours(1));
        manager.Subscribe((_, line) => received.Add(line));

        // Run A — seed 1 pending item
        var runDirA = Path.Combine(_outDir, "run-a");
        Directory.CreateDirectory(Path.Combine(runDirA, "pending"));
        Directory.CreateDirectory(Path.Combine(runDirA, "done"));
        File.WriteAllText(Path.Combine(runDirA, "pending", "0001"),
            "new HashTable from {\n  \"depth\" => 0,\n}");

        manager.Start("run-a", "/input/tori.m2");
        await Task.Delay(50); // let initial timer fire (dueTime=0) — establishes _lastPolledState={1,0}
        blockingFake.LastProcess!.Release(0, "EVENT:{\"type\":\"run_paused\"}");
        await manager.WaitAsync();

        var countAfterRunA = received.Count(l => l.Contains("queue_state"));

        // Run B — same queue state (1 pending item) to expose the stale _lastPolledState bug
        var runDirB = Path.Combine(_outDir, "run-b");
        Directory.CreateDirectory(Path.Combine(runDirB, "pending"));
        Directory.CreateDirectory(Path.Combine(runDirB, "done"));
        File.WriteAllText(Path.Combine(runDirB, "pending", "0001"),
            "new HashTable from {\n  \"depth\" => 0,\n}");

        manager.Start("run-b", "/input/tori.m2");
        // Immediately call FirePollForTest — should broadcast because _lastPolledState was reset
        manager.FirePollForTest();

        Assert.True(received.Count(l => l.Contains("queue_state")) > countAfterRunA,
            "_lastPolledState should be reset on Start() so re-run with same counts still broadcasts");

        blockingFake.LastProcess!.Release(0, "");
        await manager.WaitAsync();
    }

    [Fact]
    public async Task GetState_ReturnsLiveDiskCounts_BetweenPollFireings()
    {
        // Fails before fix: GetState() returns stale _state snapshot.
        // Passes after fix: GetState() overlays live _queueReader.Read() when Running.
        var blockingFake = new ControllableFakeProcessFactory();
        var manager = new AnalysisJobManager(
            new M2ProcessRunner(blockingFake, _m2Dir), _m2Dir, _outDir,
            pollingInterval: TimeSpan.FromSeconds(10)); // long enough that only the initial dueTime=0 fires

        var runDir = Path.Combine(_outDir, "my-run");
        var pendingDir = Directory.CreateDirectory(Path.Combine(runDir, "pending")).FullName;
        var doneDir = Directory.CreateDirectory(Path.Combine(runDir, "done")).FullName;
        File.WriteAllText(Path.Combine(pendingDir, "0001"),
            "new HashTable from {\n  \"depth\" => 0,\n}");

        manager.Start("my-run", "/input/tori.m2");
        await Task.Delay(50); // let timer fire once — _lastPolledState = {pending:1}

        // Simulate M2 completing the item between poll firings
        File.Move(Path.Combine(pendingDir, "0001"), Path.Combine(doneDir, "0001"));

        // GetState() should reflect current disk state immediately (no FirePollForTest needed)
        var state = manager.GetState();
        Assert.Equal(0, state.PendingCount);
        Assert.Equal(1, state.DoneCount);

        blockingFake.LastProcess!.Release(0, "");
        await manager.WaitAsync();
    }

    // --- Issue #62: Stop() hard-kills M2 mid-item ---

    [Fact]
    public async Task Stop_WritesStopRequestedFile_InsteadOfImmediateCancellation()
    {
        // Verifies Stop() writes stop_requested and does not immediately end the run.
        var blockingFake = new ControllableFakeProcessFactory();
        var manager = new AnalysisJobManager(
            new M2ProcessRunner(blockingFake, _m2Dir), _m2Dir, _outDir);

        manager.Start("my-run", "/input/tori.m2");

        manager.Stop();

        // Signal file should exist
        Assert.True(File.Exists(Path.Combine(_outDir, "my-run", "stop_requested")));

        // Run should still be in progress — Stop() does not cancel CTS
        await Task.Delay(20);
        Assert.False(manager.WaitAsync().IsCompleted,
            "Run should not complete immediately after Stop()");

        // Cleanup: release naturally (simulating M2 finishing current item and seeing signal)
        blockingFake.LastProcess!.Release(0, "EVENT:{\"type\":\"run_paused\"}");
        await manager.WaitAsync();
        Assert.Equal(JobStatus.Paused, manager.GetState().Status);
    }

    [Fact]
    public async Task Stop_ItemInFlight_ItemDoneReceivedBeforePaused_KillNeverCalled()
    {
        // Fails before fix: Stop() cancels CTS → process killed → item_done never emitted.
        // Passes after fix: Stop() writes file; process runs to natural exit; item_done is received.
        var blockingFake = new ControllableFakeProcessFactory();
        var manager = new AnalysisJobManager(
            new M2ProcessRunner(blockingFake, _m2Dir), _m2Dir, _outDir);
        var received = new List<string>();
        manager.Subscribe((_, line) => received.Add(line));

        manager.Start("my-run", "/input/tori.m2");
        var process = blockingFake.LastProcess!;

        // M2 emits item_started — item is now in-flight
        process.EmitOutput("EVENT:{\"type\":\"item_started\",\"item\":\"0001\",\"depth\":0,\"parent\":\"seed\"}");

        // Stop() called while item is in-flight — should NOT kill process immediately
        manager.Stop();

        // M2 finishes the current item naturally, then sees stop_requested and exits
        process.Release(0,
            "EVENT:{\"type\":\"item_done\",\"item\":\"0001\",\"splits\":2}\n" +
            "EVENT:{\"type\":\"run_paused\"}");
        await manager.WaitAsync();

        Assert.Contains(received, l => l.Contains("item_done"));
        Assert.Equal(JobStatus.Paused, manager.GetState().Status);
        Assert.False(process.WasKilled, "M2 process should not be hard-killed when stop_requested signal is used");
    }

    // --- Bug: Stop() hard-kills M2 mid-item when grace timeout fires ---

    [Fact]
    public async Task Stop_ItemTakesLongerThanGraceTimeout_ProcessNotKilledPrematurely()
    {
        // Before fix: Stop() scheduled CTS cancellation after a grace timeout, killing M2 mid-item.
        // After fix: Stop() only writes stop_requested; CTS is never cancelled by Stop().
        var blockingFake = new ControllableFakeProcessFactory();
        var manager = new AnalysisJobManager(
            new M2ProcessRunner(blockingFake, _m2Dir), _m2Dir, _outDir);

        manager.Start("my-run", "/input/tori.m2");
        var process = blockingFake.LastProcess!;

        process.EmitOutput("EVENT:{\"type\":\"item_started\",\"item\":\"0001\",\"depth\":0,\"parent\":\"seed\"}");
        manager.Stop();

        // Wait well past the (old) grace timeout — item is still computing
        await Task.Delay(150);

        // Bug: WaitAsync() is already completed (CTS fired at 30ms, M2 killed)
        // Fix: run is still in progress — Stop() did not cancel CTS
        Assert.False(manager.WaitAsync().IsCompleted,
            "Stop() must not kill M2 mid-item; run should still be in progress after grace timeout");

        process.Release(0,
            "EVENT:{\"type\":\"item_done\",\"item\":\"0001\",\"splits\":0}\n" +
            "EVENT:{\"type\":\"run_paused\"}");
        await manager.WaitAsync();

        Assert.Equal(JobStatus.Paused, manager.GetState().Status);
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

        manager.Stop(); // writes stop_requested; does NOT cancel CTS

        // Start("run2") on a background thread — should block draining run1, not throw
        var startTask = Task.Run(() => manager.Start("run2", "/input/tori.m2"));

        await Task.Delay(50); // give startTask time to reach the drain point

        Assert.False(startTask.IsCompleted,
            "Start should block while draining the old run, not throw or complete immediately");

        // Release run1 naturally (M2 sees signal and exits) — unblocks the drain
        oldProcess.Release(0, "EVENT:{\"type\":\"run_paused\"}");

        await startTask;
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
