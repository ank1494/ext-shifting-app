using System.Text.Json;

namespace ExtShiftingApp.Analysis;

/// <summary>
/// Converts a broadcast line from M2 or the polling timer into an SSE JSON payload.
/// Lines prefixed with "EVENT:" are already typed JSON — the prefix is stripped and
/// the remainder is returned as-is. All other lines are wrapped as m2_output events.
/// </summary>
public static class SseEventParser
{
    private const string EventPrefix = "EVENT:";

    public static string Parse(string line)
    {
        if (line.StartsWith(EventPrefix))
            return line[EventPrefix.Length..];

        return JsonSerializer.Serialize(new { type = "m2_output", line });
    }
}
