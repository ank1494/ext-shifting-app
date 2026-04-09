namespace ExtShiftingApp.Analysis;

public enum JobStatus { Idle, Running, Paused, Complete, Failed }

public record BatchParameters(int? ItemCap = null, int? MaxVertexCount = null, TimeSpan? Timeout = null);

public record JobState(
    string? RunName,
    JobStatus Status,
    int CurrentIteration,
    string? Error,
    int PendingCount = 0,
    int DoneCount = 0,
    int? CurrentItemDepth = null)
{
    public static readonly JobState Initial = new(null, JobStatus.Idle, 0, null);
}
