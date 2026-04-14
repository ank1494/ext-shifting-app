namespace ExtShiftingApp.M2;

/// <summary>
/// WSL-aware M2 runner for test infrastructure. Owns all M2 invocation complexity:
/// reads M2_EXECUTABLE env var, falls back to WSL on Windows, translates paths.
/// </summary>
public class WslAwareM2Runner
{
    private readonly IProcessFactory _processFactory;

    public WslAwareM2Runner() : this(new SystemProcessFactory()) { }

    internal WslAwareM2Runner(IProcessFactory processFactory)
    {
        _processFactory = processFactory;
    }

    private static bool IsWindows => OperatingSystem.IsWindows();

    private string Executable
    {
        get
        {
            var envVar = Environment.GetEnvironmentVariable("M2_EXECUTABLE");
            if (!string.IsNullOrEmpty(envVar)) return envVar;
            return IsWindows ? "wsl.exe" : "M2";
        }
    }

    private bool UseWsl => string.IsNullOrEmpty(Environment.GetEnvironmentVariable("M2_EXECUTABLE")) && IsWindows;

    /// <summary>
    /// Translates a Windows path (C:\...) to a WSL path (/mnt/c/...) when running under WSL.
    /// </summary>
    private static string ToWslPath(string windowsPath)
    {
        if (windowsPath.Length >= 3 && windowsPath[1] == ':')
        {
            var driveLetter = char.ToLowerInvariant(windowsPath[0]);
            var rest = windowsPath[2..].Replace('\\', '/');
            return $"/mnt/{driveLetter}{rest}";
        }
        return windowsPath.Replace('\\', '/');
    }

    private string BuildArguments(string scriptPath)
    {
        if (UseWsl)
        {
            var wslPath = ToWslPath(scriptPath);
            return $"M2 --script \"{wslPath}\"";
        }
        return $"--script \"{scriptPath}\"";
    }

    private string WorkingDirectory(string scriptPath)
    {
        var dir = Path.GetDirectoryName(scriptPath) ?? ".";
        // For WSL runs, working directory is the parent of the script in WSL form
        // but ProcessFactory's working directory is the host path; WSL resolves relative to CWD.
        // We pass the host path and let the shell resolve it.
        return Path.GetDirectoryName(Path.GetFullPath(scriptPath)) ?? ".";
    }

    public async Task<M2Result> RunScriptAsync(string scriptPath)
    {
        const int maxOutputLines = 100;
        var recentLines = new Queue<string>();
        var workDir = WorkingDirectory(scriptPath);
        var process = _processFactory.Start(Executable, BuildArguments(scriptPath), workDir);

        void capture(string line)
        {
            if (recentLines.Count >= maxOutputLines) recentLines.Dequeue();
            recentLines.Enqueue(line);
        }

        process.OutputReceived += (_, line) => capture(line);
        process.ErrorReceived += (_, line) => capture(line);

        await process.WaitForExitAsync(CancellationToken.None);

        return new M2Result(process.ExitCode == 0, string.Join("\n", recentLines), process.ExitCode);
    }

    /// <summary>
    /// Returns false (never throws) when M2 is unreachable. Used as a skip gate in tests.
    /// </summary>
    public bool IsAvailable()
    {
        try
        {
            var exe = Executable;
            var args = UseWsl ? "M2 --version" : "--version";
            var process = _processFactory.Start(exe, args, ".");
            process.WaitForExitAsync(CancellationToken.None).GetAwaiter().GetResult();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
