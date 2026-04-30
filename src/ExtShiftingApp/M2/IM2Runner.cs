namespace ExtShiftingApp.M2;

public interface IM2Runner
{
    Task<M2Result> RunScriptAsync(
        string scriptPath,
        Action<string>? onOutput = null,
        CancellationToken ct = default,
        string? scriptArgs = null);
}
