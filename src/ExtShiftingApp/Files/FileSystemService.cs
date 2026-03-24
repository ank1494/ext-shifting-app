namespace ExtShiftingApp.Files;

public class FileSystemService(string m2RepoPath)
{
    public IReadOnlyList<string> ListM2Files() =>
        Directory.GetFiles(m2RepoPath, "*.m2", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(m2RepoPath, f))
            .Order()
            .ToList();

    public string? ResolveFilePath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(m2RepoPath, relativePath));
        if (!fullPath.StartsWith(m2RepoPath, StringComparison.OrdinalIgnoreCase))
            return null;
        return File.Exists(fullPath) ? fullPath : null;
    }
}
