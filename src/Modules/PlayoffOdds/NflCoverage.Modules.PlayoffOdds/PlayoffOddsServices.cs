using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NflCoverage.Modules.League.Contracts;
using NflCoverage.Modules.PlayoffOdds.Contracts;
using NflCoverage.Modules.PlayoffOdds.Persistence;
using NflCoverage.Modules.Standings.Contracts;
using NflCoverage.SharedKernel;
using NflCoverage.Simulation;

namespace NflCoverage.Modules.PlayoffOdds;

public sealed class PlayoffOddsOptions
{
    public const string Section = "PlayoffOdds";

    public int Iterations { get; set; } = 10_000;

    /// <summary>Recalculate the current season in the background when results change.</summary>
    public bool BackgroundRecalculation { get; set; } = true;
}

internal sealed class PlayoffOddsCalculator(
    SimulationInputBuilder inputs,
    OddsDbContext db,
    IOptions<PlayoffOddsOptions> options,
    TimeProvider clock) : IPlayoffOddsCalculator
{
    public async Task<PlayoffOddsSnapshot?> RecalculateAsync(int season, int? throughWeek = null, CancellationToken cancellationToken = default)
    {
        var built = season >= IStandingsQueries.FirstSupportedSeason ? await inputs.BuildAsync(season, throughWeek, cancellationToken) : null;
        if (built is not { } value)
        {
            return null;
        }

        var (input, week) = value;

        // The engine is single-threaded by design; partition across cores with distinct, reproducible seeds.
        var partitions = Math.Clamp(Environment.ProcessorCount, 1, 8);
        var perPartition = Math.Max(1, options.Value.Iterations / partitions);
        var parts = await Task.WhenAll(Enumerable.Range(0, partitions).Select(p =>
            Task.Run(() => SeasonSimulator.Run(input, perPartition, seed: (season * 1000) + (week * 10) + p), cancellationToken)));
        var result = SeasonSimulator.Merge(parts);

        await db.Runs.Where(r => r.Season == season && r.ThroughWeek == week).ExecuteDeleteAsync(cancellationToken);
        var run = new OddsRun { Season = season, ThroughWeek = week, Iterations = result.Iterations, ComputedAt = clock.GetUtcNow() };
        run.Teams.AddRange(result.Teams.Select(t => new Persistence.TeamOdds
        {
            RunId = run.Id, TeamId = t.TeamId, MeanWins = t.MeanWins, MakePlayoffs = t.MakePlayoffs, WinDivision = t.WinDivision,
            FirstSeed = t.FirstSeed, SeedProbabilities = [.. t.SeedProbabilities], WinConference = t.WinConference, WinSuperBowl = t.WinSuperBowl,
        }));
        db.Runs.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return PlayoffOddsQueries.ToSnapshot(run);
    }
}

internal sealed class PlayoffOddsQueries(OddsDbContext db, SimulationInputBuilder inputs) : IPlayoffOddsQueries
{
    public async Task<PlayoffOddsSnapshot?> GetLatestAsync(int season, CancellationToken cancellationToken = default)
    {
        var run = await db.Runs.AsNoTracking().Include(r => r.Teams)
            .Where(r => r.Season == season).OrderByDescending(r => r.ThroughWeek)
            .FirstOrDefaultAsync(cancellationToken);
        return run is null ? null : ToSnapshot(run);
    }

    public async Task<IReadOnlyList<PlayoffOddsSnapshot>> GetHistoryAsync(int season, CancellationToken cancellationToken = default) =>
        [.. (await db.Runs.AsNoTracking().Include(r => r.Teams).Where(r => r.Season == season).OrderBy(r => r.ThroughWeek)
            .ToListAsync(cancellationToken)).Select(ToSnapshot)];

    public async Task<SimulationInput?> GetSimulationInputAsync(int season, CancellationToken cancellationToken = default) =>
        season >= IStandingsQueries.FirstSupportedSeason
            ? (await inputs.BuildAsync(season, null, cancellationToken))?.Input
            : null;

    internal static PlayoffOddsSnapshot ToSnapshot(OddsRun run) =>
        new(run.Season, run.ThroughWeek, run.Iterations, run.ComputedAt,
            [.. run.Teams.OrderBy(t => t.TeamId).Select(t => new TeamPlayoffOdds(
                t.TeamId, t.MeanWins, t.MakePlayoffs, t.WinDivision, t.FirstSeed, t.SeedProbabilities, t.WinConference, t.WinSuperBowl))]);
}

/// <summary>Coalescing queue of seasons whose odds need recalculating.</summary>
internal sealed class RecalculationQueue
{
    private readonly Channel<int> _seasons = Channel.CreateUnbounded<int>();

    public void Enqueue(int season) => _seasons.Writer.TryWrite(season);

    public ChannelReader<int> Reader => _seasons.Reader;
}

internal sealed class EnqueueOnResultsChanged(RecalculationQueue queue) : IHandles<GameResultsChanged>
{
    public Task HandleAsync(GameResultsChanged @event, CancellationToken cancellationToken)
    {
        queue.Enqueue(@event.Season);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Keeps the current season's odds fresh: on startup and after every results change it fills in any missing
/// weekly snapshots (for the odds-over-time chart) and recomputes the latest week. Older seasons are skipped.
/// </summary>
internal sealed class OddsRecalculationWorker(
    RecalculationQueue queue,
    IServiceScopeFactory scopes,
    ILogger<OddsRecalculationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        queue.Enqueue(int.MaxValue); // "current season" on startup
        while (await queue.Reader.WaitToReadAsync(stoppingToken))
        {
            var requested = new HashSet<int>();
            while (queue.Reader.TryRead(out var season))
            {
                requested.Add(season);
            }

            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var seasons = await scope.ServiceProvider.GetRequiredService<ILeagueQueries>().GetSeasonsAsync(stoppingToken);
                if (seasons.Count > 0 && (requested.Contains(int.MaxValue) || requested.Contains(seasons[0])))
                {
                    await RefreshSeasonAsync(scope.ServiceProvider, seasons[0], stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Playoff odds recalculation failed");
            }
        }
    }

    private async Task RefreshSeasonAsync(IServiceProvider services, int season, CancellationToken cancellationToken)
    {
        var games = await services.GetRequiredService<ILeagueQueries>().GetGamesAsync(season, cancellationToken);
        var lastPlayed = games.Where(g => g.GameType == GameTypes.Regular && g.Status == GameStatus.Final).Select(g => g.Week).DefaultIfEmpty(0).Max();
        var db = services.GetRequiredService<OddsDbContext>();
        var existing = await db.Runs.Where(r => r.Season == season).Select(r => r.ThroughWeek).ToListAsync(cancellationToken);
        var calculator = services.GetRequiredService<IPlayoffOddsCalculator>();

        foreach (var week in Enumerable.Range(0, lastPlayed + 1).Where(w => w == lastPlayed || !existing.Contains(w)))
        {
            await calculator.RecalculateAsync(season, week, cancellationToken);
            logger.LogInformation("Playoff odds updated for {Season} through week {Week}", season, week);
        }
    }
}
