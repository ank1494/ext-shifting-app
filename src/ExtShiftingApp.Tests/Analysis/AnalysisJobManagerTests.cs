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
        // analyze triangs.m2 must exist for RunScriptAsync to find it
        File.WriteAllText(Path.Combine(_m2Dir, "analyze triangs.m2"), "");
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
        var fake = new FakeProcessFactory(exitCode: 0, output: "no more splits to calculate", error: "");
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

        factory.LastProcess!.Release(0, "no more splits to calculate");
        await manager.WaitAsync();
    }

    [Fact]
    public async Task Start_M2Failure_TransitionsToFailed()
    {
        var fake = new FakeProcessFactory(exitCode: 1, output: "", error: "M2 crashed");
        var manager = Build(fake);

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        Assert.Equal(JobStatus.Failed, manager.GetState().Status);
        Assert.NotNull(manager.GetState().Error);
    }

    [Fact]
    public async Task Stop_CancelsRunningJob_TransitionsToIdle()
    {
        var factory = new ControllableFakeProcessFactory();
        var manager = Build(factory);

        manager.Start("my-run", "/input/tori.m2");
        manager.Stop();
        await manager.WaitAsync();

        Assert.Equal(JobStatus.Idle, manager.GetState().Status);
    }

    [Fact]
    public async Task Start_PersistsStateToDisk()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "no more splits to calculate", error: "");
        var manager = Build(fake);

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        var stateFile = Path.Combine(_outDir, "my-run", "job_state.json");
        Assert.True(File.Exists(stateFile));
    }

    [Fact]
    public async Task Start_IterationCounterIncrementsPerRun()
    {
        // First iteration: no convergence. Second iteration: converged.
        var callCount = 0;
        var outputs = new[] { "still going", "no more splits to calculate" };
        var factory = new MultiResponseFakeProcessFactory(outputs);
        var manager = new AnalysisJobManager(new M2ProcessRunner(factory, _m2Dir), _m2Dir, _outDir);

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        Assert.Equal(2, manager.GetState().CurrentIteration);
        Assert.Equal(JobStatus.Complete, manager.GetState().Status);
    }

    [Fact]
    public async Task Start_BroadcastsOutputLines()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "line1\nno more splits to calculate", error: "");
        var manager = Build(fake);
        var received = new List<string>();
        manager.Subscribe((_, line) => received.Add(line));

        manager.Start("my-run", "/input/tori.m2");
        await manager.WaitAsync();

        Assert.Contains("line1", received);
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
