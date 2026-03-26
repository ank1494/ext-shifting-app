namespace ExtShiftingApp.M2;

public interface IInteractiveSession : IDisposable, IAsyncDisposable
{
    event EventHandler<string> OutputReceived;
    Task SendInputAsync(string line, CancellationToken ct = default);
}
