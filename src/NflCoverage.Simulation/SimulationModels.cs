using NflCoverage.Rules;

namespace NflCoverage.Simulation;

public enum ForcedOutcome
{
    HomeWin,
    AwayWin,
    Tie,
}

/// <summary>A game still to be played. <paramref name="HomeSpread"/> is the market's expected home margin, if known.</summary>
public sealed record ScheduledGame(string Id, int Week, string HomeTeam, string AwayTeam, bool NeutralSite = false, double? HomeSpread = null);

public sealed record SimulationInput(
    int Season,
    IReadOnlyList<Team> Teams,
    IReadOnlyList<GameScore> CompletedGames,
    IReadOnlyList<ScheduledGame> RemainingGames,
    IReadOnlyDictionary<string, double> Ratings)
{
    /// <summary>What-if picks keyed by <see cref="ScheduledGame.Id"/>; these games are not sampled.</summary>
    public IReadOnlyDictionary<string, ForcedOutcome> ForcedOutcomes { get; init; } = new Dictionary<string, ForcedOutcome>();
}

public sealed record TeamOdds(
    string TeamId,
    double MeanWins,
    double MakePlayoffs,
    double WinDivision,
    double FirstSeed,
    IReadOnlyList<double> SeedProbabilities,
    double WinConference,
    double WinSuperBowl);

public sealed record SimulationResult(int Season, int Iterations, IReadOnlyList<TeamOdds> Teams);
