using System.Globalization;

namespace NflCoverage.Rules.Tests;

/// <summary>
/// nflverse schedule snapshot (2002-2025) used as a regression oracle: the engine must reproduce
/// the playoff field and bracket that actually happened, using only regular-season scores.
/// </summary>
internal static class HistoricalData
{
    public sealed record Game(int Season, string GameType, int Week, string Away, int AwayScore, string Home, int HomeScore, bool Neutral);

    private static readonly Lazy<IReadOnlyList<Game>> GamesLazy = new(LoadGames);
    private static readonly Lazy<IReadOnlyDictionary<string, (Conference Conference, string Division)>> AlignmentLazy = new(LoadAlignment);

    public static IReadOnlyList<Game> Games => GamesLazy.Value;

    public static League LeagueFor(int season)
    {
        var ids = Games.Where(g => g.Season == season && g.GameType == "REG")
            .SelectMany(g => new[] { g.Home, g.Away })
            .Distinct();
        return League.Create(ids.Select(id => new Team(id, AlignmentLazy.Value[id].Conference, AlignmentLazy.Value[id].Division)));
    }

    public static IEnumerable<GameScore> RegularSeason(int season) =>
        Games.Where(g => g.Season == season && g.GameType == "REG")
            .Select(g => new GameScore(g.Home, g.Away, g.HomeScore, g.AwayScore));

    private static IReadOnlyList<Game> LoadGames() =>
    [
        .. ReadCsv("games_2002_2025.csv").Select(r => new Game(
            int.Parse(r["season"], CultureInfo.InvariantCulture),
            r["game_type"],
            int.Parse(r["week"], CultureInfo.InvariantCulture),
            r["away_team"],
            int.Parse(r["away_score"], CultureInfo.InvariantCulture),
            r["home_team"],
            int.Parse(r["home_score"], CultureInfo.InvariantCulture),
            r["location"] == "Neutral")),
    ];

    private static IReadOnlyDictionary<string, (Conference, string)> LoadAlignment() =>
        ReadCsv("teams.csv").ToDictionary(
            r => r["team_abbr"],
            r => (r["team_conf"] == "AFC" ? Conference.Afc : Conference.Nfc, r["team_division"]));

    private static IEnumerable<Dictionary<string, string>> ReadCsv(string file)
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Data", file));
        var header = lines[0].Split(',');
        return lines.Skip(1).Select(line =>
        {
            var cells = line.Split(',');
            return header.Select((h, i) => (h, cells[i])).ToDictionary(x => x.h, x => x.Item2);
        });
    }
}
