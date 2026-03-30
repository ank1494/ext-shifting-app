namespace ExtShiftingApp.M2;

public class M2ProcessRunner(IProcessFactory processFactory, string workingDirectory)
{
    public async Task<M2Result> RunScriptAsync(
        string scriptPath,
        Action<string>? onOutput = null,
        CancellationToken ct = default,
        string? scriptArgs = null)
    {
        const int maxOutputLines = 100;
        var recentLines = new Queue<string>();
        var args = string.IsNullOrEmpty(scriptArgs)
            ? $"--script \"{scriptPath}\""
            : $"--script \"{scriptPath}\" {scriptArgs}";

        var process = processFactory.Start("M2", args, workingDirectory);

        void capture(string line)
        {
            if (recentLines.Count >= maxOutputLines) recentLines.Dequeue();
            recentLines.Enqueue(line);
        }

        process.OutputReceived += (_, line) => { capture(line); onOutput?.Invoke(line); };
        process.ErrorReceived += (_, line) => { capture(line); onOutput?.Invoke(line); };

        await process.WaitForExitAsync(ct);

        return new M2Result(process.ExitCode == 0, string.Join("\n", recentLines), process.ExitCode);
    }

    public IInteractiveSession StartInteractiveSession()
    {
        var process = processFactory.Start("M2", "", workingDirectory, redirectStdin: true);
        return new M2InteractiveSession(process);
    }

    public async Task<M2Result> RunCommandAsync(
        string m2Code,
        Action<string>? onOutput = null,
        CancellationToken ct = default)
    {
        var tmpScript = Path.Combine(Path.GetTempPath(), $"m2_{Guid.NewGuid():N}.m2");
        try
        {
            await File.WriteAllTextAsync(tmpScript, m2Code, ct);
            return await RunScriptAsync(tmpScript, onOutput, ct);
        }
        finally
        {
            File.Delete(tmpScript);
        }
    }
}
