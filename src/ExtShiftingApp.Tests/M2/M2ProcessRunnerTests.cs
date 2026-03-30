using ExtShiftingApp.M2;

namespace ExtShiftingApp.Tests.M2;

public class M2ProcessRunnerTests
{
    [Fact]
    public async Task RunScriptAsync_SuccessfulScript_ReturnsTrueWithOutput()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "o1\no2", error: "");
        var runner = new M2ProcessRunner(fake, workingDirectory: "/m2");

        var result = await runner.RunScriptAsync("/m2/script.m2");

        Assert.True(result.Success);
        Assert.Contains("o1", result.Output);
        Assert.Contains("o2", result.Output);
    }

    [Fact]
    public async Task RunScriptAsync_NonZeroExit_ReturnsFalse()
    {
        var fake = new FakeProcessFactory(exitCode: 1, output: "", error: "error: syntax error");
        var runner = new M2ProcessRunner(fake, workingDirectory: "/m2");

        var result = await runner.RunScriptAsync("/m2/bad.m2");

        Assert.False(result.Success);
        Assert.Contains("syntax error", result.Output);
    }

    [Fact]
    public async Task RunScriptAsync_CallsOnOutputCallbackPerLine()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "line1\nline2\nline3", error: "");
        var runner = new M2ProcessRunner(fake, workingDirectory: "/m2");
        var received = new List<string>();

        await runner.RunScriptAsync("/m2/script.m2", onOutput: line => received.Add(line));

        Assert.Equal(["line1", "line2", "line3"], received);
    }

    [Fact]
    public async Task RunScriptAsync_PassesCorrectExecutableAndScript()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var runner = new M2ProcessRunner(fake, workingDirectory: "/m2/ext-shifting");

        await runner.RunScriptAsync("/m2/ext-shifting/libs.m2");

        Assert.Equal("M2", fake.LastExecutable);
        Assert.Contains("libs.m2", fake.LastArguments);
        Assert.Equal("/m2/ext-shifting", fake.LastWorkingDirectory);
    }

    [Fact]
    public async Task RunScriptAsync_QuotesPathsWithSpaces()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var runner = new M2ProcessRunner(fake, workingDirectory: "/m2");

        await runner.RunScriptAsync("/m2/analyze triangs.m2");

        Assert.Equal("--script \"/m2/analyze triangs.m2\"", fake.LastArguments);
    }

    [Fact]
    public async Task RunScriptAsync_LargeOutput_CapsOutputToRecentLines()
    {
        // 200 lines with zero-padded unambiguous markers
        var manyLines = string.Join("\n", Enumerable.Range(1, 200).Select(i => $"marker-{i:D3}"));
        var fake = new FakeProcessFactory(exitCode: 2, output: "", error: manyLines);
        var runner = new M2ProcessRunner(fake, workingDirectory: "/m2");

        var result = await runner.RunScriptAsync("/m2/script.m2");

        Assert.Contains("marker-200", result.Output);    // recent lines kept
        Assert.DoesNotContain("marker-001", result.Output); // oldest lines dropped
    }

    [Fact]
    public async Task RunScriptAsync_BroadcastsStderrLines()
    {
        var fake = new FakeProcessFactory(exitCode: 1, output: "", error: "err1\nerr2");
        var runner = new M2ProcessRunner(fake, workingDirectory: "/m2");
        var received = new List<string>();

        await runner.RunScriptAsync("/m2/script.m2", onOutput: line => received.Add(line));

        Assert.Contains("err1", received);
        Assert.Contains("err2", received);
    }
}
