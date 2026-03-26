using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ExtShiftingApp.Tests.Analysis;

public class AnalysisControllerTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetStatus_ReturnsStatusAsString()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/analysis/status");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var statusValue = doc.RootElement.GetProperty("status");
        Assert.Equal(JsonValueKind.String, statusValue.ValueKind);
        Assert.False(string.IsNullOrEmpty(statusValue.GetString()));
    }

    [Fact]
    public async Task PostStop_ResetsStatusToIdleOrStopped()
    {
        var client = factory.CreateClient();

        await client.PostAsync("/analysis/stop", null);
        var response = await client.GetAsync("/analysis/status");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var status = doc.RootElement.GetProperty("status").GetString();
        Assert.True(status is "Idle" or "Stopped", $"Expected Idle or Stopped, got: {status}");
    }
}
