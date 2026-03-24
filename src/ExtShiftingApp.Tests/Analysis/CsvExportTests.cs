using System.Net;
using ExtShiftingApp.Analysis;
using ExtShiftingApp.Files;
using ExtShiftingApp.M2;
using ExtShiftingApp.Tests.M2;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ExtShiftingApp.Tests.Analysis;

public class CsvExportTests : IDisposable
{
    private readonly string _m2Dir = Path.Combine(Path.GetTempPath(), $"m2csv_{Guid.NewGuid():N}");
    private readonly string _outDir = Path.Combine(Path.GetTempPath(), $"outcsv_{Guid.NewGuid():N}");

    public CsvExportTests()
    {
        Directory.CreateDirectory(_m2Dir);
        Directory.CreateDirectory(_outDir);
    }

    public void Dispose()
    {
        Directory.Delete(_m2Dir, recursive: true);
        Directory.Delete(_outDir, recursive: true);
    }

    private void WriteSummary(string runName, int iteration, string content)
    {
        var dir = Path.Combine(_m2Dir, "analysis output", runName, $"iteration_{iteration}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Analysis Summary.txt"), content);
    }

    private HttpClient BuildClient()
    {
        var fake = new FakeProcessFactory(exitCode: 0, output: "", error: "");
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(new FileSystemService(_m2Dir));
                services.AddSingleton<IProcessFactory>(fake);
                services.AddSingleton(_ => new M2ProcessRunner(fake, _m2Dir));
                services.AddSingleton(_ => new AnalysisJobManager(new M2ProcessRunner(fake, _m2Dir), _m2Dir, _outDir));
                services.AddSingleton(_m2Dir);
            }));
        return factory.CreateClient();
    }

    [Fact]
    public async Task GetCsv_ContainsHeaders()
    {
        WriteSummary("my-run", 1,
            "the following critical regions were found: {disk:3:0}\n" +
            "largest triangulation with shifting not a prefix had 7 vertices\n" +
            "CALCULATION FINISHED, NO MORE SPLITS FOR CALCULATION");

        var client = BuildClient();
        var response = await client.GetAsync("/analysis/results/my-run/csv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var csv = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("iteration,critical_regions,largest_non_prefix_vertices,converged", csv);
    }

    [Fact]
    public async Task GetCsv_OneRowPerIteration()
    {
        WriteSummary("my-run", 1,
            "the following critical regions were found: {disk:3:0}\n" +
            "largest triangulation with shifting not a prefix had 7 vertices\n");
        WriteSummary("my-run", 2,
            "the following critical regions were found: {disk:3:0}\n" +
            "largest triangulation with shifting not a prefix had 8 vertices\n" +
            "CALCULATION FINISHED, NO MORE SPLITS FOR CALCULATION");

        var client = BuildClient();
        var response = await client.GetAsync("/analysis/results/my-run/csv");
        var csv = await response.Content.ReadAsStringAsync();
        var lines = csv.Trim().Split('\n');

        Assert.Equal(3, lines.Length); // header + 2 data rows
        Assert.Contains("1,", lines[1]);
        Assert.Contains("2,", lines[2]);
    }

    [Fact]
    public async Task GetCsv_ParsesVerticesAndConverged()
    {
        WriteSummary("my-run", 1,
            "the following critical regions were found: {disk:3:0,disk:4:1}\n" +
            "largest triangulation with shifting not a prefix had 9 vertices\n" +
            "CALCULATION FINISHED, NO MORE SPLITS FOR CALCULATION");

        var client = BuildClient();
        var csv = await client.GetAsync("/analysis/results/my-run/csv")
                              .ContinueWith(t => t.Result.Content.ReadAsStringAsync()).Unwrap();
        var dataRow = csv.Trim().Split('\n')[1];

        Assert.Contains("9", dataRow);
        Assert.Contains("true", dataRow);
    }

    [Fact]
    public async Task GetCsv_NonExistentRun_Returns404()
    {
        var client = BuildClient();
        var response = await client.GetAsync("/analysis/results/does-not-exist/csv");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
