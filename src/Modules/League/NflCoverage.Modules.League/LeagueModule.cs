using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NflCoverage.Modules.League.Contracts;
using NflCoverage.Modules.League.Persistence;
using NflCoverage.SharedKernel;

namespace NflCoverage.Modules.League;

public sealed class LeagueModule : IModule
{
    public string Name => "League";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LeagueDbContext>(options => options.UseNpgsql(
            configuration.GetConnectionString("nfl"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", LeagueDbContext.Schema)));
        services.AddScoped<ILeagueQueries, LeagueQueries>();
        services.AddScoped<ILeagueIngestion, LeagueIngestion>();
        services.AddScoped<IModuleDatabase, LeagueDatabase>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api").WithTags(Name);
        api.MapGet("/teams", (ILeagueQueries league, CancellationToken ct) => league.GetTeamsAsync(ct));
        api.MapGet("/seasons", (ILeagueQueries league, CancellationToken ct) => league.GetSeasonsAsync(ct));
        api.MapGet("/seasons/{season:int}/games", (int season, ILeagueQueries league, CancellationToken ct) => league.GetGamesAsync(season, ct));
    }

    private sealed class LeagueDatabase(LeagueDbContext db) : IModuleDatabase
    {
        public string Schema => LeagueDbContext.Schema;

        public Task MigrateAsync(CancellationToken cancellationToken) => db.Database.MigrateAsync(cancellationToken);
    }
}
