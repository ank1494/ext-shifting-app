using ExtShiftingApp.Files;

namespace ExtShiftingApp.Tests.Files;

public class FileSystemServiceTests : IDisposable
{
    private readonly string _repoDir = Path.Combine(Path.GetTempPath(), $"m2test_{Guid.NewGuid():N}");

    public FileSystemServiceTests() => Directory.CreateDirectory(_repoDir);
    public void Dispose() => Directory.Delete(_repoDir, recursive: true);

    private void CreateFile(string relativePath, string content = "")
    {
        var fullPath = Path.Combine(_repoDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    [Fact]
    public void ListM2Files_ReturnsOnlyM2Files()
    {
        CreateFile("libs.m2");
        CreateFile("utils.m2");
        CreateFile("README.md");
        CreateFile("notes.txt");

        var svc = new FileSystemService(_repoDir);
        var files = svc.ListM2Files();

        Assert.Equal(2, files.Count);
        Assert.All(files, f => Assert.EndsWith(".m2", f));
    }

    [Fact]
    public void ListM2Files_FindsFilesInSubdirectories()
    {
        CreateFile("libs.m2");
        CreateFile("surface triangulations/tori.m2");

        var svc = new FileSystemService(_repoDir);
        var files = svc.ListM2Files();

        Assert.Equal(2, files.Count);
        Assert.Contains(files, f => f.Contains("tori.m2"));
    }

    [Fact]
    public void ListM2Files_ReturnsRelativePaths()
    {
        CreateFile("libs.m2");

        var svc = new FileSystemService(_repoDir);
        var files = svc.ListM2Files();

        Assert.DoesNotContain(files, f => f.Contains(_repoDir));
    }

    [Fact]
    public void ResolveFilePath_ValidRelativePath_ReturnsFullPath()
    {
        CreateFile("libs.m2");

        var svc = new FileSystemService(_repoDir);
        var resolved = svc.ResolveFilePath("libs.m2");

        Assert.NotNull(resolved);
        Assert.True(File.Exists(resolved));
    }

    [Fact]
    public void ResolveFilePath_PathTraversal_ReturnsNull()
    {
        var svc = new FileSystemService(_repoDir);
        var resolved = svc.ResolveFilePath("../../../etc/passwd");

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveFilePath_NonExistentFile_ReturnsNull()
    {
        var svc = new FileSystemService(_repoDir);
        var resolved = svc.ResolveFilePath("doesnotexist.m2");

        Assert.Null(resolved);
    }
}
