using ExtShiftingApp.M2;

namespace ExtShiftingApp.Tests.M2;

public class M2InteractiveSessionTests
{
    [Fact]
    public void StartInteractiveSession_StartsM2WithNoScriptFlag()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var runner = new M2ProcessRunner(fake, workingDirectory: "/m2");

        using var _ = runner.StartInteractiveSession();

        Assert.Equal("M2", fake.LastExecutable);
        Assert.Equal("", fake.LastArguments);
        Assert.True(fake.LastRedirectStdin);
    }

    [Fact]
    public async Task StartInteractiveSession_OutputReceivedEventFires()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var runner = new M2ProcessRunner(fake, workingDirectory: "/m2");
        var received = new List<string>();

        await using var session = runner.StartInteractiveSession();
        session.OutputReceived += (_, line) => received.Add(line);

        await session.SendInputAsync("1 + 1");

        Assert.Contains("1 + 1", received);
    }

    [Fact]
    public async Task StartInteractiveSession_SendInputAsync_WritesToProcess()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var runner = new M2ProcessRunner(fake, workingDirectory: "/m2");

        await using var session = runner.StartInteractiveSession();
        await session.SendInputAsync("1 + 1");
        await session.SendInputAsync("exit 0");

        Assert.Equal(["1 + 1", "exit 0"], fake.LastProcess!.InputLines);
    }

    [Fact]
    public async Task DisposeSession_KillsProcess()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var runner = new M2ProcessRunner(fake, workingDirectory: "/m2");

        await using (runner.StartInteractiveSession()) { }

        Assert.True(fake.LastProcess!.WasKilled);
    }
}
