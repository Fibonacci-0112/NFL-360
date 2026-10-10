using NflCoverage.Modules.History.Contracts;
using NflCoverage.Modules.League.Contracts;

namespace NflCoverage.Modules.History;

/// <summary>A team's final regular-season standing, from the Standings module.</summary>
internal readonly record struct DivisionFinish(bool DivisionTitle, int? Seed);

internal readonly record struct TeamRecord(int Wins, int Losses, int Ties, int PointsFor, int PointsAgainst);

/// <summary>Pure history computations over final game results.</summary>
internal static class HistoryCalculator
{
    /// <summary>nflverse postseason game types, in round order.</summary>
    private static readonly string[] Rounds = ["WC", "DIV", "CON", "SB"];

    /// <returns>0 (wild card) to 3 (Super Bowl), or -1 for a regular-season game.</returns>
    public static int RoundIndex(string gameType) => Array.IndexOf(Rounds, gameType);

    /// <summary>
    /// The deepest round a team reached: the round of its last playoff game, or the next round if it won that game
    /// (it is still alive, or was a pre-2002 bye team that started in the divisional round).
    /// </summary>
    public static PlayoffRound PlayoffRoundReached(string team, IEnumerable<GameDto> postseasonGames)
    {
        GameDto? deepest = null;
        var depth = -1;
        foreach (var game in postseasonGames.Where(g => Involves(g, team)))
        {
            var round = RoundIndex(game.GameType);
            if (round > depth)
            {
                depth = round;
                deepest = game;
            }
        }

        if (deepest is null)
        {
            return PlayoffRound.None;
        }

        var reached = (PlayoffRound)(depth + 1);
        return Winner(deepest) == team ? reached + 1 : reached;
    }

    public static IReadOnlyList<FranchiseSeason> FranchiseSeasons(
        string team,
        IEnumerable<GameDto> finals,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, DivisionFinish>> divisions)
    {
        var seasons = new List<FranchiseSeason>();
        foreach (var season in finals.Where(g => Involves(g, team)).GroupBy(g => g.Season).OrderBy(g => g.Key))
        {
            var regular = Record(team, season.Where(g => g.GameType == GameTypes.Regular));
            var postseason = season.Where(g => GameTypes.IsPostseason(g.GameType)).ToList();
            var playoffs = Record(team, postseason);
            DivisionFinish? finish = divisions.TryGetValue(season.Key, out var finishes) && finishes.TryGetValue(team, out var f) ? f : null;

            seasons.Add(new FranchiseSeason(
                season.Key,
                regular.Wins,
                regular.Losses,
                regular.Ties,
                WinPct(regular.Wins, regular.Losses, regular.Ties),
                regular.PointsFor,
                regular.PointsAgainst,
                PlayoffRoundReached(team, postseason),
                playoffs.Wins,
                playoffs.Losses,
                finish?.DivisionTitle,
                finish?.Seed));
        }

        return seasons;
    }

    public static FranchiseTotals Totals(IReadOnlyList<FranchiseSeason> seasons)
    {
        var wins = seasons.Sum(s => s.Wins);
        var losses = seasons.Sum(s => s.Losses);
        var ties = seasons.Sum(s => s.Ties);
        return new FranchiseTotals(
            seasons.Count,
            wins,
            losses,
            ties,
            WinPct(wins, losses, ties),
            seasons.Count(s => s.Playoffs != PlayoffRound.None),
            seasons.Sum(s => s.PlayoffWins),
            seasons.Sum(s => s.PlayoffLosses),
            seasons.Count(s => s.DivisionTitle == true),
            seasons.Count(s => s.Playoffs >= PlayoffRound.SuperBowl),
            seasons.Count(s => s.Playoffs == PlayoffRound.Champion));
    }

    public static HeadToHead HeadToHead(string teamA, string teamB, IEnumerable<GameDto> finals, int lastMeetings = 10)
    {
        var meetings = finals
            .Where(g => (g.HomeTeam == teamA && g.AwayTeam == teamB) || (g.HomeTeam == teamB && g.AwayTeam == teamA))
            .OrderBy(g => g.Season).ThenBy(g => RoundIndex(g.GameType)).ThenBy(g => g.Week).ThenBy(g => g.Kickoff)
            .Select(ToMeeting)
            .ToList();

        var (pointsA, pointsB) = meetings.Aggregate((A: 0, B: 0), (sum, m) => m.HomeTeam == teamA
            ? (sum.A + m.HomeScore, sum.B + m.AwayScore)
            : (sum.A + m.AwayScore, sum.B + m.HomeScore));
        var newestFirst = Enumerable.Reverse(meetings).ToList();

        return new HeadToHead(
            teamA,
            teamB,
            Series(teamA, teamB, meetings),
            Series(teamA, teamB, meetings.Where(m => m.GameType == GameTypes.Regular)),
            Series(teamA, teamB, meetings.Where(m => GameTypes.IsPostseason(m.GameType))),
            pointsA,
            pointsB,
            CurrentStreak(newestFirst),
            [.. newestFirst.Take(lastMeetings)],
            [.. newestFirst.Where(m => GameTypes.IsPostseason(m.GameType))]);
    }

