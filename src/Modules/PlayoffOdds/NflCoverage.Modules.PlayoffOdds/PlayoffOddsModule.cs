using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NflCoverage.Modules.League.Contracts;
using NflCoverage.Modules.PlayoffOdds.Contracts;
using NflCoverage.Modules.PlayoffOdds.Persistence;
using NflCoverage.SharedKernel;

namespace NflCoverage.Modules.PlayoffOdds;

public sealed class PlayoffOddsModule : IModule
{
    public string Name => "PlayoffOdds";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(PlayoffOddsOptions.Section);
        services.Configure<PlayoffOddsOptions>(section);
        services.AddMemoryCache();
        services.AddDbContext<OddsDbContext>(options => options.UseNpgsql(
            configuration.GetConnectionString("nfl"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", OddsDbContext.Schema)));
        services.AddScoped<IModuleDatabase, OddsDatabase>();
        services.AddScoped<SimulationInputBuilder>();
        services.AddScoped<IPlayoffOddsQueries, PlayoffOddsQueries>();
        services.AddScoped<IPlayoffOddsCalculator, PlayoffOddsCalculator>();
        services.AddSingleton<RecalculationQueue>();
        services.AddScoped<IHandles<GameResultsChanged>, EnqueueOnResultsChanged>();

        if (section.GetValue(nameof(PlayoffOddsOptions.BackgroundRecalculation), defaultValue: true))
        {
            services.AddHostedService<OddsRecalculationWorker>();
        }
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/seasons/{season:int}/playoff-odds").WithTags(Name);
        api.MapGet("/", async (int season, IPlayoffOddsQueries odds, CancellationToken ct) =>
            await odds.GetLatestAsync(season, ct) is { } snapshot ? Results.Ok(snapshot) : Results.NotFound());
        api.MapGet("/history", (int season, IPlayoffOddsQueries odds, CancellationToken ct) => odds.GetHistoryAsync(season, ct));
        api.MapGet("/simulation-input", async (int season, IPlayoffOddsQueries odds, CancellationToken ct) =>
            await odds.GetSimulationInputAsync(season, ct) is { } input ? Results.Ok(input) : Results.NotFound());
    }

    private sealed class OddsDatabase(OddsDbContext db) : IModuleDatabase
    {
        public string Schema => OddsDbContext.Schema;

        public Task MigrateAsync(CancellationToken cancellationToken) => db.Database.MigrateAsync(cancellationToken);
    }
}
