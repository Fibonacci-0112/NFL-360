namespace NflCoverage.Modules.Ingestion;

public sealed class IngestionOptions
{
    public const string Section = "Ingestion";

    /// <summary>Turns all background polling off (tests, read-only replicas).</summary>
    public bool Enabled { get; set; } = true;

    public Uri NflverseGamesUrl { get; set; } = new("https://github.com/nflverse/nflverse-data/releases/download/schedules/games.csv");

    public Uri NflverseTeamsUrl { get; set; } = new("https://github.com/nflverse/nflverse-data/releases/download/teams/teams_colors_logos.csv");

    public TimeSpan NflverseRefreshInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Base of the nflverse release assets; statistics files are resolved relative to it.</summary>
    public Uri NflverseReleasesUrl { get; set; } = new("https://github.com/nflverse/nflverse-data/releases/download/");

    /// <summary>Imports player and team statistics (on top of <see cref="Enabled"/>).</summary>
    public bool StatsEnabled { get; set; } = true;

    /// <summary>First season to backfill statistics for; nflverse publishes them from 1999.</summary>
    public int StatsFirstSeason { get; set; } = 1999;

    /// <summary>
    /// How many recent seasons keep player game logs (about 20,000 rows each). Raise it to backfill older logs.
    /// </summary>
    public int StatsGameLogSeasons { get; set; } = 2;

    public TimeSpan StatsRefreshInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Wait after a week's last game before refreshing, so nflverse has published and corrections coalesce.</summary>
    public TimeSpan StatsRefreshDebounce { get; set; } = TimeSpan.FromMinutes(30);

    public Uri EspnScoreboardUrl { get; set; } = new("https://site.api.espn.com/apis/site/v2/sports/football/nfl/scoreboard");

    /// <summary>Poll interval while games are live or about to start.</summary>
    public TimeSpan EspnLivePollInterval { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Check interval when no game is near kickoff.</summary>
    public TimeSpan EspnIdlePollInterval { get; set; } = TimeSpan.FromMinutes(10);
}
