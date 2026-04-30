namespace ExtShiftingApp.Analysis;

public record QueueState(int PendingCount, int DoneCount, int? CurrentItemDepth);

/// <summary>
/// Reads queue directory state (pending/ and done/ counts and front-item metadata)
/// from a run output directory.
/// </summary>
public class QueueStateReader : IQueueStateReader
{
    public QueueState Read(string runDir)
    {
        var pendingDir = Path.Combine(runDir, "pending");
        var doneDir    = Path.Combine(runDir, "done");

        var pendingFiles = Directory.Exists(pendingDir)
            ? Directory.GetFiles(pendingDir).OrderBy(f => f).ToArray()
            : [];
        var doneCount = Directory.Exists(doneDir)
            ? Directory.GetFiles(doneDir).Length
            : 0;

        int? depth = pendingFiles.Length > 0 ? ReadDepth(pendingFiles[0]) : null;

        return new QueueState(pendingFiles.Length, doneCount, depth);
    }

    private static int? ReadDepth(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("\"depth\"")) continue;
            var parts = trimmed.Split("=>");
            if (parts.Length >= 2 && int.TryParse(parts[1].Trim().TrimEnd(','), out var d))
                return d;
        }
        return null;
    }
}
