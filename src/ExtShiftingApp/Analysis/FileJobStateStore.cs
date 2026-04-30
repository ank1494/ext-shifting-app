using System.Text.Json;

namespace ExtShiftingApp.Analysis;

public class FileJobStateStore(string outputPath) : IJobStateStore
{
    public void Save(JobState state)
    {
        var dir = Path.Combine(outputPath, state.RunName ?? "unknown");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "job_state.json"), JsonSerializer.Serialize(state));
    }

    public JobState? TryLoad()
    {
        if (!Directory.Exists(outputPath)) return null;

        JobState? best = null;
        DateTime bestModified = DateTime.MinValue;

        foreach (var file in Directory.EnumerateFiles(outputPath, "job_state.json", SearchOption.AllDirectories))
        {
            try
            {
                var state = JsonSerializer.Deserialize<JobState>(File.ReadAllText(file));
                if (state is null) continue;
                if (state.Status != JobStatus.Paused && state.Status != JobStatus.Failed) continue;

                var modified = File.GetLastWriteTimeUtc(file);
                if (modified > bestModified) { best = state; bestModified = modified; }
            }
            catch { }
        }

        return best;
    }
}
