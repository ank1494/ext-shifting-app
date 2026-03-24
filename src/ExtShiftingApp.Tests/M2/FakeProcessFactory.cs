using ExtShiftingApp.M2;

namespace ExtShiftingApp.Tests.M2;

public class FakeProcessFactory(int exitCode, string output, string error) : IProcessFactory
{
    public string? LastExecutable { get; private set; }
    public string? LastArguments { get; private set; }
    public string? LastWorkingDirectory { get; private set; }

    public IRunningProcess Start(string executable, string arguments, string workingDirectory)
    {
        LastExecutable = executable;
        LastArguments = arguments;
        LastWorkingDirectory = workingDirectory;
        return new FakeProcess(exitCode, output, error);
    }
}

public class FakeProcess(int exitCode, string output, string error) : IRunningProcess
{
    public event EventHandler<string>? OutputReceived;
    public event EventHandler<string>? ErrorReceived;
    public int ExitCode { get; private set; }
    public void Kill() { }

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
