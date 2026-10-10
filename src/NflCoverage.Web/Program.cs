using MudBlazor.Services;
using NflCoverage.Modules.Ingestion;
using NflCoverage.Modules.League;
using NflCoverage.Modules.PlayoffOdds;
using NflCoverage.Modules.Standings;
using NflCoverage.SharedKernel;
using NflCoverage.Web;
using NflCoverage.Web.Components;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// The modular monolith: every module is registered explicitly; modules only see each other's Contracts.
IModule[] modules = [new LeagueModule(), new StandingsModule(), new PlayoffOddsModule(), new IngestionModule()];
foreach (var module in modules)
{
    module.AddServices(builder.Services, builder.Configuration);
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddInProcessEventBus();
builder.Services.AddMudServices();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", defaultValue: true))
{
    await using var scope = app.Services.CreateAsyncScope();
    foreach (var database in scope.ServiceProvider.GetServices<IModuleDatabase>())
    {
        await database.MigrateAsync(CancellationToken.None);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapDefaultEndpoints();
foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.MapWhatIfEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(NflCoverage.Web.Client._Imports).Assembly);

await app.RunAsync();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;
