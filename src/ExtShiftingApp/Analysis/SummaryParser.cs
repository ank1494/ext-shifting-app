namespace ExtShiftingApp.Analysis;

public record IterationSummary(int Iteration, string CriticalRegions, int LargestNonPrefixVertices, bool Converged);

public static class SummaryParser
{
    private const string ConvergedMarker = "CALCULATION FINISHED, NO MORE SPLITS FOR CALCULATION";

    public static IterationSummary Parse(int iteration, string summaryText)
    {
        var critRegions = "";
        var largestVertices = 0;
        var converged = summaryText.Contains(ConvergedMarker);

        foreach (var line in summaryText.Split('\n'))
        {
            if (line.Contains("critical regions were found:"))
            {
                var idx = line.IndexOf('{');
                if (idx >= 0) critRegions = line[idx..].Trim();
            }
            else if (line.Contains("largest triangulation"))
            {
                var parts = line.Split(' ');
                var numIdx = Array.FindIndex(parts, p => p == "had") + 1;
                if (numIdx > 0 && numIdx < parts.Length)
                    int.TryParse(parts[numIdx], out largestVertices);
            }
        }

        return new IterationSummary(iteration, critRegions, largestVertices, converged);
    }
}
