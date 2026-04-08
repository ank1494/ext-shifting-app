using ExtShiftingApp.M2;

namespace ExtShiftingApp.Analysis;

public class AnalysisJobManager(M2ProcessRunner m2, string m2RepoPath, string outputPath)
{
    private JobState _state = JobState.Initial;
    private CancellationTokenSource? _cts;
    private Task _runTask = Task.CompletedTask;
    private readonly List<string> _outputLog = [];
    private readonly List<EventHandler<string>> _subscribers = [];
    private readonly QueueStateReader _queueReader = new();
    private QueueState? _lastPolledState;

    public JobState GetState() => _state;

    public IReadOnlyList<string> GetOutputLog() => _outputLog;

    public void Subscribe(EventHandler<string> handler) => _subscribers.Add(handler);
    public void Unsubscribe(EventHandler<string> handler) => _subscribers.Remove(handler);

    public bool RunExists(string runName) =>
        Directory.Exists(Path.Combine(outputPath, runName));

    public void Start(string runName, string inputFilePath, BatchParameters? batch = null)
    {
        if (_state.Status == JobStatus.Running)
            throw new InvalidOperationException("A job is already running.");

        _outputLog.Clear();
        _cts = new CancellationTokenSource();
        _state = JobState.Initial with { RunName = runName, Status = JobStatus.Running };
        PersistState();
        _runTask = RunQueueAsync(runName, inputFilePath, batch ?? new BatchParameters(), _cts.Token);
    }

    public void Stop() => _cts?.Cancel();

    /// <summary>
    /// Resumes a paused run. Exposed for testing via SetPausedState.
    /// </summary>
    public void Resume(string runName, BatchParameters? batch = null)
    {
        if (_state.Status != JobStatus.Paused)
            throw new InvalidOperationException("Job is not paused.");

        _cts = new CancellationTokenSource();
        _state = _state with { Status = JobStatus.Running };
        PersistState();
        _runTask = RunQueueAsync(runName, inputFilePath: null, batch ?? new BatchParameters(), _cts.Token);
    }

    /// <summary>Test seam: puts the manager into Paused state for a named run.</summary>
    public void SetPausedState(string runName) =>
        _state = _state with { RunName = runName, Status = JobStatus.Paused };

    /// <summary>Test seam: fires the polling logic immediately (bypasses the 1-minute timer).</summary>
    public void FirePollForTest() => Poll();

    public Task WaitAsync() => _runTask;

    private async Task RunQueueAsync(string runName, string? inputFilePath, BatchParameters batch, CancellationToken ct)
    {
        try
        {
            var scriptPath = Path.Combine(m2RepoPath, "scripts", "runQueue.m2");
            var configPath = WriteConfig(runName, inputFilePath);
            var batchArgs  = FormatBatchArgs(batch);

            var result = await m2.RunScriptAsync(
                scriptPath,
                onOutput: Broadcast,
                ct: ct,
                scriptArgs: $"\"{configPath}\" {batchArgs}");

            if (result.ExitCode == 0)
                _state = _state with { Status = ct.IsCancellationRequested ? JobStatus.Paused : JobStatus.Complete };
            else
                _state = _state with { Status = JobStatus.Failed, Error = result.Output };
        }
        catch (OperationCanceledException)
        {
            _state = _state with { Status = JobStatus.Paused };
        }
        catch (Exception ex)
        {
            _state = _state with { Status = JobStatus.Failed, Error = ex.Message };
        }
        PersistState();
    }

    private void Poll()
    {
        if (_state.RunName is null) return;
        var runDir = Path.Combine(outputPath, _state.RunName);
        var current = _queueReader.Read(runDir);
        if (_lastPolledState is not null &&
            current.PendingCount == _lastPolledState.PendingCount &&
            current.DoneCount    == _lastPolledState.DoneCount)
            return;

        _lastPolledState = current;
        _state = _state with { PendingCount = current.PendingCount, DoneCount = current.DoneCount, CurrentItemDepth = current.CurrentItemDepth };
        Broadcast($"EVENT:{{\"type\":\"queue_state\",\"pendingCount\":{current.PendingCount},\"doneCount\":{current.DoneCount}}}");
    }

    private void Broadcast(string line)
    {
        _outputLog.Add(line);
        foreach (var handler in _subscribers)
            handler(this, line);
    }

    private string WriteConfig(string runName, string? inputFilePath)
    {
        var runDir = Path.Combine(outputPath, runName);
        Directory.CreateDirectory(runDir);
        var configPath = Path.Combine(runDir, "analysis config.m2");
        var inputLine = inputFilePath is not null
            ? $"\nanalysisInputFile = \"{inputFilePath}\""
            : "";
        File.WriteAllText(configPath, $"analysisName = \"{runName}\"\nanalysisOutputDir = \"{runDir}\"{inputLine}");
        return configPath;
    }

    private static string FormatBatchArgs(BatchParameters b)
    {
        var itemCap  = b.ItemCap.HasValue        ? b.ItemCap.Value.ToString()                : "null";
        var maxVerts = b.MaxVertexCount.HasValue  ? b.MaxVertexCount.Value.ToString()         : "null";
        var timeout  = b.Timeout.HasValue         ? ((int)b.Timeout.Value.TotalSeconds).ToString() : "null";
        return $"{itemCap} {maxVerts} {timeout}";
    }

    private void PersistState()
    {
        var dir = Path.Combine(outputPath, _state.RunName ?? "unknown");
        Directory.CreateDirectory(dir);
        var json = System.Text.Json.JsonSerializer.Serialize(_state);
        File.WriteAllText(Path.Combine(dir, "job_state.json"), json);
    }
}
