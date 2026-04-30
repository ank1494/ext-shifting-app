using ExtShiftingApp.Analysis;
using ExtShiftingApp.Files;
using ExtShiftingApp.M2;
using ExtShiftingApp.Repl;

var builder = WebApplication.CreateBuilder(args);

var m2RepoPath = builder.Configuration["M2_REPO_PATH"] ?? "/m2/ext-shifting";
builder.Services.AddSingleton<IProcessFactory, SystemProcessFactory>();
builder.Services.AddSingleton<IM2Runner>(sp => new M2ProcessRunner(
    new SystemProcessFactory(),
    workingDirectory: m2RepoPath));
var outputPath = builder.Configuration["OUTPUT_PATH"] ?? "/output";
builder.Services.AddSingleton<IJobStateStore>(new FileJobStateStore(outputPath));
builder.Services.AddSingleton<IQueueStateReader, QueueStateReader>();
builder.Services.AddSingleton<IAnalysisJob>(sp => new AnalysisJob(
    sp.GetRequiredService<IM2Runner>(),
    sp.GetRequiredService<IJobStateStore>(),
    sp.GetRequiredService<IQueueStateReader>(),
    outputPath,
    m2RepoPath));
builder.Services.AddSingleton(new FileSystemService(m2RepoPath));
builder.Services.AddSingleton(m2RepoPath);
builder.Services.AddSingleton(new OutputPath(outputPath));
builder.Services.AddSingleton<DoneFileReader>();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapRepl();

app.Run();

// Needed for test project access
public partial class Program { }
