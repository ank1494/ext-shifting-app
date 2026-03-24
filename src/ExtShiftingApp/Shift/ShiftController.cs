using ExtShiftingApp.M2;
using Microsoft.AspNetCore.Mvc;

namespace ExtShiftingApp.Shift;

[ApiController]
[Route("[controller]")]
public class ShiftController(M2ProcessRunner m2) : ControllerBase
{
    [HttpPost("/shift")]
    public async Task<IActionResult> Shift([FromBody] ShiftRequest request, CancellationToken ct)
    {
        var parsed = SimplexParser.Parse(request.Simplices);
        if (!parsed.IsValid)
            return BadRequest(new ShiftResponse(false, request.Simplices, null, parsed.Error));

        var ordering = request.Ordering.ToLowerInvariant() == "revlex" ? "RevLex" : "Lex";
        var m2Code = $"""
            load "libs.m2";
            print toString extShift{ordering} {request.Simplices.Trim()};
            exit 0
            """;

        var result = await m2.RunCommandAsync(m2Code, ct: ct);

        if (!result.Success)
            return StatusCode(500, new ShiftResponse(false, request.Simplices, null, result.Output));

        return Ok(new ShiftResponse(true, request.Simplices, result.Output.Trim(), null));
    }
}

public record ShiftRequest(string Simplices, string Ordering);
public record ShiftResponse(bool Success, string Input, string? Output, string? Error);
