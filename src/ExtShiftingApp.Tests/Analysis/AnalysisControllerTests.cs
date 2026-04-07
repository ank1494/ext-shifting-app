using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ExtShiftingApp.Tests.Analysis;

public class AnalysisControllerTests : IDisposable
{
    private readonly string _outputPath = Path.Combine(Path.GetTempPath(), $"ctrl_out_{Guid.NewGuid():N}");
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client => _factory.CreateClient();

    public AnalysisControllerTests()
    {
        Directory.CreateDirectory(_outputPath);
        Environment.SetEnvironmentVariable("OUTPUT_PATH", _outputPath);
        _factory = new WebApplicationFactory<Program>();
    }

    public void Dispose()
    {
        _factory.Dispose();
        Environment.SetEnvironmentVariable("OUTPUT_PATH", null);
        if (Directory.Exists(_outputPath))
            Directory.Delete(_outputPath, recursive: true);
    }

    // Helper: create a run directory in the app's output path
    private void SeedRunDir(string runName) =>
        Directory.CreateDirectory(Path.Combine(_outputPath, runName));


    [Fact]
    public async Task GetStatus_ReturnsStatusAsString()
    {
        var response = await _client.GetAsync("/analysis/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var statusValue = doc.RootElement.GetProperty("status");
        Assert.Equal(JsonValueKind.String, statusValue.ValueKind);
        Assert.False(string.IsNullOrEmpty(statusValue.GetString()));
    }

    [Fact]
    public async Task PostStop_ResetsStatusToIdleOrStopped()
    {
        await _client.PostAsync("/analysis/stop", null);
        var response = await _client.GetAsync("/analysis/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var status = doc.RootElement.GetProperty("status").GetString();
        Assert.True(status is "Idle" or "Paused" or "Failed" or "Stopped", $"Unexpected status: {status}");
    }

    [Fact]
    public async Task PostStart_Returns409_WhenRunDirectoryAlreadyExists()
    {
        var runName = $"dup-{Guid.NewGuid():N}";
        SeedRunDir(runName);

        var response = await _client.PostAsJsonAsync("/analysis/start",
            new { runName, surfaceType = "torus" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostResume_Returns404_ForUnknownRunName()
    {
        var response = await _client.PostAsJsonAsync("/analysis/resume",
            new { runName = $"no-such-run-{Guid.NewGuid():N}" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostResume_Returns409_WhenJobNotPaused()
    {
        // App starts Idle — Resume on an Idle job returns 409 (not paused)
        var runName = $"resume-{Guid.NewGuid():N}";
        SeedRunDir(runName);

        var response = await _client.PostAsJsonAsync("/analysis/resume", new { runName });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
