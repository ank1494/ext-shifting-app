using ExtShiftingApp.M2;

namespace ExtShiftingApp.Analysis;

public class AnalysisJobManager(M2ProcessRunner m2, string m2RepoPath, string outputPath)
{
    private const string ConvergedMarker = "no more splits to calculate";

    private JobState _state = JobState.Initial;
    private CancellationTokenSource? _cts;
    private Task _runTask = Task.CompletedTask;
    private readonly List<string> _outputLog = [];
    private readonly List<EventHandler<string>> _subscribers = [];

    public JobState GetState() => _state;

    public IReadOnlyList<string> GetOutputLog() => _outputLog;

    public void Subscribe(EventHandler<string> handler) => _subscribers.Add(handler);
    public void Unsubscribe(EventHandler<string> handler) => _subscribers.Remove(handler);

    public void Start(string runName, string inputFilePath)
    {
        if (_state.Status == JobStatus.Running)
            throw new InvalidOperationException("A job is already running.");

        _outputLog.Clear();
        _cts = new CancellationTokenSource();
        _state = JobState.Initial with { RunName = runName, Status = JobStatus.Running };
        PersistState();
        _runTask = RunLoopAsync(runName, inputFilePath, _cts.Token);
    }

    public void Stop() => _cts?.Cancel();

    internal Task WaitAsync() => _runTask;

    private async Task RunLoopAsync(string runName, string inputFilePath, CancellationToken ct)
    {
        try
        {
            var scriptPath = Path.Combine(m2RepoPath, "analyze triangs.m2");
            bool converged = false;

            while (!converged && !ct.IsCancellationRequested)
            {
                _state = _state with { CurrentIteration = _state.CurrentIteration + 1 };
                WriteConfig(runName, inputFilePath);

                var result = await m2.RunScriptAsync(scriptPath, onOutput: Broadcast, ct: ct);

                if (!result.Success)
                {
                    _state = _state with { Status = JobStatus.Failed, Error = result.Output };
                    PersistState();
                    return;
                }

                converged = result.Output.Contains(ConvergedMarker);
            }

            _state = _state with { Status = ct.IsCancellationRequested ? JobStatus.Idle : JobStatus.Complete };
            PersistState();
        }
        catch (OperationCanceledException)
        {
            _state = _state with { Status = JobStatus.Idle };
            PersistState();
        }
        catch (Exception ex)
        {
            _state = _state with { Status = JobStatus.Failed, Error = ex.Message };
            PersistState();
        }
    }

    private void Broadcast(string line)
    {
        _outputLog.Add(line);
        foreach (var handler in _subscribers)
            handler(this, line);
    }

    private void WriteConfig(string runName, string inputFilePath)
    {
        var config = $"""
            analysisName = "{runName}"
            analysisInputFile = "{inputFilePath}"
            """;
        File.WriteAllText(Path.Combine(m2RepoPath, "analysis config.m2"), config);
    }

    private void PersistState()
    {
        var dir = Path.Combine(outputPath, _state.RunName ?? "unknown");
        Directory.CreateDirectory(dir);
        var json = System.Text.Json.JsonSerializer.Serialize(_state);
        File.WriteAllText(Path.Combine(dir, "job_state.json"), json);
    }
}
