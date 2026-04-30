using Microsoft.AspNetCore.Mvc;

namespace ExtShiftingApp.Analysis;

[ApiController]
public class AnalysisController(IAnalysisJob job, string m2RepoPath, OutputPath outputPath, DoneFileReader doneFileReader) : ControllerBase
{
    private static readonly Dictionary<string, string> SurfaceInputFiles = new()
    {
        ["torus"]          = "data/surface triangulations/irredTori.m2",
        ["kleinbottle"]    = "data/surface triangulations/irredKb.m2",
        ["projectiveplane"]= "data/surface triangulations/irredPp.m2",
    };

    [HttpPost("/analysis/start")]
    public async Task<IActionResult> Start([FromBody] StartAnalysisRequest request)
    {
        string inputFilePath;

        if (!string.IsNullOrWhiteSpace(request.SurfaceType))
        {
            var key = request.SurfaceType.ToLowerInvariant().Replace(" ", "");
            if (!SurfaceInputFiles.TryGetValue(key, out var relative))
                return BadRequest(new { error = $"Unknown surface type '{request.SurfaceType}'." });
            inputFilePath = Path.Combine(m2RepoPath, relative);
        }
        else if (!string.IsNullOrWhiteSpace(request.CustomFilePath))
        {
            inputFilePath = request.CustomFilePath;
        }
        else
        {
            return BadRequest(new { error = "Provide either surfaceType or customFilePath." });
        }

        if (job.RunExists(request.RunName))
            return Conflict(new { error = $"A run named '{request.RunName}' already exists." });

        try
        {
            await job.StartAsync(request.RunName, inputFilePath, request.Batch);
            return Ok(new { started = true, runName = request.RunName });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("/analysis/resume")]
    public async Task<IActionResult> Resume([FromBody] ResumeAnalysisRequest request)
    {
        if (!job.RunExists(request.RunName))
            return NotFound(new { error = $"No run named '{request.RunName}' exists." });

        try
        {
            await job.ResumeAsync(request.RunName, request.Batch);
            return Ok(new { resumed = true, runName = request.RunName });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("/analysis/stop")]
    public async Task<IActionResult> Stop()
    {
        await job.StopAsync();
        return Ok(new { stopped = true });
    }

    [HttpGet("/analysis/status")]
    public IActionResult Status() => Ok(job.GetSnapshot());

    [HttpGet("/analysis/stream")]
    public async Task Stream(CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        await foreach (var line in job.OpenOutputStream().ReadAllAsync(ct))
            await WriteEvent(line, ct);
    }

    [HttpGet("/analysis/results/{runName}/csv")]
    public IActionResult Csv(string runName)
    {
        var runDir = Path.Combine(outputPath.Value, runName);
        if (!Directory.Exists(runDir))
            return NotFound(new { error = $"No results found for run '{runName}'." });

        var items = doneFileReader.Read(runDir);

        var largestVertexCount = items.Count > 0 ? items.Max(i => i.VertexCount) : 0;
        var distinctTriples = items
            .SelectMany(i => i.CritRegions)
            .Select(r => (r.RegionShape, r.BoundaryVertexCount, r.InnerVertexCount))
            .Distinct()
            .ToList();
        var critRegionTypes = string.Join(", ", distinctTriples.Select(t =>
            $"{t.RegionShape}(boundary={t.BoundaryVertexCount}, inner={t.InnerVertexCount})"));

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"largest_non_prefix_vertex_count;{largestVertexCount}");
        sb.AppendLine($"critical_region_types;{critRegionTypes}");
        sb.AppendLine();
        sb.AppendLine("seq;parent;split_vertex;split_between;depth;vertex_count;critRegions;triangulation");

        foreach (var item in items)
        {
            var splitVertex = item.SplitFrom?.Vertex.ToString() ?? "";
            var splitBetween = item.SplitFrom != null
                ? $"vertex={item.SplitFrom.Vertex}, neighbors={item.SplitFrom.Neighbors}"
                : "";
            var critRegions = string.Join(", ", item.CritRegions.Select(r =>
                $"{r.RegionShape}(boundary={r.BoundaryVertexCount}, inner={r.InnerVertexCount})"));
            sb.AppendLine($"{item.Seq};{item.Parent};{splitVertex};{splitBetween};{item.Depth};{item.VertexCount};{critRegions};{item.Triangulation}");
        }

        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()),
            "text/csv", $"{runName}-results.csv");
    }

    [HttpGet("/analysis/results/{iteration:int}")]
    public IActionResult Results(int iteration, [FromQuery] string runName)
    {
        if (string.IsNullOrWhiteSpace(runName))
            runName = job.GetSnapshot().RunName ?? "";

        var summaryPath = Path.Combine(
            m2RepoPath, "analysis output", runName,
            $"iteration_{iteration}", "Analysis Summary.txt");

        if (!System.IO.File.Exists(summaryPath))
            return NotFound(new { error = "Results not found for this iteration." });

        var content = System.IO.File.ReadAllText(summaryPath);
        return Ok(new { iteration, content });
    }

    private async Task WriteEvent(string line, CancellationToken ct)
    {
        var json = SseEventParser.Parse(line);
        await Response.WriteAsync($"data: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}

public record StartAnalysisRequest(string RunName, string? SurfaceType, string? CustomFilePath, BatchParameters? Batch = null);
public record ResumeAnalysisRequest(string RunName, BatchParameters? Batch = null);
public record OutputPath(string Value);
