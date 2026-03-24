using System.Text;
using System.Text.Json;
using ExtShiftingApp.M2;
using Microsoft.AspNetCore.Mvc;

namespace ExtShiftingApp.Files;

[ApiController]
public class FilesController(FileSystemService fileSystem, M2ProcessRunner m2) : ControllerBase
{
    [HttpGet("/files")]
    public IActionResult List() => Ok(fileSystem.ListM2Files());

    [HttpPost("/files/run")]
    public async Task Run([FromBody] RunFileRequest request, CancellationToken ct)
    {
        var fullPath = fileSystem.ResolveFilePath(request.File);
        if (fullPath is null)
        {
            Response.StatusCode = 400;
            await Response.WriteAsync("File not found or path is invalid.", ct);
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        async Task SendEvent(object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            await Response.WriteAsync($"data: {json}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        var result = await m2.RunScriptAsync(
            fullPath,
            onOutput: line => SendEvent(new { type = "output", line }).GetAwaiter().GetResult(),
            ct: ct);

        await SendEvent(new { type = "done", success = result.Success });
    }
}

public record RunFileRequest(string File);
