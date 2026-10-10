namespace NflCoverage.Modules.Stats.Contracts;

/// <summary>Regular season or postseason; nflverse's REG and POST.</summary>
public enum SeasonType
{
    Regular,
    Postseason,
}

public static class SeasonTypes
{
    /// <summary>The nflverse code: REG or POST.</summary>
    public static string Code(SeasonType seasonType) => seasonType == SeasonType.Postseason ? "POST" : "REG";

    /// <summary>Accepts REG/POST or the enum names, case-insensitively; anything else is the regular season.</summary>
    public static SeasonType Parse(string? value) =>
        value is not null && (value.Equals("POST", StringComparison.OrdinalIgnoreCase) || value.Equals(nameof(SeasonType.Postseason), StringComparison.OrdinalIgnoreCase))
            ? SeasonType.Postseason
            : SeasonType.Regular;
}

/// <summary>Player leaderboards. Every category ranks descending.</summary>
public enum LeaderCategory
{
    PassingYards,
    PassingTds,
    PasserRating,
    PassingEpa,
    RushingYards,
    RushingTds,
    ReceivingYards,
    Receptions,
    ReceivingTds,
    Sacks,
    Interceptions,
    Tackles,
    FgMade,
    FantasyPointsPpr,
}

public sealed record PassingStats(int Completions, int Attempts, int Yards, int Touchdowns, int Interceptions, int Sacks, double Epa, double? Cpoe)
{
    public static readonly PassingStats None = new(0, 0, 0, 0, 0, 0, 0, null);
}

public sealed record RushingStats(int Carries, int Yards, int Touchdowns, double Epa)
{
    public static readonly RushingStats None = new(0, 0, 0, 0);
}

public sealed record ReceivingStats(int Receptions, int Targets, int Yards, int Touchdowns, double Epa, double? TargetShare)
{
    public static readonly ReceivingStats None = new(0, 0, 0, 0, 0, null);
}

/// <param name="Sacks">Half sacks are credited when two players share one.</param>
public sealed record DefenseStats(
    int SoloTackles,
    int AssistedTackles,
    double TacklesForLoss,
    double Sacks,
    int QbHits,
    int Interceptions,
    int PassesDefended,
    int ForcedFumbles)
{
    public static readonly DefenseStats None = new(0, 0, 0, 0, 0, 0, 0, 0);

    public int Tackles => SoloTackles + AssistedTackles;
}

public sealed record KickingStats(int FgMade, int FgAttempted, int? FgLong, int PatMade, int PatAttempted)
{
    public static readonly KickingStats None = new(0, 0, null, 0, 0);
}

/// <summary>The curated box-score columns kept for a player in a season or a game.</summary>
public sealed record StatLine(
    PassingStats Passing,
    RushingStats Rushing,
    ReceivingStats Receiving,
    DefenseStats Defense,
    KickingStats Kicking,
    double FantasyPointsPpr)
{
    public static readonly StatLine None = new();

    /// <summary>Also lets EF Core materialize the nested complex types, which it cannot bind through a constructor.</summary>
    private StatLine()
        : this(PassingStats.None, RushingStats.None, ReceivingStats.None, DefenseStats.None, KickingStats.None, 0)
    {
    }
}

public sealed record PlayerProfile(
    string Id,
    string Name,
    string? Position,
    string? PositionGroup,
    string? HeadshotUrl,
    DateOnly? BirthDate,
    string? College,
    int? DraftYear,
    int? DraftRound,
    int? DraftPick,
    string? DraftTeam,
    int? RookieSeason,
    int? LastSeason,
    string? LatestTeam);

public sealed record PlayerSummary(string Id, string Name, string? Position, string? LatestTeam, string? HeadshotUrl, int? RookieSeason, int? LastSeason);

/// <param name="Team">The player's most recent team that season; empty in a few source rows.</param>
public sealed record PlayerSeasonStats(string PlayerId, int Season, SeasonType SeasonType, string? Team, int Games, StatLine Stats);

public sealed record PlayerGameStats(
    string PlayerId,
    string GameId,
    int Season,
    int Week,
    SeasonType SeasonType,
    string? Team,
    string? Opponent,
    StatLine Stats);

/// <param name="Rank">Competition ranking: tied values share a rank.</param>
public sealed record LeaderRow(int Rank, PlayerSummary Player, string? Team, int Games, double Value, StatLine Stats);

public sealed record TeamLeader(LeaderCategory Category, LeaderRow Leader);

public sealed record TeamOffense(
    int PassAttempts,
    int Completions,
    int PassingYards,
    int PassingTds,
    int Interceptions,
    int SacksTaken,
    int Carries,
    int RushingYards,
    int RushingTds,
    int FumblesLost,
    double PassingEpa,
    double RushingEpa)
{
    public int Plays => StatFormulas.Plays(PassAttempts, SacksTaken, Carries);

    public int Yards => PassingYards + RushingYards;

    public int Turnovers => Interceptions + FumblesLost;

    public double Epa => PassingEpa + RushingEpa;
}

public sealed record TeamDefense(
    int SoloTackles,
    double TacklesForLoss,
    double Sacks,
    int QbHits,
    int Interceptions,
    int PassesDefended,
    int ForcedFumbles,
    int Touchdowns);

public sealed record TeamSeasonStats(
    string Team,
    int Season,
    SeasonType SeasonType,
    int Games,
    TeamOffense Offense,
    TeamDefense Defense,
    int Penalties,
    int PenaltyYards);

/// <summary>One team's offense in one game. The opponent's row for the same game is that team's defense allowed.</summary>
public sealed record TeamGameStats(string Team, string GameId, int Season, int Week, SeasonType SeasonType, string Opponent, TeamOffense Offense);

