using ExtShiftingApp.M2;

namespace ExtShiftingApp.Analysis;

public class AnalysisJobManager(M2ProcessRunner m2, string m2RepoPath, string outputPath,
    TimeSpan? pollingInterval = null, TimeSpan? graceTimeout = null)
{
    private readonly TimeSpan _pollingInterval = pollingInterval ?? TimeSpan.FromSeconds(60);
    private readonly TimeSpan _graceTimeout    = graceTimeout    ?? TimeSpan.FromSeconds(30);
    private JobState _state = TryLoadPersistedState(outputPath) ?? JobState.Initial;
    private CancellationTokenSource? _cts;
    private Task _runTask = Task.CompletedTask;
    private readonly object _lock = new();
    private readonly List<string> _outputLog = [];
    private readonly List<EventHandler<string>> _subscribers = [];
    private readonly QueueStateReader _queueReader = new();
    private QueueState? _lastPolledState;
    private bool _runPausedSeen;
    private bool _stopRequested;

    public JobState GetState()
    {
        if (_state.Status == JobStatus.Running && _state.RunName is not null)
        {
            var runDir = Path.Combine(outputPath, _state.RunName);
            var live = _queueReader.Read(runDir);
            return _state with
            {
                PendingCount     = live.PendingCount,
                DoneCount        = live.DoneCount,
                CurrentItemDepth = live.CurrentItemDepth,
            };
        }
        return _state;
    }

    public IReadOnlyList<string> GetOutputLog() => _outputLog;

    public void Subscribe(EventHandler<string> handler) => _subscribers.Add(handler);
    public void Unsubscribe(EventHandler<string> handler) => _subscribers.Remove(handler);

    public bool RunExists(string runName) =>
        Directory.Exists(Path.Combine(outputPath, runName));

    public void Start(string runName, string inputFilePath, BatchParameters? batch = null)
    {
        Task priorTask;
        lock (_lock)
        {
            if (_state.Status == JobStatus.Running &&
                (_cts == null || (!_cts.IsCancellationRequested && !_stopRequested)))
                throw new InvalidOperationException("A job is already running.");
            priorTask = _runTask;
        }

        // Drain any in-flight teardown from a prior Stop() before mutating shared state
        priorTask.GetAwaiter().GetResult();

        lock (_lock)
        {
            if (_state.Status == JobStatus.Running)
                throw new InvalidOperationException("A job is already running.");
            _outputLog.Clear();
            _runPausedSeen = false;
            _stopRequested = false;
            _lastPolledState = null;
            _cts = new CancellationTokenSource();
            _state = JobState.Initial with { RunName = runName, Status = JobStatus.Running };
        }

        PersistState();
        _runTask = RunQueueAsync(runName, inputFilePath, batch ?? new BatchParameters(), _cts.Token);
    }

    /// <summary>
    /// Signals a graceful stop by writing a <c>stop_requested</c> file in the run directory.
    /// M2's runQueue loop will detect the file, finish the current item, and exit cleanly.
    /// A hard CTS cancellation fires after <c>_graceTimeout</c> as a safety fallback.
    /// </summary>
    public void Stop()
    {
        var runName = _state.RunName;
        if (runName is null) return;

        _stopRequested = true;

        var stopSignalPath = Path.Combine(outputPath, runName, "stop_requested");
        try { File.WriteAllText(stopSignalPath, ""); }
        catch { /* run dir may not exist yet; grace-kill fallback handles it */ }

        // Fallback hard kill after grace period — guards against M2 hanging indefinitely
        var capturedCts = _cts;
        _ = Task.Delay(_graceTimeout).ContinueWith(_ =>
        {
            if (_cts == capturedCts) capturedCts?.Cancel();
        });
    }

    /// <summary>
    /// Resumes a paused run. Exposed for testing via SetPausedState.
    /// </summary>
    public void Resume(string runName, BatchParameters? batch = null)
    {
        if (_state.Status != JobStatus.Paused)
            throw new InvalidOperationException("Job is not paused.");

        _outputLog.Clear();
        _runPausedSeen = false;
        _stopRequested = false;
        _lastPolledState = null;
        _cts = new CancellationTokenSource();
        _state = _state with { RunName = runName, Status = JobStatus.Running };
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
        // dueTime = TimeSpan.Zero: first poll fires immediately so SSE clients see counts right away
        using var pollTimer = new System.Threading.Timer(_ => Poll(), null, TimeSpan.Zero, _pollingInterval);
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
                _state = _state with { Status = ct.IsCancellationRequested || _runPausedSeen ? JobStatus.Paused : JobStatus.Complete };
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
        finally
        {
            pollTimer.Change(Timeout.Infinite, Timeout.Infinite);
            PersistState();
        }
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
        if (line.Contains("\"type\":\"run_paused\""))
            _runPausedSeen = true;
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
        var itemCap  = b.ItemCap.HasValue        ? b.ItemCap.Value.ToString()                     : "null";
        var maxVerts = b.MaxVertexCount.HasValue  ? b.MaxVertexCount.Value.ToString()              : "null";
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

    private static JobState? TryLoadPersistedState(string outputPath)
    {
        if (!Directory.Exists(outputPath)) return null;

        JobState? best = null;
        DateTime bestModified = DateTime.MinValue;

        foreach (var file in Directory.EnumerateFiles(outputPath, "job_state.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file);
                var state = System.Text.Json.JsonSerializer.Deserialize<JobState>(json);
                if (state is null) continue;
                if (state.Status != JobStatus.Paused && state.Status != JobStatus.Failed) continue;

                var modified = File.GetLastWriteTimeUtc(file);
                if (modified > bestModified) { best = state; bestModified = modified; }
            }
            catch { /* skip unreadable files */ }
        }

        return best;
    }
}
