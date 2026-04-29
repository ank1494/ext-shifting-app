using ExtShiftingApp.M2;

namespace ExtShiftingApp.Tests;

public class M2IntegrationTests
{
    private static string M2Root =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../m2/ext-shifting"));

    private static string TestsDir => Path.Combine(M2Root, "tests");

    private static readonly WslAwareM2Runner Runner = new();

    private static void SkipIfUnavailable()
    {
        if (!Runner.IsAvailable()) return;
    }

    private static async Task RunScript(string scriptName)
    {
        if (!Runner.IsAvailable()) return; // skip: M2 not available
        var result = await Runner.RunScriptAsync(Path.Combine(TestsDir, scriptName), M2Root);
        Assert.True(result.Success, result.Output);
    }

    [Fact]
    [Trait("Category", "M2Integration")]
    public Task CriticalRegions_Structure_Passes() => RunScript("criticalRegions-structure.m2");

    [Fact]
    [Trait("Category", "M2Integration")]
    public Task CriticalRegions_Kb25_Passes() => RunScript("criticalRegions-kb25.m2");

    [Fact]
    [Trait("Category", "M2Integration")]
    public Task AnalyzeIteration_Kb25_Passes() => RunScript("analyzeIteration-kb25.m2");

    [Fact]
    [Trait("Category", "M2Integration")]
    public Task AnalyzeIteration_Torus7v_NoCritRegions_Passes() => RunScript("analyzeIteration-torus7v.m2");

    [Fact]
    [Trait("Category", "M2Integration")]
    public Task QueueOps_ProcessItem_Passes() => RunScript("queueOps-processItem.m2");

    [Fact]
    [Trait("Category", "M2Integration")]
    public Task QueueOps_RunQueue_Passes() => RunScript("queueOps-runQueue.m2");

    [Fact]
    [Trait("Category", "M2Integration")]
    public Task TestProjectivePlane_Passes() => RunScript("testProjectivePlane.m2");

    [Fact]
    [Trait("Category", "M2Integration")]
    public Task TestTorus_Passes() => RunScript("testTorus.m2");

    [Fact]
    [Trait("Category", "M2Integration")]
    public Task TestKleinBottle_Passes() => RunScript("testKleinBottle.m2");

    [Fact]
    [Trait("Category", "M2Integration")]
    public Task ExemptSplits_Tori_Passes() => RunScript("exemptSplits-tori.m2");

    [Fact]
    [Trait("Category", "M2Integration")]
    public Task ExemptSplits_Kb_Passes() => RunScript("exemptSplits-kb.m2");

    [Fact]
    [Trait("Category", "M2Integration")]
    public async Task M2LibChecks_PassAllUnitTests()
    {
        if (!Runner.IsAvailable()) return; // skip: M2 not available
        var result = await Runner.RunCommandAsync("check \"ExtShifting\"", M2Root);
        Assert.True(result.Success, result.Output);
    }

    [Fact]
    [Trait("Category", "M2Integration")]
    public async Task FailingScript_ReportsFailure()
    {
        if (!Runner.IsAvailable()) return; // skip: M2 not available
        var result = await Runner.RunCommandAsync("assert false", M2Root);
        Assert.False(result.Success, "Expected a failing M2 assertion to be reported as failure");
    }
}
