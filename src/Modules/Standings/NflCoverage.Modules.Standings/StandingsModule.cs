using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NflCoverage.Modules.League.Contracts;
using NflCoverage.Modules.Standings.Contracts;
using NflCoverage.Rules;
using NflCoverage.SharedKernel;

namespace NflCoverage.Modules.Standings;

/// <summary>
/// Standings are a pure function of league results, so they are computed on demand by the rules engine and
/// cached per season, instead of being persisted. A results change bumps the season's cache version.
/// </summary>
public sealed class StandingsModule : IModule
{
    public string Name => "Standings";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddSingleton<SeasonVersions>();
        services.AddScoped<IStandingsQueries, StandingsQueries>();
        services.AddScoped<IHandles<GameResultsChanged>, InvalidateOnResultsChanged>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(
                "/api/seasons/{season:int}/standings",
                async (int season, int? week, IStandingsQueries standings, CancellationToken ct) =>
                    await standings.GetAsync(season, week, ct) is { } result ? Results.Ok(result) : Results.NotFound())
            .WithTags(Name);

    internal sealed class SeasonVersions
    {
        private readonly ConcurrentDictionary<int, int> _versions = new();

        public int Get(int season) => _versions.GetValueOrDefault(season);

        public void Bump(int season) => _versions.AddOrUpdate(season, 1, (_, v) => v + 1);
    }

    private sealed class InvalidateOnResultsChanged(SeasonVersions versions) : IHandles<GameResultsChanged>
    {
        public Task HandleAsync(GameResultsChanged @event, CancellationToken cancellationToken)
        {
            versions.Bump(@event.Season);
            return Task.CompletedTask;
        }
    }
}

internal sealed class StandingsQueries(ILeagueQueries league, IMemoryCache cache, StandingsModule.SeasonVersions versions)
    : IStandingsQueries
{
    public async Task<SeasonStandings?> GetAsync(int season, int? throughWeek = null, CancellationToken cancellationToken = default)
    {
        if (season < IStandingsQueries.FirstSupportedSeason)
        {
            return null;
        }

        var key = (nameof(SeasonStandings), season, throughWeek, versions.Get(season));
        return await cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await ComputeAsync(season, throughWeek, cancellationToken);
        });
    }

    private async Task<SeasonStandings?> ComputeAsync(int season, int? throughWeek, CancellationToken cancellationToken)
    {
        var games = (await league.GetGamesAsync(season, cancellationToken)).Where(g => g.GameType == GameTypes.Regular).ToList();
        if (games.Count == 0)
        {
            return null;
        }

        var teams = await league.GetTeamsAsync(cancellationToken);
        var finals = games.Where(g => g.Status == GameStatus.Final).ToList();
        var lastPlayedWeek = finals.Count == 0 ? 0 : finals.Max(g => g.Week);
        var week = Math.Clamp(throughWeek ?? lastPlayedWeek, 0, games.Max(g => g.Week));

        var computed = StandingsCalculator.Compute(
            LeagueAlignment.Create(teams),
            finals.Where(g => g.Week <= week).Select(g => new GameScore(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value)),
            StandingsCalculator.PlayoffTeamsPerConference(season));

        return new SeasonStandings(
            season,
            week,
            lastPlayedWeek,
            [.. computed.Teams.Select(t => new StandingRow(
                t.TeamId, t.Conference.ToString().ToUpperInvariant(), t.Division, t.Wins, t.Losses, t.Ties, t.WinPct,
                t.PointsFor, t.PointsAgainst, t.DivisionRecord, t.ConferenceRecord, t.DivisionRank, t.ConferenceRank, t.Seed))],
            [.. computed.Notes.Select(n => new TiebreakExplanation(n.Context, n.TiedTeams, n.Winner, n.DecidedBy))]);
    }
}

/// <summary>Maps League team records onto the rules engine's alignment model.</summary>
public static class LeagueAlignment
{
    public static Rules.League Create(IEnumerable<TeamDto> teams) =>
        Rules.League.Create(teams.Select(t => new Team(
            t.Id,
            t.Conference == "AFC" ? Conference.Afc : Conference.Nfc,
            t.Division)));
}
