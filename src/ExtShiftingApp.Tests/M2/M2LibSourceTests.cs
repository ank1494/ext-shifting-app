namespace ExtShiftingApp.Tests.M2;

/// <summary>
/// Regression tests for the M2 library source files.
/// These catch anti-patterns that compile/load silently but fail at runtime inside M2.
/// </summary>
public class M2LibSourceTests
{
    // Navigate from the test binary (src/ExtShiftingApp.Tests/bin/Debug/net8.0/) up to the repo root.
    private static string M2Root =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../m2/ext-shifting"));

    private static string LibDir => Path.Combine(M2Root, "lib");
    private static string ScriptsDir => Path.Combine(M2Root, "scripts");

    [Fact]
    public void LibFiles_DoNotContainBareNeedsWithoutLibPrefix()
    {
        // `needs "utils.m2"` worked when all files were at the repo root, but after the
        // restructuring into lib/ the working directory is /m2/ext-shifting so bare names
        // like "utils.m2" are not found — they must be "lib/utils.m2".
        Assert.True(Directory.Exists(LibDir), $"lib dir not found at {LibDir}");

        var violations = Directory.GetFiles(LibDir, "*.m2")
            .Select(f => (file: Path.GetFileName(f), content: File.ReadAllText(f)))
            .Where(x => System.Text.RegularExpressions.Regex.IsMatch(
                x.content, @"needs\s+""(?!lib/)"))
            .Select(x => x.file)
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void RunAnalysis_DefinesLogInfoAndLogException()
    {
        // criticalRegions.m2 calls logInfo and logException, which are only defined in
        // runSplitsAnalysis.m2 — not in runAnalysis.m2. When runAnalysis.m2 runs analyzeIteration,
        // those symbols are undefined and M2 crashes.
        var script = File.ReadAllText(Path.Combine(ScriptsDir, "runAnalysis.m2"));
        Assert.Contains("logInfo =", script);
        Assert.Contains("logException =", script);
    }

    [Fact]
    public void InitAnalysisEnv_UsesTryMkdirForIterationOutputDir()
    {
        // On retry after a crash, the iteration output directory already exists.
        // A bare `mkdir` throws; it must be wrapped in `try` so initialization succeeds.
        var script = File.ReadAllText(Path.Combine(ScriptsDir, "initAnalysisEnv.m2"));
        Assert.DoesNotMatch(@"(?<!try\s)(?<!try\()mkdir iterationOutputDir", script);
        Assert.Contains("try mkdir iterationOutputDir", script);
    }
}
