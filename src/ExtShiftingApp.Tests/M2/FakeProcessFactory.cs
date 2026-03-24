using ExtShiftingApp.M2;

namespace ExtShiftingApp.Tests.M2;

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
