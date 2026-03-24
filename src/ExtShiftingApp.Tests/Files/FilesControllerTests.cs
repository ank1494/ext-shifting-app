using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ExtShiftingApp.Files;
using ExtShiftingApp.M2;
using ExtShiftingApp.Tests.M2;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ExtShiftingApp.Tests.Files;

public class FilesControllerTests : IDisposable
{
    private readonly string _repoDir = Path.Combine(Path.GetTempPath(), $"m2ctrl_{Guid.NewGuid():N}");

    public FilesControllerTests() => Directory.CreateDirectory(_repoDir);
    public void Dispose() => Directory.Delete(_repoDir, recursive: true);

    private void CreateFile(string relativePath, string content = "")
    {
        var fullPath = Path.Combine(_repoDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private HttpClient BuildClient(FakeProcessFactory fake)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(new FileSystemService(_repoDir));
                services.AddSingleton<IProcessFactory>(fake);
                services.AddSingleton(_ => new M2ProcessRunner(fake, workingDirectory: _repoDir));
            }));
        return factory.CreateClient();
    }

    [Fact]
    public async Task GetFiles_ReturnsM2FileList()
    {
        CreateFile("libs.m2");
        CreateFile("utils.m2");
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var client = BuildClient(fake);

        var response = await client.GetAsync("/files");
        var files = await response.Content.ReadFromJsonAsync<List<string>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, files!.Count);
        Assert.All(files, f => Assert.EndsWith(".m2", f));
    }

    [Fact]
    public async Task PostFilesRun_ValidFile_StreamsSseOutputAndDoneEvent()
    {
        CreateFile("script.m2");
        var fake = new FakeProcessFactory(exitCode: 0, output: "line1\nline2", error: "");
        var client = BuildClient(fake);

        var response = await client.PostAsJsonAsync("/files/run", new RunFileRequest("script.m2"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"type\":\"output\"", body);
        Assert.Contains("line1", body);
        Assert.Contains("line2", body);
        Assert.Contains("\"type\":\"done\"", body);
        Assert.Contains("\"success\":true", body);
    }

    [Fact]
    public async Task PostFilesRun_InvalidPath_ReturnsBadRequest()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var client = BuildClient(fake);

        var response = await client.PostAsJsonAsync("/files/run", new RunFileRequest("../../../etc/passwd"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostFilesRun_M2Failure_StreamsDoneWithSuccessFalse()
    {
        CreateFile("bad.m2");
        var fake = new FakeProcessFactory(exitCode: 1, output: "", error: "syntax error");
        var client = BuildClient(fake);

        var response = await client.PostAsJsonAsync("/files/run", new RunFileRequest("bad.m2"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"success\":false", body);
    }
}
