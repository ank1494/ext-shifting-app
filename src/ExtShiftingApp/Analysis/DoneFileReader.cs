namespace ExtShiftingApp.Analysis;

public class DoneFileReader
{
    public List<DoneItem> Read(string runDirectory)
    {
        var doneDir = Path.Combine(runDirectory, "done");
        if (!Directory.Exists(doneDir))
            return [];

        return Directory.GetFiles(doneDir)
            .Select(f => DoneFileParser.Parse(File.ReadAllText(f)))
            .OrderBy(item => item.Seq)
            .ToList();
    }
}
