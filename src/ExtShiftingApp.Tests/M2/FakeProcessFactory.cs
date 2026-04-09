using ExtShiftingApp.M2;

namespace ExtShiftingApp.Tests.M2;

/// <summary>
/// A process that blocks until Release() is called — useful for testing cancellation and concurrent-start prevention.
/// </summary>
public class ControllableFakeProcess : IRunningProcess
{
    private readonly TaskCompletionSource _gate = new();
    public event EventHandler<string>? OutputReceived;
    public event EventHandler<string>? ErrorReceived;
    public int ExitCode { get; private set; }
    public bool WasKilled { get; private set; }
    public List<string> InputLines { get; } = [];

    public void Release(int exitCode = 0, string output = "")
    {
        ExitCode = exitCode;
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            OutputReceived?.Invoke(this, line);
        _gate.TrySetResult();
    }

    private TaskCompletionSource? _teardownHold;

    /// <summary>
    /// When set before Kill/Stop, WaitForExitAsync will block at the cancellation point
    /// until ReleaseTeardown() is called. Used to reliably test the stop/restart race.
    /// </summary>
    public void HoldTeardown() => _teardownHold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    public void ReleaseTeardown() => _teardownHold?.TrySetResult();

    public void Kill() { WasKilled = true; _gate.TrySetCanceled(); }
    public Task SendInputAsync(string line, CancellationToken ct = default) { InputLines.Add(line); return Task.CompletedTask; }
    public async Task WaitForExitAsync(CancellationToken ct)
    {
        await using var _ = ct.Register(() => _gate.TrySetCanceled());
        try
        {
            await _gate.Task;
        }
        catch (OperationCanceledException) when (_teardownHold is not null)
        {
            await _teardownHold.Task; // hold here until ReleaseTeardown() is called
            throw;
        }
    }
}

public class ControllableFakeProcessFactory : IProcessFactory
{
    public ControllableFakeProcess? LastProcess { get; private set; }
    public int StartCount { get; private set; }

    public IRunningProcess Start(string executable, string arguments, string workingDirectory, bool redirectStdin = false)
    {
        StartCount++;
        LastProcess = new ControllableFakeProcess();
        return LastProcess;
    }
}


public class FakeProcessFactory(int exitCode, string output, string error) : IProcessFactory
{
    public string? LastExecutable { get; private set; }
    public string? LastArguments { get; private set; }
    public string? LastWorkingDirectory { get; private set; }
    public bool LastRedirectStdin { get; private set; }
    public FakeProcess? LastProcess { get; private set; }

    public IRunningProcess Start(string executable, string arguments, string workingDirectory, bool redirectStdin = false)
    {
        LastExecutable = executable;
        LastArguments = arguments;
        LastWorkingDirectory = workingDirectory;
        LastRedirectStdin = redirectStdin;
        LastProcess = new FakeProcess(exitCode, output, error);
        return LastProcess;
    }
}

public class FakeProcess(int exitCode, string output, string error) : IRunningProcess
{
    public event EventHandler<string>? OutputReceived;
    public event EventHandler<string>? ErrorReceived;
    public int ExitCode { get; private set; }
    public bool WasKilled { get; private set; }
    public List<string> InputLines { get; } = [];

    public void Kill() => WasKilled = true;

    public Task SendInputAsync(string line, CancellationToken ct = default)
    {
        InputLines.Add(line);
        // Echo input back as output so tests can observe it
        OutputReceived?.Invoke(this, line);
        return Task.CompletedTask;
    }

    public Task WaitForExitAsync(CancellationToken ct)
    {
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            OutputReceived?.Invoke(this, line);

        foreach (var line in error.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            ErrorReceived?.Invoke(this, line);

        ExitCode = exitCode;
        return Task.CompletedTask;
    }
}
