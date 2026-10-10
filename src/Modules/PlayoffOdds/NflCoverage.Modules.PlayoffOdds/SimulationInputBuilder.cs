using Microsoft.Extensions.Caching.Memory;
using NflCoverage.Modules.League.Contracts;
using NflCoverage.Rules;
using NflCoverage.Simulation;

namespace NflCoverage.Modules.PlayoffOdds;

/// <summary>Translates League data into the simulation engine's input, as of the end of a given week.</summary>
internal sealed class SimulationInputBuilder(ILeagueQueries league, IMemoryCache cache)
{
    public async Task<(SimulationInput Input, int ThroughWeek)?> BuildAsync(int season, int? throughWeek, CancellationToken cancellationToken)
    {
        var games = (await league.GetGamesAsync(season, cancellationToken)).Where(g => g.GameType == GameTypes.Regular).ToList();
        if (games.Count == 0)
        {
            return null;
        }

        var finals = games.Where(g => g.Status == GameStatus.Final).ToList();
        var week = Math.Clamp(throughWeek ?? (finals.Count == 0 ? 0 : finals.Max(g => g.Week)), 0, games.Max(g => g.Week));
        var completed = finals.Where(g => g.Week <= week).ToList();
        var remaining = games.Except(completed).ToList();

        var history = await cache.GetOrCreateAsync(("elo-history", season), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
            return await league.GetFinalGamesBeforeAsync(season, cancellationToken);
        }) ?? [];
        var ratings = Elo.BuildRatings(history.Concat(completed).Select(ToHistorical));

        var teams = await league.GetTeamsAsync(cancellationToken);
        var input = new SimulationInput(
            season,
            [.. teams.Select(t => new Team(t.Id, t.Conference == "AFC" ? Conference.Afc : Conference.Nfc, t.Division))],
            [.. completed.Select(g => new GameScore(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value))],
            [.. remaining.Select(g => new ScheduledGame(g.Id, g.Week, g.HomeTeam, g.AwayTeam, g.NeutralSite, g.HomeSpread))],
            ratings);
        return (input, week);
    }

    private static HistoricalGame ToHistorical(GameDto g) =>
        new(g.Season, g.Kickoff?.UtcTicks ?? g.Week, new GameScore(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value), g.NeutralSite);
}
