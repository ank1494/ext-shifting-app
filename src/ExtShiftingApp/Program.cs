using ExtShiftingApp.Analysis;
using ExtShiftingApp.Files;
using ExtShiftingApp.M2;
using ExtShiftingApp.Repl;

var builder = WebApplication.CreateBuilder(args);

var m2RepoPath = builder.Configuration["M2_REPO_PATH"] ?? "/m2/ext-shifting";
builder.Services.AddSingleton<IProcessFactory, SystemProcessFactory>();
builder.Services.AddSingleton(_ => new M2ProcessRunner(
    new SystemProcessFactory(),
    workingDirectory: m2RepoPath));
var outputPath = builder.Configuration["OUTPUT_PATH"] ?? "/output";
builder.Services.AddSingleton(new FileSystemService(m2RepoPath));
builder.Services.AddSingleton(sp => new AnalysisJobManager(
    sp.GetRequiredService<M2ProcessRunner>(), m2RepoPath, outputPath));
builder.Services.AddSingleton(m2RepoPath);
builder.Services.AddControllers();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapRepl();

app.Run();

// Needed for test project access
public partial class Program { }
