using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NflCoverage.Modules.League.Contracts;
using NflCoverage.SharedKernel;

namespace NflCoverage.Modules.Ingestion.Nflverse;

/// <summary>Coalescing "a week just finished" signal; each week is signalled at most once.</summary>
internal sealed class StatsRefreshSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
    private readonly Lock _lock = new();
    private (int Season, int Week)? _lastWeek;

    public ChannelReader<bool> Reader => _channel.Reader;

    public bool WeekCompleted(int season, int week)
    {
        lock (_lock)
        {
            if (_lastWeek == (season, week))
            {
                return false;
            }

            _lastWeek = (season, week);
        }

        _channel.Writer.TryWrite(true);
        return true;
    }
}

/// <summary>Signals a stats refresh once every game of the current season's latest week is final.</summary>
internal sealed class RefreshStatsWhenWeekCompletes(ILeagueQueries league, StatsRefreshSignal signal, TimeProvider clock) : IHandles<GameResultsChanged>
{
    public async Task HandleAsync(GameResultsChanged @event, CancellationToken cancellationToken)
    {
        if (@event.Season != NflverseStatsImporter.CurrentSeason(clock.GetUtcNow()))
        {
            return;
        }

        var games = await league.GetGamesAsync(@event.Season, cancellationToken);
        var latest = games.Where(g => g.Status == GameStatus.Final).MaxBy(g => g.Week);
        if (latest is not null && games.Where(g => g.Week == latest.Week).All(g => g.Status == GameStatus.Final))
        {
            signal.WeekCompleted(@event.Season, latest.Week);
        }
    }
}

/// <summary>
/// Runs the stats sync on startup and then on <see cref="IngestionOptions.StatsRefreshInterval"/>. When a week
/// completes it also refreshes the current season, after <see cref="IngestionOptions.StatsRefreshDebounce"/> so
/// nflverse has published the new numbers and score corrections are coalesced into one import.
/// </summary>
internal sealed class NflverseStatsSyncService(
    IServiceScopeFactory scopes,
    StatsRefreshSignal signal,
    IOptions<IngestionOptions> options,
    TimeProvider clock,
    ILogger<NflverseStatsSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunAsync((importer, ct) => importer.SyncAsync(ct), stoppingToken);
            var nextSync = clock.GetUtcNow() + options.Value.StatsRefreshInterval;

            while (nextSync - clock.GetUtcNow() is var remaining && remaining > TimeSpan.Zero)
            {
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var weekCompleted = signal.Reader.WaitToReadAsync(wait.Token).AsTask();
                var timeout = Task.Delay(remaining, clock, wait.Token);
                var first = await Task.WhenAny(weekCompleted, timeout);
                await wait.CancelAsync();
                stoppingToken.ThrowIfCancellationRequested();
                if (first == timeout)
                {
                    break;
                }

                await Task.Delay(options.Value.StatsRefreshDebounce, clock, stoppingToken);
                while (signal.Reader.TryRead(out _))
                {
                }

                await RunAsync((importer, ct) => importer.RefreshCurrentSeasonAsync(ct), stoppingToken);
            }
        }
    }

    private async Task RunAsync(Func<NflverseStatsImporter, CancellationToken, Task> import, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await import(scope.ServiceProvider.GetRequiredService<NflverseStatsImporter>(), stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "nflverse stats sync failed; retrying on the next tick");
        }
    }
}