    /// <summary>Consecutive results by the same winner (or consecutive ties), counting back from the latest meeting.</summary>
    public static Streak? CurrentStreak(IReadOnlyList<Meeting> newestFirst)
    {
        if (newestFirst.Count == 0)
        {
            return null;
        }

        var team = newestFirst[0].Winner;
        return new Streak(team, newestFirst.TakeWhile(m => m.Winner == team).Count());
    }

    /// <returns>Latest season first.</returns>
    public static IReadOnlyList<SeasonSummary> SeasonSummaries(IEnumerable<GameDto> finals, IReadOnlyDictionary<string, TeamDto> teams)
    {
        var summaries = new List<SeasonSummary>();
        foreach (var season in finals.GroupBy(g => g.Season).OrderByDescending(g => g.Key))
        {
            var superBowl = season.FirstOrDefault(g => g.GameType == "SB") is { } sb && Winner(sb) is { } champion
                ? new SuperBowlResult(
                    season.Key, sb.Id, champion, champion == sb.HomeTeam ? sb.AwayTeam : sb.HomeTeam,
                    Math.Max(sb.HomeScore!.Value, sb.AwayScore!.Value), Math.Min(sb.HomeScore!.Value, sb.AwayScore!.Value))
                : null;
            var conferenceChampions = season.Where(g => g.GameType == "CON").Select(Winner).OfType<string>().ToList();

            var records = season.Where(g => g.GameType == GameTypes.Regular)
                .SelectMany(g => new[] { g.HomeTeam, g.AwayTeam })
                .Distinct()
                .Select(team => (Team: team, Record: Record(team, season.Where(g => g.GameType == GameTypes.Regular))))
                .Select(r => (r.Team, r.Record, Pct: WinPct(r.Record.Wins, r.Record.Losses, r.Record.Ties)))
                .ToList();
            var bestPct = records.Count == 0 ? 0 : records.Max(r => r.Pct);
            var best = records.Where(r => r.Pct == bestPct).OrderByDescending(r => r.Record.Wins).ThenBy(r => r.Team).ToList();

            summaries.Add(new SeasonSummary(
                season.Key,
                superBowl,
                conferenceChampions.FirstOrDefault(t => teams.GetValueOrDefault(t)?.Conference == "AFC"),
                conferenceChampions.FirstOrDefault(t => teams.GetValueOrDefault(t)?.Conference == "NFC"),
                [.. best.Select(b => b.Team)],
                best.FirstOrDefault().Record.Wins,
                best.FirstOrDefault().Record.Losses,
                best.FirstOrDefault().Record.Ties));
        }

        return summaries;
    }

    public static double WinPct(int wins, int losses, int ties)
    {
        var games = wins + losses + ties;
        return games == 0 ? 0 : (wins + (ties / 2.0)) / games;
    }

    private static SeriesRecord Series(string teamA, string teamB, IEnumerable<Meeting> meetings)
    {
        var list = meetings.ToList();
        return new SeriesRecord(list.Count(m => m.Winner == teamA), list.Count(m => m.Winner == teamB), list.Count(m => m.Winner is null));
    }

    private static TeamRecord Record(string team, IEnumerable<GameDto> games)
    {
        var record = default(TeamRecord);
        foreach (var game in games.Where(g => Involves(g, team) && g.HomeScore is not null && g.AwayScore is not null))
        {
            var (us, them) = game.HomeTeam == team ? (game.HomeScore!.Value, game.AwayScore!.Value) : (game.AwayScore!.Value, game.HomeScore!.Value);
            record = record with
            {
                Wins = record.Wins + (us > them ? 1 : 0),
                Losses = record.Losses + (us < them ? 1 : 0),
                Ties = record.Ties + (us == them ? 1 : 0),
                PointsFor = record.PointsFor + us,
                PointsAgainst = record.PointsAgainst + them,
            };
        }

        return record;
    }

    private static Meeting ToMeeting(GameDto g) =>
        new(g.Id, g.Season, g.GameType, g.Week, g.Kickoff, g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value, g.NeutralSite);

    private static bool Involves(GameDto game, string team) => game.HomeTeam == team || game.AwayTeam == team;

    private static string? Winner(GameDto game) =>
        game.HomeScore > game.AwayScore ? game.HomeTeam : game.AwayScore > game.HomeScore ? game.AwayTeam : null;
}
