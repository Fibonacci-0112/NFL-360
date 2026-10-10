using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NflCoverage.Modules.Stats.Contracts;
using NflCoverage.Modules.Stats.Persistence;
using NflCoverage.SharedKernel;

namespace NflCoverage.Modules.Stats;

/// <summary>Player and team box-score statistics (schema <c>stats</c>), filled by the Ingestion module.</summary>
public sealed class StatsModule : IModule
{
    public string Name => "Stats";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<StatsDbContext>(options => options.UseNpgsql(
            configuration.GetConnectionString("nfl"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", StatsDbContext.Schema)));
        services.AddScoped<IStatsQueries, StatsQueries>();
        services.AddScoped<IStatsIngestion, StatsIngestion>();
        services.AddScoped<IModuleDatabase, StatsDatabase>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api").WithTags(Name);
        api.MapGet("/stats/seasons", (IStatsQueries stats, CancellationToken ct) => stats.GetSeasonsAsync(ct));
        api.MapGet("/stats/{season:int}/leaders/{category}", async (int season, string category, string? type, int? top, IStatsQueries stats, CancellationToken ct) =>
            Enum.TryParse<LeaderCategory>(category, ignoreCase: true, out var parsed)
                ? Results.Ok(await stats.GetLeadersAsync(season, parsed, SeasonTypes.Parse(type), top ?? 50, ct))
                : Results.NotFound());
        api.MapGet("/stats/{season:int}/teams", (int season, string? type, IStatsQueries stats, CancellationToken ct) =>
            stats.GetTeamComparisonAsync(season, SeasonTypes.Parse(type), ct));

        api.MapGet("/players", (string? q, IStatsQueries stats, CancellationToken ct) => stats.SearchPlayersAsync(q ?? "", cancellationToken: ct));
        api.MapGet("/players/{id}", async (string id, IStatsQueries stats, CancellationToken ct) =>
            await stats.GetPlayerAsync(id, ct) is { } player ? Results.Ok(player) : Results.NotFound());
        api.MapGet("/players/{id}/career", (string id, IStatsQueries stats, CancellationToken ct) => stats.GetPlayerCareerAsync(id, ct));
        api.MapGet("/players/{id}/games/{season:int}", (string id, int season, IStatsQueries stats, CancellationToken ct) =>
            stats.GetPlayerGameLogAsync(id, season, ct));

        api.MapGet("/teams/{team}/stats/{season:int}", async (string team, int season, string? type, IStatsQueries stats, CancellationToken ct) =>
            await stats.GetTeamSeasonAsync(team.ToUpperInvariant(), season, SeasonTypes.Parse(type), ct) is { } row ? Results.Ok(row) : Results.NotFound());
        api.MapGet("/teams/{team}/stats/{season:int}/games", (string team, int season, IStatsQueries stats, CancellationToken ct) =>
            stats.GetTeamGamesAsync(team.ToUpperInvariant(), season, ct));
        api.MapGet("/teams/{team}/stats/{season:int}/efficiency", (string team, int season, IStatsQueries stats, CancellationToken ct) =>
            stats.GetTeamGameEfficiencyAsync(team.ToUpperInvariant(), season, ct));
        api.MapGet("/teams/{team}/stats/{season:int}/leaders", (string team, int season, IStatsQueries stats, CancellationToken ct) =>
            stats.GetTeamRosterLeadersAsync(team.ToUpperInvariant(), season, ct));
    }

    private sealed class StatsDatabase(StatsDbContext db) : IModuleDatabase
    {
        public string Schema => StatsDbContext.Schema;

        public Task MigrateAsync(CancellationToken cancellationToken) => db.Database.MigrateAsync(cancellationToken);
    }
}
