namespace ExtShiftingApp.M2;

public interface IRunningProcess
{
    event EventHandler<string> OutputReceived;
    event EventHandler<string> ErrorReceived;
    Task WaitForExitAsync(CancellationToken ct);
    Task SendInputAsync(string line, CancellationToken ct = default);
    int ExitCode { get; }
    void Kill();
}
