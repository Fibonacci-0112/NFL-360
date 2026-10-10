using NflCoverage.SharedKernel;

namespace NflCoverage.Modules.League.Contracts;

public enum GameStatus
{
    Scheduled,
    InProgress,
    Final,
}

/// <summary>Season phases as used by nflverse: REG, WC, DIV, CON, SB.</summary>
public static class GameTypes
{
    public const string Regular = "REG";

    public static bool IsPostseason(string gameType) => gameType != Regular;
}

public sealed record TeamDto(
    string Id,
    string Name,
    string Nickname,
    string Conference,
    string Division,
    string PrimaryColor,
    string SecondaryColor,
    string? LogoUrl);

public sealed record GameDto(
    string Id,
    int Season,
    string GameType,
    int Week,
    DateTimeOffset? Kickoff,
    string HomeTeam,
    string AwayTeam,
    int? HomeScore,
    int? AwayScore,
    GameStatus Status,
    string? StatusDetail,
    bool NeutralSite,
    double? HomeSpread);

/// <summary>Read side of the League module, consumed by other modules and the web host.</summary>
public interface ILeagueQueries
{
    Task<IReadOnlyList<TeamDto>> GetTeamsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetSeasonsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameDto>> GetGamesAsync(int season, CancellationToken cancellationToken = default);

    /// <summary>All final regular-season and postseason games before <paramref name="beforeSeason"/>, for rating models.</summary>
    Task<IReadOnlyList<GameDto>> GetFinalGamesBeforeAsync(int beforeSeason, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameDto>> GetGamesKickingOffBetweenAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

public sealed record TeamUpsert(
    string Id,
    string Name,
    string Nickname,
    string Conference,
    string Division,
    string PrimaryColor,
    string SecondaryColor,
    string? LogoUrl);

public sealed record GameUpsert(
    string Id,
    int Season,
    string GameType,
    int Week,
    DateTimeOffset? Kickoff,
    string HomeTeam,
    string AwayTeam,
    int? HomeScore,
    int? AwayScore,
    GameStatus Status,
    bool NeutralSite,
    double? HomeSpread);

public sealed record LiveScoreUpdate(string GameId, int HomeScore, int AwayScore, GameStatus Status, string? StatusDetail);

/// <summary>Write side used by the Ingestion module; data providers never touch League storage directly.</summary>
public interface ILeagueIngestion
{
    Task UpsertTeamsAsync(IReadOnlyCollection<TeamUpsert> teams, CancellationToken cancellationToken = default);

    /// <returns>The number of games inserted or changed.</returns>
    Task<int> UpsertGamesAsync(IReadOnlyCollection<GameUpsert> games, CancellationToken cancellationToken = default);

    Task ApplyLiveScoresAsync(IReadOnlyCollection<LiveScoreUpdate> updates, CancellationToken cancellationToken = default);
}

/// <summary>Raised when any final score in <paramref name="Season"/> was added or corrected.</summary>
public sealed record GameResultsChanged(int Season) : IIntegrationEvent;
