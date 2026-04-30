using System.Threading.Channels;

namespace ExtShiftingApp.Analysis;

public interface IAnalysisJob
{
    Task StartAsync(string runName, string inputFilePath, BatchParameters? batch = null);
    Task ResumeAsync(string runName, BatchParameters? batch = null);
    Task StopAsync();
    JobSnapshot GetSnapshot();
    bool RunExists(string runName);
    Task WaitAsync(CancellationToken ct = default);
    ChannelReader<string> OpenOutputStream();
}

public record JobSnapshot(
    JobStatus Status,
    string? RunName,
    int PendingCount,
    int DoneCount,
    int? CurrentItemDepth,
    string? Error);
