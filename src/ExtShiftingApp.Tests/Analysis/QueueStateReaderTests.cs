using ExtShiftingApp.Analysis;

namespace ExtShiftingApp.Tests.Analysis;

public class QueueStateReaderTests : IDisposable
{
    private readonly string _runDir = Path.Combine(Path.GetTempPath(), $"queue_test_{Guid.NewGuid():N}");
    private readonly string _pendingDir;
    private readonly string _doneDir;

    public QueueStateReaderTests()
    {
        _pendingDir = Path.Combine(_runDir, "pending");
        _doneDir = Path.Combine(_runDir, "done");
        Directory.CreateDirectory(_pendingDir);
        Directory.CreateDirectory(_doneDir);
    }

    public void Dispose() => Directory.Delete(_runDir, recursive: true);

    private void WritePendingItem(string name, int depth) =>
        File.WriteAllText(Path.Combine(_pendingDir, name),
            $"new HashTable from {{\n  \"parent\" => \"seed\",\n  \"depth\" => {depth},\n  \"seq\" => 1,\n  \"triangulation\" => {{}}\n}}");

    private void WriteDoneItem(string name) =>
        File.WriteAllText(Path.Combine(_doneDir, name), "done");

    [Fact]
    public void Read_CountsPendingAndDoneFiles()
    {
        WritePendingItem("0001", depth: 0);
        WritePendingItem("0002", depth: 0);
        WriteDoneItem("0000");

        var state = new QueueStateReader().Read(_runDir);

        Assert.Equal(2, state.PendingCount);
        Assert.Equal(1, state.DoneCount);
    }

    [Fact]
    public void Read_CurrentItemDepth_ReadsFromFrontOfPending()
    {
        WritePendingItem("0002", depth: 3);
        WritePendingItem("0005", depth: 7);

        var state = new QueueStateReader().Read(_runDir);

        Assert.Equal(3, state.CurrentItemDepth);
    }

    [Fact]
    public void Read_CurrentItemDepth_NullWhenPendingEmpty()
    {
        WriteDoneItem("0001");

        var state = new QueueStateReader().Read(_runDir);

        Assert.Null(state.CurrentItemDepth);
    }

    [Fact]
    public void Read_ReturnsZeroCounts_WhenDirectoriesMissing()
    {
        // runDir exists but pending/ and done/ do not
        var emptyRunDir = Path.Combine(Path.GetTempPath(), $"empty_{Guid.NewGuid():N}");
        Directory.CreateDirectory(emptyRunDir);
        try
        {
            var state = new QueueStateReader().Read(emptyRunDir);
            Assert.Equal(0, state.PendingCount);
            Assert.Equal(0, state.DoneCount);
            Assert.Null(state.CurrentItemDepth);
        }
        finally
        {
            Directory.Delete(emptyRunDir);
        }
    }
}
