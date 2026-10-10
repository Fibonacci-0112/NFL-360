using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NflCoverage.Modules.Ingestion.Persistence;
using NflCoverage.Modules.Stats.Contracts;

namespace NflCoverage.Modules.Ingestion.Nflverse;

/// <summary>Opens one nflverse release file by its path, or returns <c>null</c> when it is not published (yet).</summary>
internal delegate Task<TextReader?> OpenStatsFile(string path, CancellationToken cancellationToken);

/// <summary>
/// Imports player and team statistics from the nflverse release files into the Stats module. Missing seasons are
/// backfilled from <see cref="IngestionOptions.StatsFirstSeason"/>; the current season is always re-imported.
/// </summary>
internal sealed class NflverseStatsImporter(
    HttpClient http,
    IOptions<IngestionOptions> options,
    IStatsIngestion stats,
    IngestionDbContext db,
    TimeProvider clock,
    ILogger<NflverseStatsImporter> logger)
{
    public const string Source = "nflverse-stats";

    /// <summary>The season in progress: the NFL year starts in March, so January and February belong to the previous season.</summary>
    public static int CurrentSeason(DateTimeOffset now) => now.Month >= 3 ? now.Year : now.Year - 1;

    /// <summary>Player directory, every season that is missing or incomplete, and the current season.</summary>
    public Task SyncAsync(CancellationToken cancellationToken) =>
        RecordRunAsync(async run =>
        {
            if (await DownloadAsync("players/players.csv", cancellationToken) is { } players)
            {
                using (players)
                {
                    await ImportPlayersAsync(players, run, cancellationToken);
                }
            }

            var current = CurrentSeason(clock.GetUtcNow());
            var firstLogSeason = FirstGameLogSeason(current);
            var coverage = (await stats.GetCoverageAsync(cancellationToken)).ToDictionary(c => c.Season);
            var errors = new List<string>();

            for (var season = options.Value.StatsFirstSeason; season <= current; season++)
            {
                var logs = season >= firstLogSeason;
                if (season != current && coverage.TryGetValue(season, out var c)
                    && c.PlayerSeasons > 0 && c.TeamSeasons > 0 && c.TeamGames > 0 && (!logs || c.PlayerGames > 0))
                {
                    continue;
                }

                try
                {
                    await ImportSeasonAsync(season, logs, DownloadAsync, run, cancellationToken);
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or FormatException)
                {
                    // Keep going; the next sync retries the seasons that are still incomplete.
                    errors.Add($"{season}: {ex.Message}");
                    logger.LogError(ex, "nflverse stats import failed for {Season}", season);
                }
            }

            await stats.DeletePlayerGamesBeforeAsync(firstLogSeason, cancellationToken);
            run.Error = errors.Count == 0 ? null : string.Join("; ", errors);
        });

    /// <summary>Re-imports the current season only: the refresh after each completed week.</summary>
    public Task RefreshCurrentSeasonAsync(CancellationToken cancellationToken) =>
        RecordRunAsync(run => ImportSeasonAsync(CurrentSeason(clock.GetUtcNow()), gameLogs: true, DownloadAsync, run, cancellationToken));

    internal async Task ImportPlayersAsync(TextReader csv, ImportRun run, CancellationToken cancellationToken)
    {
        var players = NflverseStatsParser.ParsePlayers(csv);
        run.RowsRead += players.Count;
        run.RowsChanged += await stats.UpsertPlayersAsync(players, cancellationToken);
        logger.LogInformation("nflverse players: {Players} read", players.Count);
    }

    internal async Task ImportSeasonAsync(int season, bool gameLogs, OpenStatsFile open, ImportRun run, CancellationToken cancellationToken)
    {
        var read = run.RowsRead;
        foreach (var seasonType in new[] { SeasonType.Regular, SeasonType.Postseason })
        {
            var code = SeasonTypes.Code(seasonType).ToLowerInvariant();
            await ImportFileAsync(open, $"stats_player/stats_player_{code}_{season}.csv", NflverseStatsParser.ParsePlayerSeasons,
                rows => stats.UpsertPlayerSeasonsAsync(season, seasonType, rows, cancellationToken), run, cancellationToken);
            await ImportFileAsync(open, $"stats_team/stats_team_{code}_{season}.csv", NflverseStatsParser.ParseTeamSeasons,
                rows => stats.UpsertTeamSeasonsAsync(season, seasonType, rows, cancellationToken), run, cancellationToken);
        }

        await ImportFileAsync(open, $"stats_team/stats_team_week_{season}.csv", NflverseStatsParser.ParseTeamGames,
            rows => stats.UpsertTeamGamesAsync(season, rows, cancellationToken), run, cancellationToken);
        if (gameLogs)
        {
            await ImportFileAsync(open, $"stats_player/stats_player_week_{season}.csv", NflverseStatsParser.ParsePlayerGames,
                rows => stats.UpsertPlayerGamesAsync(season, rows, cancellationToken), run, cancellationToken);
        }

        logger.LogInformation("nflverse stats {Season}: {Rows} rows read", season, run.RowsRead - read);
    }

    private int FirstGameLogSeason(int current) => current - Math.Max(1, options.Value.StatsGameLogSeasons) + 1;

    private static async Task ImportFileAsync<T>(
        OpenStatsFile open,
        string path,
        Func<TextReader, List<T>> parse,
        Func<List<T>, Task<int>> upsert,
        ImportRun run,
        CancellationToken cancellationToken)
    {
        // A file that is not published yet (e.g. the postseason in October) leaves the stored rows untouched.
        if (await open(path, cancellationToken) is not { } csv)
        {
            return;
        }

        List<T> rows;
        using (csv)
        {
            rows = parse(csv);
        }

        run.RowsRead += rows.Count;
        run.RowsChanged += await upsert(rows);
    }

    private async Task<TextReader?> DownloadAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri(options.Value.NflverseReleasesUrl, path), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return new StringReader(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private async Task RecordRunAsync(Func<ImportRun, Task> import)
    {
        var run = new ImportRun { Source = Source, StartedAt = clock.GetUtcNow() };
        db.ImportRuns.Add(run);
        try
        {
            await import(run);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or FormatException)
        {
            run.Error = ex.Message;
            logger.LogError(ex, "nflverse stats import failed");
        }
        finally
        {
            run.FinishedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }
}
