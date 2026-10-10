using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NflCoverage.Modules.Ingestion.Espn;
using NflCoverage.Modules.Ingestion.Persistence;
using NflCoverage.Modules.League.Contracts;

namespace NflCoverage.Modules.Ingestion.Nflverse;

/// <summary>Pulls teams and the full schedule (1999-present) from nflverse and upserts them into the League module.</summary>
internal sealed class NflverseImporter(
    HttpClient http,
    IOptions<IngestionOptions> options,
    ILeagueIngestion league,
    IngestionDbContext db,
    TimeProvider clock,
    ILogger<NflverseImporter> logger)
{
    public const string Provider = "nflverse";

    public async Task ImportAsync(CancellationToken cancellationToken)
    {
        var run = new ImportRun { Source = Provider, StartedAt = clock.GetUtcNow() };
        db.ImportRuns.Add(run);
        try
        {
            var teams = NflverseParser.ParseTeams(await DownloadAsync(options.Value.NflverseTeamsUrl, cancellationToken));
            var games = NflverseParser.ParseGames(await DownloadAsync(options.Value.NflverseGamesUrl, cancellationToken));
            await ImportAsync(teams, games, run, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or FormatException)
        {
            run.Error = ex.Message;
            logger.LogError(ex, "nflverse import failed");
        }
        finally
        {
            run.FinishedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    internal async Task ImportAsync(IReadOnlyCollection<TeamUpsert> teams, IReadOnlyCollection<NflverseGame> games, ImportRun run, CancellationToken cancellationToken)
    {
        await league.UpsertTeamsAsync(teams, cancellationToken);
        run.RowsRead = games.Count;
        run.RowsChanged = await league.UpsertGamesAsync([.. games.Select(g => g.Game)], cancellationToken);
        await RecordEspnIdsAsync(games, cancellationToken);
        logger.LogInformation("nflverse import: {Teams} teams, {Games} games read, {Changed} changed", teams.Count, games.Count, run.RowsChanged);
    }

    private async Task RecordEspnIdsAsync(IEnumerable<NflverseGame> games, CancellationToken cancellationToken)
    {
        var known = await db.ExternalIds
            .Where(x => x.Provider == EspnProvider.Name && x.EntityType == EspnProvider.GameEntity)
            .ToDictionaryAsync(x => x.ExternalKey, cancellationToken);

        foreach (var game in games.Where(g => g.EspnId is not null))
        {
            if (known.TryGetValue(game.EspnId!, out var mapping))
            {
                mapping.InternalId = game.Game.Id;
                continue;
            }

            var added = new ExternalId { Provider = EspnProvider.Name, EntityType = EspnProvider.GameEntity, ExternalKey = game.EspnId!, InternalId = game.Game.Id };
            db.ExternalIds.Add(added);
            known[added.ExternalKey] = added;
        }
    }

    private async Task<StringReader> DownloadAsync(Uri url, CancellationToken cancellationToken) =>
        new(await http.GetStringAsync(url, cancellationToken));
}
