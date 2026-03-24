using ExtShiftingApp.M2;

var builder = WebApplication.CreateBuilder(args);

var m2RepoPath = builder.Configuration["M2_REPO_PATH"] ?? "/m2/ext-shifting";
builder.Services.AddSingleton<IProcessFactory, SystemProcessFactory>();
builder.Services.AddSingleton(_ => new M2ProcessRunner(
    new SystemProcessFactory(),
    workingDirectory: m2RepoPath));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Needed for test project access
public partial class Program { }
