using System.Text.RegularExpressions;

namespace ExtShiftingApp.Shift;

public static class SimplexParser
{
    public static ParseResult Parse(string input)
    {
        input = input.Trim();

        if (string.IsNullOrEmpty(input))
            return ParseResult.Failure("Input is empty.");

        // Must be wrapped in outer braces: {{ ... }}
        if (!input.StartsWith('{') || !input.EndsWith('}'))
            return ParseResult.Failure("Input must be wrapped in outer braces, e.g. {{1,2},{1,3}}.");

        // Extract inner content between the outer { }
        var inner = input[1..^1].Trim();

        // Find each simplex: {n,n,...}
        var simplexPattern = new Regex(@"\{([^{}]*)\}");
        var matches = simplexPattern.Matches(inner);

        if (matches.Count == 0)
            return ParseResult.Failure("No simplices found. Input must contain at least one simplex, e.g. {{1,2}}.");

        var simplices = new List<IReadOnlyList<int>>();

        foreach (Match match in matches)
        {
            var content = match.Groups[1].Value.Trim();

            if (string.IsNullOrEmpty(content))
                return ParseResult.Failure("A simplex cannot be empty.");

            var parts = content.Split(',');
            var vertices = new List<int>();

            foreach (var part in parts)
            {
                if (!int.TryParse(part.Trim(), out int vertex))
                    return ParseResult.Failure($"'{part.Trim()}' is not a valid vertex — vertices must be non-negative integers.");

                if (vertex < 0)
                    return ParseResult.Failure($"Vertex '{vertex}' is negative — vertices must be non-negative integers.");

                vertices.Add(vertex);
            }

            if (vertices.Count != vertices.Distinct().Count())
                return ParseResult.Failure($"A simplex contains duplicate vertices: {{{string.Join(",", vertices)}}}.");

            simplices.Add(vertices);
        }

        // All simplices must have the same dimension
        var dimension = simplices[0].Count;
        if (simplices.Any(s => s.Count != dimension))
            return ParseResult.Failure("All simplices must have the same dimension (same number of vertices).");

        return ParseResult.Success(simplices);
    }
}
