namespace ExtShiftingApp.Analysis;

public interface IQueueStateReader
{
    QueueState Read(string runDir);
}
