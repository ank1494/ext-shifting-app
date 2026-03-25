using System.Diagnostics;

namespace ExtShiftingApp.M2;

public class SystemProcessFactory : IProcessFactory
{
    public IRunningProcess Start(string executable, string arguments, string workingDirectory, bool redirectStdin = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = redirectStdin,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        return new SystemRunningProcess(process);
    }
}

public class SystemRunningProcess : IRunningProcess
{
    private readonly Process _process;
    private readonly TaskCompletionSource _exited = new();

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<string>? ErrorReceived;
    public int ExitCode => _process.ExitCode;

    public SystemRunningProcess(Process process)
    {
        _process = process;

        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                OutputReceived?.Invoke(this, e.Data);
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                ErrorReceived?.Invoke(this, e.Data);
        };
        _process.Exited += (_, _) => _exited.TrySetResult();

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public async Task WaitForExitAsync(CancellationToken ct)
    {
        await using var reg = ct.Register(() =>
        {
            Kill();
            _exited.TrySetCanceled();
        });
        await _exited.Task;
    }

    public Task SendInputAsync(string line, CancellationToken ct = default)
    {
        if (!_process.StartInfo.RedirectStandardInput)
            throw new InvalidOperationException("Process was not started with stdin redirected.");
        return _process.StandardInput.WriteLineAsync(line.AsMemory(), ct);
    }

    public void Kill()
    {
        try { _process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { } // already exited
    }
}
