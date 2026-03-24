using System.Net;
using System.Net.Http.Json;
using ExtShiftingApp.M2;
using ExtShiftingApp.Shift;
using ExtShiftingApp.Tests.M2;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ExtShiftingApp.Tests.Shift;

public class ShiftControllerTests
{
    private HttpClient BuildClient(FakeProcessFactory fake)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IProcessFactory>(fake);
                services.AddSingleton(_ => new M2ProcessRunner(fake, workingDirectory: "/m2"));
            }));
        return factory.CreateClient();
    }

    [Fact]
    public async Task PostShift_ValidInput_ReturnsOkWithOutput()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "set {{0,1},{0,2}}", error: "");
        var client = BuildClient(fake);

        var response = await client.PostAsJsonAsync("/shift",
            new ShiftRequest("{{1,2},{1,3},{3,4}}", "lex"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShiftResponse>();
        Assert.True(body!.Success);
        Assert.Contains("set", body.Output);
    }

    [Fact]
    public async Task PostShift_InvalidInput_ReturnsBadRequest()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var client = BuildClient(fake);

        var response = await client.PostAsJsonAsync("/shift",
            new ShiftRequest("not valid", "lex"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShiftResponse>();
        Assert.False(body!.Success);
        Assert.NotNull(body.Error);
    }

    [Fact]
    public async Task PostShift_M2Failure_Returns500()
    {
        var fake = new FakeProcessFactory(exitCode: 1, output: "", error: "M2 error");
        var client = BuildClient(fake);

        var response = await client.PostAsJsonAsync("/shift",
            new ShiftRequest("{{1,2},{1,3}}", "lex"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShiftResponse>();
        Assert.False(body!.Success);
    }
}
