namespace ExtShiftingApp.M2;

public interface IProcessFactory
{
    IRunningProcess Start(string executable, string arguments, string workingDirectory);
}
