namespace ExtShiftingApp.Analysis;

public enum JobStatus { Idle, Running, Complete, Failed }

public record JobState(
    string? RunName,
    JobStatus Status,
    int CurrentIteration,
    string? Error)
{
    public static readonly JobState Initial = new(null, JobStatus.Idle, 0, null);
}
