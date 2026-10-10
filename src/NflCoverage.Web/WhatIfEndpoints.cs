using NflCoverage.Modules.League.Contracts;
using NflCoverage.Modules.PlayoffOdds.Contracts;
using NflCoverage.Web.Client.Models;

namespace NflCoverage.Web;

/// <summary>
/// Backend-for-frontend endpoint: composes module contracts into the payload the WebAssembly what-if page needs,
/// so the client never depends on server modules.
/// </summary>
internal static class WhatIfEndpoints
{
    public static void MapWhatIfEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/what-if", async (ILeagueQueries league, IPlayoffOddsQueries odds, CancellationToken ct) =>
            (await league.GetSeasonsAsync(ct)) is [var latest, ..] ? await GetAsync(latest, league, odds, ct) : Results.NotFound());
        endpoints.MapGet("/api/what-if/{season:int}", GetAsync);
    }

    private static async Task<IResult> GetAsync(int season, ILeagueQueries league, IPlayoffOddsQueries odds, CancellationToken ct)
    {
        var input = await odds.GetSimulationInputAsync(season, ct);
        if (input is null)
        {
            return Results.NotFound();
        }

        var teams = await league.GetTeamsAsync(ct);
        return Results.Ok(new WhatIfData(
            input,
            [.. teams.Select(t => new TeamInfo(t.Id, t.Name, t.Conference, t.Division, t.PrimaryColor, t.LogoUrl))]));
    }
}
