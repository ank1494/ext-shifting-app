namespace ExtShiftingApp.M2;

public class M2InteractiveSession(IRunningProcess process) : IInteractiveSession
{
    public event EventHandler<string> OutputReceived
    {
        add => process.OutputReceived += value;
        remove => process.OutputReceived -= value;
    }

    public Task SendInputAsync(string line, CancellationToken ct = default)
        => process.SendInputAsync(line, ct);

    public ValueTask DisposeAsync()
    {
        process.Kill();
        return ValueTask.CompletedTask;
    }
}
