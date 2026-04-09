using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ExtShiftingApp.Analysis;

[ApiController]
public class AnalysisController(AnalysisJobManager jobManager, string m2RepoPath) : ControllerBase
{
    private static readonly Dictionary<string, string> SurfaceInputFiles = new()
    {
        ["torus"]          = "data/surface triangulations/irredTori.m2",
        ["kleinbottle"]    = "data/surface triangulations/irredKb.m2",
        ["projectiveplane"]= "data/surface triangulations/irredPp.m2",
    };

    [HttpPost("/analysis/start")]
    public IActionResult Start([FromBody] StartAnalysisRequest request)
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

        if (jobManager.RunExists(request.RunName))
            return Conflict(new { error = $"A run named '{request.RunName}' already exists." });

        try
        {
            jobManager.Start(request.RunName, inputFilePath, request.Batch);
            return Ok(new { started = true, runName = request.RunName });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("/analysis/resume")]
    public IActionResult Resume([FromBody] ResumeAnalysisRequest request)
    {
        if (!jobManager.RunExists(request.RunName))
            return NotFound(new { error = $"No run named '{request.RunName}' exists." });

        try
        {
            jobManager.Resume(request.RunName, request.Batch);
            return Ok(new { resumed = true, runName = request.RunName });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("/analysis/stop")]
    public IActionResult Stop()
    {
        jobManager.Stop();
        return Ok(new { stopped = true });
    }

    [HttpGet("/analysis/status")]
    public IActionResult Status() => Ok(jobManager.GetState());

    [HttpGet("/analysis/stream")]
    public async Task Stream(CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        // Replay existing log first
        foreach (var line in jobManager.GetOutputLog())
            await WriteEvent(line, ct);

        // Then subscribe to live output
        if (jobManager.GetState().Status == JobStatus.Running)
        {
            var tcs = new TaskCompletionSource();
            EventHandler<string> handler = async (_, line) =>
            {
                await WriteEvent(line, ct);
                if (ct.IsCancellationRequested)
                    tcs.TrySetResult();
            };

            jobManager.Subscribe(handler);
            ct.Register(() => tcs.TrySetResult());
            try { await tcs.Task; }
            finally { jobManager.Unsubscribe(handler); }
        }
    }

    [HttpGet("/analysis/results/{runName}/csv")]
    public IActionResult Csv(string runName)
    {
        var runDir = Path.Combine(m2RepoPath, "analysis output", runName);
        if (!Directory.Exists(runDir))
            return NotFound(new { error = $"No results found for run '{runName}'." });

        var iterationDirs = Directory.GetDirectories(runDir, "iteration_*")
            .Select(d => (dir: d, n: int.TryParse(Path.GetFileName(d).Replace("iteration_", ""), out var n) ? n : 0))
            .Where(x => x.n > 0)
            .OrderBy(x => x.n)
            .ToList();

        if (iterationDirs.Count == 0)
            return NotFound(new { error = "No iteration results found." });

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("iteration,critical_regions,largest_non_prefix_vertices,converged");

        foreach (var (dir, n) in iterationDirs)
        {
            var summaryPath = Path.Combine(dir, "Analysis Summary.txt");
            if (!System.IO.File.Exists(summaryPath)) continue;

            var summary = SummaryParser.Parse(n, System.IO.File.ReadAllText(summaryPath));
            var regions = summary.CriticalRegions.Replace(",", ";"); // avoid CSV collision
            sb.AppendLine($"{summary.Iteration},{regions},{summary.LargestNonPrefixVertices},{summary.Converged.ToString().ToLower()}");
        }

        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()),
            "text/csv", $"{runName}-results.csv");
    }

    [HttpGet("/analysis/results/{iteration:int}")]
    public IActionResult Results(int iteration, [FromQuery] string runName)
    {
        if (string.IsNullOrWhiteSpace(runName))
            runName = jobManager.GetState().RunName ?? "";

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
