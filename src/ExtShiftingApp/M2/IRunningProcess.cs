namespace ExtShiftingApp.M2;

public interface IRunningProcess
{
    event EventHandler<string> OutputReceived;
    event EventHandler<string> ErrorReceived;
    Task WaitForExitAsync(CancellationToken ct);
    int ExitCode { get; }
    void Kill();
}
