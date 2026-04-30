using System.Net;
using ExtShiftingApp.Analysis;
using ExtShiftingApp.Files;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ExtShiftingApp.Tests.Analysis;

public class AnalysisControllerCsvTests : IDisposable
{
    private readonly string _outputPath = Path.Combine(Path.GetTempPath(), $"csv_out_{Guid.NewGuid():N}");
    private readonly string _m2Dir = Path.Combine(Path.GetTempPath(), $"csv_m2_{Guid.NewGuid():N}");
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client => _factory.CreateClient();

    public AnalysisControllerCsvTests()
    {
        Directory.CreateDirectory(_outputPath);
        Directory.CreateDirectory(_m2Dir);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(new FileSystemService(_m2Dir));
                services.AddSingleton<IAnalysisJob>(_ => new AnalysisJob(
                    new FakeM2Runner(), new MemoryJobStateStore(), new StubQueueStateReader(),
                    _outputPath, _m2Dir));
                services.AddSingleton(_m2Dir);
                services.AddSingleton(new OutputPath(_outputPath));
                services.AddSingleton<DoneFileReader>();
            }));
    }

    public void Dispose()
    {
        _factory.Dispose();
        if (Directory.Exists(_outputPath))
            Directory.Delete(_outputPath, recursive: true);
        if (Directory.Exists(_m2Dir))
            Directory.Delete(_m2Dir, recursive: true);
    }

    private void WriteDoneFile(string runName, string filename, int seq, string parent = "seed", int depth = 0,
        string triangulation = "{{0,1,3},{1,3,5},{1,2,5}}",
        string critRegions = "{}",
        string? splitVertex = null, string? splitNeighbors = null)
    {
        var doneDir = Path.Combine(_outputPath, runName, "done");
        Directory.CreateDirectory(doneDir);

        var splitLine = splitVertex != null
            ? $"\n  \"splitFrom\" => new HashTable from {{\"vertex\" => {splitVertex}, \"neighbors\" => {splitNeighbors}}},"
            : "";

        var content = "new HashTable from {\n" +
            $"  \"parent\" => \"{parent}\",\n" +
            $"  \"depth\" => {depth},\n" +
            $"  \"seq\" => {seq},{splitLine}\n" +
            $"  \"triangulation\" => {triangulation},\n" +
            $"  \"critRegions\" => {critRegions}\n" +
            "}";
        File.WriteAllText(Path.Combine(doneDir, filename), content);
    }

    private void CreateRunDir(string runName) =>
        Directory.CreateDirectory(Path.Combine(_outputPath, runName));

    [Fact]
    public async Task GetCsv_UnknownRun_Returns404()
    {
        var response = await _client.GetAsync("/analysis/results/no-such-run/csv");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCsv_SummaryRow_LargestNonPrefixVertexCount()
    {
        // {{0,1,3},{1,3,5},{1,2,5}} has vertices {0,1,2,3,5} = 5 distinct
        WriteDoneFile("my-run", "0006", seq: 6, triangulation: "{{0,1,3},{1,3,5},{1,2,5}}");

        var csv = await _client.GetAsync("/analysis/results/my-run/csv")
            .ContinueWith(t => t.Result.Content.ReadAsStringAsync()).Unwrap();

        Assert.Contains("largest_non_prefix_vertex_count;5", csv);
    }

    [Fact]
    public async Task GetCsv_SummaryRow_CriticalRegionTypes_VerboseFormat()
    {
        WriteDoneFile("my-run", "0006", seq: 6,
            critRegions: "{new HashTable from {\"innerVertexCount\" => 0, \"boundaryVertexCount\" => 3, \"regionShape\" => \"disk\"}}");

        var csv = await _client.GetAsync("/analysis/results/my-run/csv")
            .ContinueWith(t => t.Result.Content.ReadAsStringAsync()).Unwrap();

        Assert.Contains("disk(boundary=3, inner=0)", csv);
    }

    [Fact]
    public async Task GetCsv_ColumnHeaders_Present()
    {
        WriteDoneFile("my-run", "0001", seq: 1);

        var csv = await _client.GetAsync("/analysis/results/my-run/csv")
            .ContinueWith(t => t.Result.Content.ReadAsStringAsync()).Unwrap();

        Assert.Contains("seq;parent;split_vertex;split_between;depth;vertex_count;critRegions;triangulation", csv);
    }

    [Fact]
    public async Task GetCsv_OneDataRowPerDoneFile()
    {
        WriteDoneFile("my-run", "0001", seq: 1);
        WriteDoneFile("my-run", "0002", seq: 2);
        WriteDoneFile("my-run", "0003", seq: 3);

        var csv = await _client.GetAsync("/analysis/results/my-run/csv")
            .ContinueWith(t => t.Result.Content.ReadAsStringAsync()).Unwrap();

        // Find header line index, then count data rows after it
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var headerIdx = Array.FindIndex(lines, l => l.StartsWith("seq;"));
        var dataRows = lines.Skip(headerIdx + 1).ToList();
        Assert.Equal(3, dataRows.Count);
    }

    [Fact]
    public async Task GetCsv_SeedItems_SplitColumnsBlank()
    {
        WriteDoneFile("my-run", "0001", seq: 1, parent: "seed");

        var csv = await _client.GetAsync("/analysis/results/my-run/csv")
            .ContinueWith(t => t.Result.Content.ReadAsStringAsync()).Unwrap();

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var headerIdx = Array.FindIndex(lines, l => l.StartsWith("seq;"));
        var dataRow = lines[headerIdx + 1];
        var cols = dataRow.Split(';');

        // seq;parent;split_vertex;split_between;...
        Assert.Equal("", cols[2]); // split_vertex
        Assert.Equal("", cols[3]); // split_between (not quoted)
    }

    [Fact]
    public async Task GetCsv_SplitItems_SplitColumnsPopulated()
    {
        WriteDoneFile("my-run", "0002", seq: 2, parent: "0001", depth: 1,
            splitVertex: "6", splitNeighbors: "{1,3}");

        var csv = await _client.GetAsync("/analysis/results/my-run/csv")
            .ContinueWith(t => t.Result.Content.ReadAsStringAsync()).Unwrap();

        Assert.Contains("6", csv);
        Assert.Contains("vertex=6", csv);
        Assert.Contains("neighbors={1,3}", csv);
    }

    [Fact]
    public async Task GetCsv_CritRegions_VerboseFormat()
    {
        WriteDoneFile("my-run", "0006", seq: 6,
            critRegions: "{new HashTable from {\"innerVertexCount\" => 0, \"boundaryVertexCount\" => 3, \"regionShape\" => \"disk\"}}");

        var csv = await _client.GetAsync("/analysis/results/my-run/csv")
            .ContinueWith(t => t.Result.Content.ReadAsStringAsync()).Unwrap();

        Assert.Contains("disk(boundary=3, inner=0)", csv);
    }

    [Fact]
    public async Task GetCsv_EmptyDoneDir_ValidCsvWithSummaryRowsNoDataRows()
    {
        CreateRunDir("empty-run");
        Directory.CreateDirectory(Path.Combine(_outputPath, "empty-run", "done"));

        var csv = await _client.GetAsync("/analysis/results/empty-run/csv")
            .ContinueWith(t => t.Result.Content.ReadAsStringAsync()).Unwrap();

        Assert.Contains("largest_non_prefix_vertex_count;0", csv);
        Assert.Contains("critical_region_types;", csv);
        Assert.Contains("seq;parent;split_vertex;split_between;depth;vertex_count;critRegions;triangulation", csv);

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var headerIdx = Array.FindIndex(lines, l => l.StartsWith("seq;"));
        var dataRows = lines.Skip(headerIdx + 1).ToList();
        Assert.Empty(dataRows);
    }

    [Fact]
    public async Task GetCsv_ContentType_IsCsv()
    {
        WriteDoneFile("my-run", "0001", seq: 1);

        var response = await _client.GetAsync("/analysis/results/my-run/csv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
    }
}
