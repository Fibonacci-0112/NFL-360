using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NflCoverage.Modules.Ingestion.Persistence;
using NflCoverage.Modules.League.Contracts;

namespace NflCoverage.Modules.Ingestion.Espn;

/// <summary>
/// Polls ESPN's scoreboard only while a game is near or past kickoff and not yet final, and pushes live scores into
/// the League module. ESPN events are matched to our games through the crosswalk filled by the nflverse import.
/// </summary>
internal sealed class EspnLivePoller(
    IServiceScopeFactory scopes,
    IHttpClientFactory httpClients,
    IOptions<IngestionOptions> options,
    TimeProvider clock,
    ILogger<EspnLivePoller> logger) : BackgroundService
{
    public const string HttpClientName = "espn";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var live = false;
            try
            {
                live = await PollOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "ESPN poll failed");
            }

            await Task.Delay(live ? options.Value.EspnLivePollInterval : options.Value.EspnIdlePollInterval, clock, stoppingToken);
        }
    }

    /// <returns><c>true</c> when games are in their live window, so the caller polls at the faster rate.</returns>
    internal async Task<bool> PollOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var league = scope.ServiceProvider.GetRequiredService<ILeagueQueries>();
        var now = clock.GetUtcNow();
        var window = await league.GetGamesKickingOffBetweenAsync(now.AddHours(-5), now.AddMinutes(30), cancellationToken);
        if (!window.Any(g => g.Status != GameStatus.Final))
        {
            return false;
        }

        var json = await httpClients.CreateClient(HttpClientName).GetStringAsync(options.Value.EspnScoreboardUrl, cancellationToken);
        var scores = EspnScoreboardParser.Parse(json);

        var db = scope.ServiceProvider.GetRequiredService<IngestionDbContext>();
        var eventIds = scores.Select(s => s.EventId).ToList();
        var gameIds = await db.ExternalIds.AsNoTracking()
            .Where(x => x.Provider == EspnProvider.Name && x.EntityType == EspnProvider.GameEntity && eventIds.Contains(x.ExternalKey))
            .ToDictionaryAsync(x => x.ExternalKey, x => x.InternalId, cancellationToken);

        var updates = scores
            .Where(s => gameIds.ContainsKey(s.EventId))
            .Select(s => new LiveScoreUpdate(gameIds[s.EventId], s.HomeScore, s.AwayScore, s.Status, s.Detail))
            .ToList();
        await scope.ServiceProvider.GetRequiredService<ILeagueIngestion>().ApplyLiveScoresAsync(updates, cancellationToken);
        return true;
    }
}
