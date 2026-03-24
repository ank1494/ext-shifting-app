namespace ExtShiftingApp.M2;

public class M2ProcessRunner(IProcessFactory processFactory, string workingDirectory)
{
    public async Task<M2Result> RunScriptAsync(
        string scriptPath,
        Action<string>? onOutput = null,
        CancellationToken ct = default)
    {
        var output = new System.Text.StringBuilder();

        var process = processFactory.Start("M2", $"--script {scriptPath}", workingDirectory);

        process.OutputReceived += (_, line) =>
        {
            output.AppendLine(line);
            onOutput?.Invoke(line);
        };
        process.ErrorReceived += (_, line) => output.AppendLine(line);

        await process.WaitForExitAsync(ct);

        return new M2Result(process.ExitCode == 0, output.ToString());
    }
}
