using ExtShiftingApp.Analysis;

namespace ExtShiftingApp.Tests.Analysis;

public class DoneFileReaderTests : IDisposable
{
    private readonly string _runDir = Path.Combine(Path.GetTempPath(), $"donereader_{Guid.NewGuid():N}");
    private readonly DoneFileReader _reader = new();

    public DoneFileReaderTests() => Directory.CreateDirectory(_runDir);

    public void Dispose() => Directory.Delete(_runDir, recursive: true);

    private void WriteDoneFile(string filename, int seq, string parent = "seed", int depth = 0, string? splitFromVertex = null, string? splitFromNeighbors = null)
    {
        var doneDir = Path.Combine(_runDir, "done");
        Directory.CreateDirectory(doneDir);

        var splitLine = splitFromVertex != null
            ? $"\n  \"splitFrom\" => new HashTable from {{\"vertex\" => {splitFromVertex}, \"neighbors\" => {splitFromNeighbors}}},"
            : "";

        var content = "new HashTable from {\n" +
            $"  \"parent\" => \"{parent}\",\n" +
            $"  \"depth\" => {depth},\n" +
            $"  \"seq\" => {seq},{splitLine}\n" +
            "  \"triangulation\" => {{0,1,3},{1,3,5}},\n" +
            "  \"critRegions\" => {}\n" +
            "}";
        File.WriteAllText(Path.Combine(doneDir, filename), content);
    }

    [Fact]
    public void Read_MultipleDoneFiles_SortedBySeqAscending()
    {
        WriteDoneFile("0013", seq: 13);
        WriteDoneFile("0006", seq: 6);
        WriteDoneFile("0007", seq: 7);

        var items = _reader.Read(_runDir);

        Assert.Equal(3, items.Count);
        Assert.Equal(6, items[0].Seq);
        Assert.Equal(7, items[1].Seq);
        Assert.Equal(13, items[2].Seq);
    }

    [Fact]
    public void Read_WithAndWithoutSplitFrom_PopulatedAndNull()
    {
        WriteDoneFile("0001", seq: 1, parent: "seed");
        WriteDoneFile("0002", seq: 2, parent: "0001", depth: 1, splitFromVertex: "6", splitFromNeighbors: "{1,3}");

        var items = _reader.Read(_runDir);

        Assert.Null(items.First(i => i.Seq == 1).SplitFrom);
        var split = items.First(i => i.Seq == 2).SplitFrom;
        Assert.NotNull(split);
        Assert.Equal(6, split.Vertex);
        Assert.Equal("{1,3}", split.Neighbors);
    }

    [Fact]
    public void Read_EmptyDoneDir_ReturnsEmptyList()
    {
        Directory.CreateDirectory(Path.Combine(_runDir, "done"));

        var items = _reader.Read(_runDir);

        Assert.Empty(items);
    }

    [Fact]
    public void Read_NoDoneDir_ReturnsEmptyList()
    {
        var items = _reader.Read(_runDir);

        Assert.Empty(items);
    }
}
