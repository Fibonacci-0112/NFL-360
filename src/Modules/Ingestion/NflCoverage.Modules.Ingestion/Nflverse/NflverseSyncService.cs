using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NflCoverage.Modules.Ingestion.Nflverse;

/// <summary>Imports nflverse on startup and then on a fixed interval (nflverse itself refreshes nightly in season).</summary>
internal sealed class NflverseSyncService(
    IServiceScopeFactory scopes,
    IOptions<IngestionOptions> options,
    TimeProvider clock,
    ILogger<NflverseSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.NflverseRefreshInterval, clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<NflverseImporter>().ImportAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "nflverse sync failed; retrying on next tick");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
