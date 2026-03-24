using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ExtShiftingApp.Analysis;

[ApiController]
public class AnalysisController(AnalysisJobManager jobManager, string m2RepoPath) : ControllerBase
{
    private static readonly Dictionary<string, string> SurfaceInputFiles = new()
    {
        ["torus"]          = "surface triangulations/irred tori.m2",
        ["kleinbottle"]    = "surface triangulations/irred kb.m2",
        ["projectiveplane"]= "surface triangulations/irred pp.m2",
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

        try
        {
            jobManager.Start(request.RunName, inputFilePath);
            return Ok(new { started = true, runName = request.RunName });
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
                if (line.Contains("no more splits") || ct.IsCancellationRequested)
                    tcs.TrySetResult();
            };

            jobManager.Subscribe(handler);
            ct.Register(() => tcs.TrySetResult());
            try { await tcs.Task; }
            finally { jobManager.Unsubscribe(handler); }
        }
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
        var json = JsonSerializer.Serialize(new { line });
        await Response.WriteAsync($"data: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}

public record StartAnalysisRequest(string RunName, string? SurfaceType, string? CustomFilePath);
