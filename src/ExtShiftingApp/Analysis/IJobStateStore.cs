namespace ExtShiftingApp.Analysis;

public interface IJobStateStore
{
    void Save(JobState state);
    JobState? TryLoad();
}
