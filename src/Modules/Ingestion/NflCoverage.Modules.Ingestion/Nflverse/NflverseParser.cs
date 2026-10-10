using System.Globalization;
using nietras.SeparatedValues;
using NflCoverage.Modules.League.Contracts;

namespace NflCoverage.Modules.Ingestion.Nflverse;

/// <summary>A schedule row from nflverse <c>games.csv</c>, already mapped to canonical franchise ids.</summary>
internal sealed record NflverseGame(GameUpsert Game, string? EspnId);

/// <summary>Anti-corruption layer for nflverse CSV files: everything nflverse-specific stops here.</summary>
internal static class NflverseParser
{
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    public static List<NflverseGame> ParseGames(TextReader csv)
    {
        var games = new List<NflverseGame>();
        using var reader = Sep.Reader(o => o with { Unescape = true }).From(csv);
        foreach (var row in reader)
        {
            var homeScore = ParseInt(row["home_score"].ToString());
            var awayScore = ParseInt(row["away_score"].ToString());
            var final = homeScore is not null && awayScore is not null;
            var espn = row["espn"].ToString();

            games.Add(new NflverseGame(
                new GameUpsert(
                    Id: row["game_id"].ToString(),
                    Season: row["season"].Parse<int>(),
                    GameType: row["game_type"].ToString(),
                    Week: row["week"].Parse<int>(),
                    Kickoff: ParseKickoff(row["gameday"].ToString(), row["gametime"].ToString()),
                    HomeTeam: FranchiseMap.ToFranchise(row["home_team"].ToString()),
                    AwayTeam: FranchiseMap.ToFranchise(row["away_team"].ToString()),
                    HomeScore: homeScore,
                    AwayScore: awayScore,
                    Status: final ? GameStatus.Final : GameStatus.Scheduled,
                    NeutralSite: row["location"].ToString() == "Neutral",
                    HomeSpread: ParseDouble(row["spread_line"].ToString())),
                string.IsNullOrWhiteSpace(espn) || espn == "NA" ? null : espn));
        }

        return games;
    }

    public static List<TeamUpsert> ParseTeams(TextReader csv)
    {
        var teams = new List<TeamUpsert>();
        using var reader = Sep.Reader(o => o with { Unescape = true }).From(csv);
        foreach (var row in reader)
        {
            var id = row["team_abbr"].ToString();
            var conference = row["team_conf"].ToString();
            if (FranchiseMap.IsHistoricalAlias(id) || conference is not ("AFC" or "NFC"))
            {
                continue;
            }

            teams.Add(new TeamUpsert(
                id,
                row["team_name"].ToString(),
                row["team_nick"].ToString(),
                conference,
                row["team_division"].ToString(),
                row["team_color"].ToString(),
                row["team_color2"].ToString(),
                NullIfEmpty(row["team_logo_espn"].ToString())));
        }

        return teams;
    }

    private static DateTimeOffset? ParseKickoff(string day, string time)
    {
        if (!DateOnly.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return null;
        }

        var clock = TimeOnly.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : new TimeOnly(13, 0);
        var local = date.ToDateTime(clock);
        return new DateTimeOffset(local, Eastern.GetUtcOffset(local)).ToUniversalTime();
    }

    private static int? ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static double? ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) || value == "NA" ? null : value;
}
