using NflCoverage.Simulation;

namespace NflCoverage.Modules.PlayoffOdds.Contracts;

public sealed record TeamPlayoffOdds(
    string TeamId,
    double MeanWins,
    double MakePlayoffs,
    double WinDivision,
    double FirstSeed,
    IReadOnlyList<double> SeedProbabilities,
    double WinConference,
    double WinSuperBowl);

/// <param name="ThroughWeek">Odds as of the end of this regular-season week (0 = before the season).</param>
public sealed record PlayoffOddsSnapshot(
    int Season,
    int ThroughWeek,
    int Iterations,
    DateTimeOffset ComputedAt,
    IReadOnlyList<TeamPlayoffOdds> Teams);

public interface IPlayoffOddsQueries
{
    Task<PlayoffOddsSnapshot?> GetLatestAsync(int season, CancellationToken cancellationToken = default);

    /// <summary>One snapshot per week, oldest first, for odds-over-time charts.</summary>
    Task<IReadOnlyList<PlayoffOddsSnapshot>> GetHistoryAsync(int season, CancellationToken cancellationToken = default);

    /// <summary>Everything a client needs to run its own what-if simulations for the season's current state.</summary>
    Task<SimulationInput?> GetSimulationInputAsync(int season, CancellationToken cancellationToken = default);
}

public interface IPlayoffOddsCalculator
{
    /// <summary>Simulates the season as of <paramref name="throughWeek"/> (latest played week if null) and stores the snapshot.</summary>
    Task<PlayoffOddsSnapshot?> RecalculateAsync(int season, int? throughWeek = null, CancellationToken cancellationToken = default);
}
