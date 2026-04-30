using ExtShiftingApp.Analysis;
using ExtShiftingApp.M2;

namespace ExtShiftingApp.Tests.Analysis;

public class FakeM2Runner(int exitCode = 0, string output = "") : IM2Runner
{
    public string? LastScriptPath { get; private set; }
    public string? LastScriptArgs { get; private set; }

    public Task<M2Result> RunScriptAsync(string scriptPath, Action<string>? onOutput = null,
        CancellationToken ct = default, string? scriptArgs = null)
    {
        LastScriptPath = scriptPath;
        LastScriptArgs = scriptArgs;
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            onOutput?.Invoke(line);
        return Task.FromResult(new M2Result(exitCode == 0, output, exitCode));
    }
}

public class ControllableFakeM2Runner : IM2Runner
{
    private TaskCompletionSource<M2Result>? _tcs;
    private Action<string>? _onOutput;

    public string? LastScriptPath { get; private set; }
    public string? LastScriptArgs { get; private set; }

    public Task<M2Result> RunScriptAsync(string scriptPath, Action<string>? onOutput = null,
        CancellationToken ct = default, string? scriptArgs = null)
    {
        LastScriptPath = scriptPath;
        LastScriptArgs = scriptArgs;
        _onOutput = onOutput;
        _tcs = new TaskCompletionSource<M2Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => _tcs.TrySetCanceled());
        return _tcs.Task;
    }

    public void EmitLine(string line) => _onOutput?.Invoke(line);

    public void Release(int exitCode = 0, string output = "")
    {
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            _onOutput?.Invoke(line);
        _tcs?.TrySetResult(new M2Result(exitCode == 0, output, exitCode));
    }
}

public class MemoryJobStateStore(JobState? initial = null) : IJobStateStore
{
    private JobState? _state = initial;

    public void Save(JobState state) => _state = state;
    public JobState? TryLoad() => _state;
}

public class StubQueueStateReader : IQueueStateReader
{
    private QueueState _state = new(0, 0, null);

    public void SetState(QueueState state) => _state = state;
    public QueueState Read(string runDir) => _state;
}
