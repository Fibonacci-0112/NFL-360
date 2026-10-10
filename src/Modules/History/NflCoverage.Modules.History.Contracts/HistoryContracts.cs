namespace NflCoverage.Modules.History.Contracts;

/// <summary>How far a team got in the playoffs: the round it reached, or <see cref="Champion"/>.</summary>
public enum PlayoffRound
{
    None,
    WildCard,
    Divisional,
    Conference,
    SuperBowl,
    Champion,
}

/// <param name="DivisionTitle"><c>null</c> before 2002 (other alignment) and while the regular season is in progress.</param>
/// <param name="Seed">Playoff seed, when known (2002 onward).</param>
public sealed record FranchiseSeason(
    int Season,
    int Wins,
    int Losses,
    int Ties,
    double WinPct,
    int PointsFor,
    int PointsAgainst,
    PlayoffRound Playoffs,
    int PlayoffWins,
    int PlayoffLosses,
    bool? DivisionTitle,
    int? Seed);

public sealed record FranchiseTotals(
    int Seasons,
    int Wins,
    int Losses,
    int Ties,
    double WinPct,
    int PlayoffAppearances,
    int PlayoffWins,
    int PlayoffLosses,
    int DivisionTitles,
    int ConferenceTitles,
    int SuperBowlWins);

/// <param name="Seasons">Oldest first, from the first season in the schedule data (1999).</param>
public sealed record FranchiseHistory(string Team, IReadOnlyList<FranchiseSeason> Seasons, FranchiseTotals Totals);

public sealed record SeriesRecord(int WinsA, int WinsB, int Ties)
{
    public int Games => WinsA + WinsB + Ties;
}

public sealed record Meeting(
    string GameId,
    int Season,
    string GameType,
    int Week,
    DateTimeOffset? Kickoff,
    string HomeTeam,
    string AwayTeam,
    int HomeScore,
    int AwayScore,
    bool NeutralSite)
{
    /// <summary><c>null</c> for a tie.</summary>
    public string? Winner => HomeScore > AwayScore ? HomeTeam : AwayScore > HomeScore ? AwayTeam : null;
}

/// <param name="Team">The team on the streak; <c>null</c> when the latest meetings were ties.</param>
public sealed record Streak(string? Team, int Games);

/// <param name="LastMeetings">Most recent first.</param>
/// <param name="PlayoffMeetings">Most recent first.</param>
public sealed record HeadToHead(
    string TeamA,
    string TeamB,
    SeriesRecord Overall,
    SeriesRecord Regular,
    SeriesRecord Playoffs,
    int PointsA,
    int PointsB,
    Streak? Streak,
    IReadOnlyList<Meeting> LastMeetings,
    IReadOnlyList<Meeting> PlayoffMeetings);

public sealed record SuperBowlResult(int Season, string GameId, string Winner, string Loser, int WinnerScore, int LoserScore);

/// <param name="BestRecordTeams">Teams tied for the best regular-season record.</param>
public sealed record SeasonSummary(
    int Season,
    SuperBowlResult? SuperBowl,
    string? AfcChampion,
    string? NfcChampion,
    IReadOnlyList<string> BestRecordTeams,
    int BestWins,
    int BestLosses,
    int BestTies);

/// <summary>Franchise and league history, derived from League results (and Standings for division titles).</summary>
public interface IHistoryQueries
{
    /// <returns><c>null</c> for an unknown team.</returns>
    Task<FranchiseHistory?> GetFranchiseHistoryAsync(string team, CancellationToken cancellationToken = default);

    /// <returns><c>null</c> when either team is unknown or both are the same.</returns>
    Task<HeadToHead?> GetHeadToHeadAsync(string teamA, string teamB, CancellationToken cancellationToken = default);

    /// <summary>Super Bowl results, latest first.</summary>
    Task<IReadOnlyList<SuperBowlResult>> GetChampionsAsync(CancellationToken cancellationToken = default);

    Task<SeasonSummary?> GetSeasonSummaryAsync(int season, CancellationToken cancellationToken = default);

    /// <summary>Every season's summary, latest first.</summary>
    Task<IReadOnlyList<SeasonSummary>> GetSeasonSummariesAsync(CancellationToken cancellationToken = default);
}