/// <summary>A team's efficiency in one game: its offense, and the opponent's offense against its defense.</summary>
public sealed record TeamGameEfficiency(string GameId, int Week, SeasonType SeasonType, string Opponent, double OffenseEpaPerPlay, double DefenseEpaPerPlay);

/// <param name="OffenseRank">1 = most EPA per play.</param>
/// <param name="DefenseRank">1 = least EPA allowed per play.</param>
public sealed record TeamComparisonRow(
    string Team,
    int Games,
    int OffensePlays,
    double OffenseEpaPerPlay,
    double PassEpaPerDropback,
    double RushEpaPerCarry,
    double YardsPerPlay,
    int DefensePlays,
    double DefenseEpaPerPlay,
    double YardsAllowedPerPlay,
    int Giveaways,
    int Takeaways,
    int OffenseRank,
    int DefenseRank)
{
    public int TurnoverDifferential => Takeaways - Giveaways;
}

public interface IStatsQueries
{
    /// <summary>Seasons with player statistics, latest first.</summary>
    Task<IReadOnlyList<int>> GetSeasonsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaderRow>> GetLeadersAsync(
        int season,
        LeaderCategory category,
        SeasonType seasonType = SeasonType.Regular,
        int top = 50,
        CancellationToken cancellationToken = default);

    Task<PlayerProfile?> GetPlayerAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Every season line of a player, regular season and postseason, oldest first.</summary>
    Task<IReadOnlyList<PlayerSeasonStats>> GetPlayerCareerAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Game-by-game lines. Kept only for recent seasons (see the ingestion settings).</summary>
    Task<IReadOnlyList<PlayerGameStats>> GetPlayerGameLogAsync(string id, int season, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlayerSummary>> SearchPlayersAsync(string query, int limit = 25, CancellationToken cancellationToken = default);

    Task<TeamSeasonStats?> GetTeamSeasonAsync(
        string team,
        int season,
        SeasonType seasonType = SeasonType.Regular,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TeamGameStats>> GetTeamGamesAsync(string team, int season, CancellationToken cancellationToken = default);

    /// <summary>Offense and defense EPA per play for each of a team's games, in order: the weekly trend.</summary>
    Task<IReadOnlyList<TeamGameEfficiency>> GetTeamGameEfficiencyAsync(string team, int season, CancellationToken cancellationToken = default);

    /// <summary>Every team's offense and defense efficiency, from game-level data.</summary>
    Task<IReadOnlyList<TeamComparisonRow>> GetTeamComparisonAsync(
        int season,
        SeasonType seasonType = SeasonType.Regular,
        CancellationToken cancellationToken = default);

    /// <summary>A team's regular-season leader in the main categories, attributing each player to their latest team that season.</summary>
    Task<IReadOnlyList<TeamLeader>> GetTeamRosterLeadersAsync(string team, int season, CancellationToken cancellationToken = default);
}

/// <param name="Extra">Source columns not curated into typed fields, kept so no data is lost.</param>
public sealed record PlayerUpsert(PlayerProfile Profile, IReadOnlyDictionary<string, string> Extra);

/// <param name="Name">Used to create a minimal player record when the player is missing from the player directory.</param>
public sealed record PlayerSeasonUpsert(
    PlayerSeasonStats Stats,
    string Name,
    string? Position,
    string? PositionGroup,
    string? HeadshotUrl,
    IReadOnlyDictionary<string, string> Extra);

public sealed record PlayerGameUpsert(PlayerGameStats Stats, IReadOnlyDictionary<string, string> Extra);

public sealed record TeamSeasonUpsert(TeamSeasonStats Stats, IReadOnlyDictionary<string, string> Extra);

public sealed record TeamGameUpsert(TeamGameStats Stats, IReadOnlyDictionary<string, string> Extra);

/// <summary>How many rows of each kind are stored for a season.</summary>
public sealed record StatsCoverage(int Season, int PlayerSeasons, int TeamSeasons, int TeamGames, int PlayerGames);

/// <summary>
/// Write side used by the Ingestion module. Each upsert replaces all rows of one season (and season type) in a
/// transaction, so re-importing a file is idempotent.
/// </summary>
public interface IStatsIngestion
{
    /// <summary>Inserts new players and updates known ones; players are never deleted.</summary>
    Task<int> UpsertPlayersAsync(IReadOnlyCollection<PlayerUpsert> players, CancellationToken cancellationToken = default);

    Task<int> UpsertPlayerSeasonsAsync(int season, SeasonType seasonType, IReadOnlyCollection<PlayerSeasonUpsert> rows, CancellationToken cancellationToken = default);

    Task<int> UpsertTeamSeasonsAsync(int season, SeasonType seasonType, IReadOnlyCollection<TeamSeasonUpsert> rows, CancellationToken cancellationToken = default);

    /// <summary>Replaces the season's team games, regular season and postseason.</summary>
    Task<int> UpsertTeamGamesAsync(int season, IReadOnlyCollection<TeamGameUpsert> rows, CancellationToken cancellationToken = default);

    /// <summary>Replaces the season's player game logs, regular season and postseason.</summary>
    Task<int> UpsertPlayerGamesAsync(int season, IReadOnlyCollection<PlayerGameUpsert> rows, CancellationToken cancellationToken = default);

    /// <summary>Drops game logs older than <paramref name="season"/>, keeping storage bounded.</summary>
    Task<int> DeletePlayerGamesBeforeAsync(int season, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StatsCoverage>> GetCoverageAsync(CancellationToken cancellationToken = default);
}
