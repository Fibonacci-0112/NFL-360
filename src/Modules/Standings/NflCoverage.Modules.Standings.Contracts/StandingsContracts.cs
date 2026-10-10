namespace NflCoverage.Modules.Standings.Contracts;

public sealed record StandingRow(
    string TeamId,
    string Conference,
    string Division,
    int Wins,
    int Losses,
    int Ties,
    double WinPct,
    int PointsFor,
    int PointsAgainst,
    string DivisionRecord,
    string ConferenceRecord,
    int DivisionRank,
    int ConferenceRank,
    int? Seed);

public sealed record TiebreakExplanation(string Context, IReadOnlyList<string> TiedTeams, string Winner, string DecidedBy);

/// <param name="ThroughWeek">Last regular-season week included.</param>
/// <param name="LastPlayedWeek">Latest week with a final result, i.e. the default for <paramref name="ThroughWeek"/>.</param>
public sealed record SeasonStandings(
    int Season,
    int ThroughWeek,
    int LastPlayedWeek,
    IReadOnlyList<StandingRow> Teams,
    IReadOnlyList<TiebreakExplanation> Tiebreaks);

public interface IStandingsQueries
{
    /// <summary>First season played under the current 32-team, 8-division alignment.</summary>
    const int FirstSupportedSeason = 2002;

    /// <returns><c>null</c> when the season is unknown or predates the current alignment.</returns>
    Task<SeasonStandings?> GetAsync(int season, int? throughWeek = null, CancellationToken cancellationToken = default);
}
