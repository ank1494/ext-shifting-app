using System.Threading.Channels;
using ExtShiftingApp.M2;

namespace ExtShiftingApp.Analysis;

public class AnalysisJob : IAnalysisJob
{
    private readonly IM2Runner _m2Runner;
    private readonly IJobStateStore _stateStore;
    private readonly IQueueStateReader _queueStateReader;
    private readonly string _outputPath;
    private readonly string _m2RepoPath;
    private readonly object _lock = new();

    private JobStatus _status;
    private string? _runName;
    private string? _error;
    private Task _runTask = Task.CompletedTask;
    private bool _stopRequested;
    private bool _runPausedSeen;
    private readonly List<string> _replayBuffer = [];
    private readonly List<Channel<string>> _subscribers = [];

    public AnalysisJob(IM2Runner m2Runner, IJobStateStore stateStore, IQueueStateReader queueStateReader,
        string outputPath, string m2RepoPath)
    {
        _m2Runner = m2Runner;
        _stateStore = stateStore;
        _queueStateReader = queueStateReader;
        _outputPath = outputPath;
        _m2RepoPath = m2RepoPath;

        var saved = stateStore.TryLoad();
        if (saved?.Status is JobStatus.Paused or JobStatus.Failed)
        {
            _status = saved.Status;
            _runName = saved.RunName;
            _error = saved.Error;
        }
    }

    public Task StartAsync(string runName, string inputFilePath, BatchParameters? batch = null)
    {
        lock (_lock)
        {
            if (_status == JobStatus.Running)
                throw new InvalidOperationException("A job is already running.");
            _status = JobStatus.Running;
            _runName = runName;
            _stopRequested = false;
            _runPausedSeen = false;
            _replayBuffer.Clear();
        }
        _runTask = RunCoreAsync(runName, inputFilePath, batch ?? new BatchParameters());
        return Task.CompletedTask;
    }

    public async Task ResumeAsync(string runName, BatchParameters? batch = null)
    {
        Task priorTask;
        lock (_lock)
        {
            if (_status != JobStatus.Paused)
                throw new InvalidOperationException("Job is not paused.");
            priorTask = _runTask;
        }

        await priorTask;

        lock (_lock)
        {
            if (_status != JobStatus.Paused)
                throw new InvalidOperationException("Job is not paused.");
            _status = JobStatus.Running;
            _runName = runName;
            _stopRequested = false;
            _runPausedSeen = false;
            _replayBuffer.Clear();
        }
        _stateStore.Save(MakeJobState());
        _runTask = RunCoreAsync(runName, null, batch ?? new BatchParameters());
    }

    public Task StopAsync()
    {
        var runName = _runName;
        if (runName is null) return Task.CompletedTask;

        _stopRequested = true;
        var stopSignalPath = Path.Combine(_outputPath, runName, "stop_requested");
        try { File.WriteAllText(stopSignalPath, ""); }
        catch { }
        return Task.CompletedTask;
    }

    public JobSnapshot GetSnapshot()
    {
        if (_status == JobStatus.Running && _runName is not null)
        {
            var runDir = Path.Combine(_outputPath, _runName);
            var live = _queueStateReader.Read(runDir);
            return new JobSnapshot(_status, _runName, live.PendingCount, live.DoneCount, live.CurrentItemDepth, _error);
        }
        return new JobSnapshot(_status, _runName, 0, 0, null, _error);
    }

    public bool RunExists(string runName) => Directory.Exists(Path.Combine(_outputPath, runName));

    public Task WaitAsync(CancellationToken ct = default) => _runTask.WaitAsync(ct);

    public ChannelReader<string> OpenOutputStream()
    {
        var ch = Channel.CreateUnbounded<string>();
        lock (_lock)
        {
            foreach (var line in _replayBuffer)
                ch.Writer.TryWrite(line);

            if (_status == JobStatus.Running)
                _subscribers.Add(ch);
            else
                ch.Writer.Complete();
        }
        return ch.Reader;
    }

    private void Broadcast(string line)
    {
        lock (_lock)
        {
            if (line.Contains("\"type\":\"run_paused\""))
                _runPausedSeen = true;
            _replayBuffer.Add(line);
            foreach (var ch in _subscribers)
                ch.Writer.TryWrite(line);
        }
    }

    private void CompleteSubscribers()
    {
        lock (_lock)
        {
            foreach (var ch in _subscribers)
                ch.Writer.TryComplete();
            _subscribers.Clear();
        }
    }

    private async Task RunCoreAsync(string runName, string? inputFilePath, BatchParameters batch)
    {
        try
        {
            var scriptPath = Path.Combine(_m2RepoPath, "scripts", "runQueue.m2");
            var configPath = WriteConfig(runName, inputFilePath);
            var batchArgs = FormatBatchArgs(batch);

            var result = await _m2Runner.RunScriptAsync(
                scriptPath,
                onOutput: Broadcast,
                scriptArgs: $"\"{configPath}\" {batchArgs}");

            lock (_lock)
            {
                if (result.ExitCode == 0)
                    _status = _stopRequested || _runPausedSeen ? JobStatus.Paused : JobStatus.Complete;
                else
                    (_status, _error) = (JobStatus.Failed, result.Output);
            }
        }
        catch (OperationCanceledException)
        {
            if (_runName is not null)
            {
                var signal = Path.Combine(_outputPath, _runName, "stop_requested");
                try { File.Delete(signal); } catch { }
            }
            lock (_lock) { _status = JobStatus.Paused; }
        }
        catch (Exception ex)
        {
            lock (_lock) { (_status, _error) = (JobStatus.Failed, ex.Message); }
        }
        finally
        {
            CompleteSubscribers();
            _stateStore.Save(MakeJobState());
        }
    }

    private JobState MakeJobState() => new(_runName, _status, 0, _error);

    private string WriteConfig(string runName, string? inputFilePath)
    {
        var runDir = Path.Combine(_outputPath, runName);
        Directory.CreateDirectory(runDir);
        var configPath = Path.Combine(runDir, "analysis config.m2");
        var inputLine = inputFilePath is not null ? $"\nanalysisInputFile = \"{inputFilePath}\"" : "";
        File.WriteAllText(configPath, $"analysisName = \"{runName}\"\nanalysisOutputDir = \"{runDir}\"{inputLine}");
        return configPath;
    }

    private static string FormatBatchArgs(BatchParameters b)
    {
        var itemCap  = b.ItemCap.HasValue       ? b.ItemCap.Value.ToString()                      : "null";
        var maxVerts = b.MaxVertexCount.HasValue ? b.MaxVertexCount.Value.ToString()               : "null";
        var timeout  = b.Timeout.HasValue        ? ((int)b.Timeout.Value.TotalSeconds).ToString()  : "null";
        return $"{itemCap} {maxVerts} {timeout}";
    }
}
