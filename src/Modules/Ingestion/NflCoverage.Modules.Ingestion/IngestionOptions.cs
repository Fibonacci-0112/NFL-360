namespace NflCoverage.Modules.Ingestion;

public sealed class IngestionOptions
{
    public const string Section = "Ingestion";

    /// <summary>Turns all background polling off (tests, read-only replicas).</summary>
    public bool Enabled { get; set; } = true;

    public Uri NflverseGamesUrl { get; set; } = new("https://github.com/nflverse/nflverse-data/releases/download/schedules/games.csv");

    public Uri NflverseTeamsUrl { get; set; } = new("https://github.com/nflverse/nflverse-data/releases/download/teams/teams_colors_logos.csv");

    public TimeSpan NflverseRefreshInterval { get; set; } = TimeSpan.FromHours(6);

    public Uri EspnScoreboardUrl { get; set; } = new("https://site.api.espn.com/apis/site/v2/sports/football/nfl/scoreboard");

    /// <summary>Poll interval while games are live or about to start.</summary>
    public TimeSpan EspnLivePollInterval { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Check interval when no game is near kickoff.</summary>
    public TimeSpan EspnIdlePollInterval { get; set; } = TimeSpan.FromMinutes(10);
}
