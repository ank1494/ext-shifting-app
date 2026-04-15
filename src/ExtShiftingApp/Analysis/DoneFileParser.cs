using System.Text.RegularExpressions;

namespace ExtShiftingApp.Analysis;

public static class DoneFileParser
{
    private static readonly Regex SeqRe = new(@"""seq""\s*=>\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex ParentRe = new(@"""parent""\s*=>\s*""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex DepthRe = new(@"""depth""\s*=>\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex TriangulationRe = new(@"""triangulation""\s*=>\s*(.+),\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex SplitFromRe = new(@"""splitFrom""\s*=>\s*new HashTable from \{""vertex""\s*=>\s*(\d+),\s*""neighbors""\s*=>\s*(\{[0-9,]+\})\}", RegexOptions.Compiled);
    private static readonly Regex CritRegionRe = new(@"new HashTable from \{""innerVertexCount""\s*=>\s*(\d+),\s*""boundaryVertexCount""\s*=>\s*(\d+),\s*""regionShape""\s*=>\s*""([^""]+)""\}", RegexOptions.Compiled);
    private static readonly Regex VertexRe = new(@"\d+", RegexOptions.Compiled);

    public static DoneItem Parse(string text)
    {
        var seq = int.Parse(SeqRe.Match(text).Groups[1].Value);
        var parent = ParentRe.Match(text).Groups[1].Value;
        var depth = int.Parse(DepthRe.Match(text).Groups[1].Value);

        var triMatch = TriangulationRe.Match(text);
        var triangulation = triMatch.Groups[1].Value.Trim();

        SplitFromData? splitFrom = null;
        var splitMatch = SplitFromRe.Match(text);
        if (splitMatch.Success)
            splitFrom = new SplitFromData(int.Parse(splitMatch.Groups[1].Value), splitMatch.Groups[2].Value);

        var critRegions = CritRegionRe.Matches(text)
            .Select(m => new CritRegion(
                m.Groups[3].Value,
                int.Parse(m.Groups[2].Value),
                int.Parse(m.Groups[1].Value)))
            .ToList();

        var vertexCount = VertexRe.Matches(triangulation)
            .Select(m => m.Value)
            .Distinct()
            .Count();

        return new DoneItem(seq, parent, depth, triangulation, vertexCount, critRegions, splitFrom);
    }
}
