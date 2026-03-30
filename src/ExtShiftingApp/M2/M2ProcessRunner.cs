namespace ExtShiftingApp.M2;

public class M2ProcessRunner(IProcessFactory processFactory, string workingDirectory)
{
    public async Task<M2Result> RunScriptAsync(
        string scriptPath,
        Action<string>? onOutput = null,
        CancellationToken ct = default,
        string? scriptArgs = null)
    {
        var output = new System.Text.StringBuilder();
        var args = string.IsNullOrEmpty(scriptArgs)
            ? $"--script \"{scriptPath}\""
            : $"--script \"{scriptPath}\" {scriptArgs}";

        var process = processFactory.Start("M2", args, workingDirectory);

        process.OutputReceived += (_, line) =>
        {
            output.AppendLine(line);
            onOutput?.Invoke(line);
        };
        process.ErrorReceived += (_, line) => output.AppendLine(line);

        await process.WaitForExitAsync(ct);

        return new M2Result(process.ExitCode == 0, output.ToString());
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
