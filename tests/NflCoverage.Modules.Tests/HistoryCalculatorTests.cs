using NflCoverage.Modules.History;
using NflCoverage.Modules.History.Contracts;
using NflCoverage.Modules.League.Contracts;

namespace NflCoverage.Modules.Tests;

/// <summary>History computations on small synthetic schedules.</summary>
public class HistoryCalculatorTests
{
    private static int _gameNumber;

    [Fact]
    public void A_team_without_playoff_games_did_not_make_the_playoffs() =>
        HistoryCalculator.PlayoffRoundReached("KC", [Game(2023, "WC", 19, "BUF", "PIT", 31, 17)]).Should().Be(PlayoffRound.None);

    [Fact]
    public void Losing_a_round_means_reaching_that_round() =>
        HistoryCalculator.PlayoffRoundReached("PIT", [Game(2023, "WC", 19, "BUF", "PIT", 31, 17)]).Should().Be(PlayoffRound.WildCard);

    [Fact]
    public void The_round_reached_is_the_deepest_one_played()
    {
        GameDto[] postseason =
        [
            Game(2023, "WC", 19, "KC", "MIA", 26, 7),
            Game(2023, "DIV", 20, "BUF", "KC", 24, 27),
            Game(2023, "CON", 21, "BAL", "KC", 10, 17),
            Game(2023, "SB", 22, "SF", "KC", 22, 25, neutral: true),
        ];

        HistoryCalculator.PlayoffRoundReached("KC", postseason).Should().Be(PlayoffRound.Champion);
        HistoryCalculator.PlayoffRoundReached("SF", postseason).Should().Be(PlayoffRound.SuperBowl);
        HistoryCalculator.PlayoffRoundReached("BAL", postseason).Should().Be(PlayoffRound.Conference);
        HistoryCalculator.PlayoffRoundReached("BUF", postseason).Should().Be(PlayoffRound.Divisional);
        HistoryCalculator.PlayoffRoundReached("MIA", postseason).Should().Be(PlayoffRound.WildCard);
    }

    [Fact]
    public void A_bye_team_that_loses_its_first_game_reached_the_divisional_round() =>
        HistoryCalculator.PlayoffRoundReached("TEN", [Game(2000, "DIV", 19, "TEN", "BAL", 10, 24)]).Should().Be(PlayoffRound.Divisional);

    [Fact]
    public void A_team_still_alive_has_reached_the_next_round() =>
        HistoryCalculator.PlayoffRoundReached("DET", [Game(2023, "WC", 19, "DET", "LA", 24, 23)]).Should().Be(PlayoffRound.Divisional);

    [Fact]
    public void Franchise_seasons_count_regular_season_record_and_playoff_run_separately()
    {
        GameDto[] games =
        [
            Game(2022, "REG", 1, "KC", "LV", 30, 10),
            Game(2022, "REG", 2, "LV", "KC", 20, 17),
            Game(2022, "REG", 3, "KC", "DEN", 14, 14),
            Game(2022, "WC", 19, "KC", "MIA", 21, 20),
            Game(2022, "DIV", 20, "KC", "BUF", 10, 24),
            Game(2023, "REG", 1, "KC", "LV", 3, 0),
        ];
        var divisions = new Dictionary<int, IReadOnlyDictionary<string, DivisionFinish>>
        {
            [2022] = new Dictionary<string, DivisionFinish> { ["KC"] = new(DivisionTitle: true, Seed: 2) },
        };

        var seasons = HistoryCalculator.FranchiseSeasons("KC", games, divisions);

        seasons.Should().HaveCount(2);
        seasons[0].Should().Be(new FranchiseSeason(
            2022, Wins: 1, Losses: 1, Ties: 1, WinPct: 0.5, PointsFor: 61, PointsAgainst: 44, PlayoffRound.Divisional,
            PlayoffWins: 1, PlayoffLosses: 1, DivisionTitle: true, Seed: 2));
        seasons[1].DivisionTitle.Should().BeNull("2023 standings were not supplied, e.g. an unfinished regular season");

        var totals = HistoryCalculator.Totals(seasons);
        totals.Should().Be(new FranchiseTotals(
            Seasons: 2, Wins: 2, Losses: 1, Ties: 1, WinPct: 0.625, PlayoffAppearances: 1, PlayoffWins: 1, PlayoffLosses: 1,
            DivisionTitles: 1, ConferenceTitles: 0, SuperBowlWins: 0));
    }

    [Fact]
    public void Head_to_head_splits_regular_season_and_playoffs_and_lists_meetings_newest_first()
    {
        GameDto[] games =
        [
            Game(2021, "REG", 5, "KC", "BUF", 20, 38),
            Game(2021, "DIV", 20, "KC", "BUF", 42, 36),
            Game(2022, "REG", 6, "KC", "BUF", 20, 24),
            Game(2023, "REG", 14, "KC", "BUF", 17, 20),
            Game(2023, "DIV", 20, "BUF", "KC", 24, 27),
            Game(2023, "REG", 1, "KC", "DET", 20, 21),
        ];

        var series = HistoryCalculator.HeadToHead("BUF", "KC", games, lastMeetings: 3);

        series.Overall.Should().Be(new SeriesRecord(WinsA: 3, WinsB: 2, Ties: 0));
        series.Regular.Should().Be(new SeriesRecord(3, 0, 0));
        series.Playoffs.Should().Be(new SeriesRecord(0, 2, 0));
        series.PointsA.Should().Be(38 + 36 + 24 + 20 + 24);
        series.PointsB.Should().Be(20 + 42 + 20 + 17 + 27);
        series.Streak.Should().Be(new Streak("KC", 1));
        series.LastMeetings.Select(m => (m.Season, m.GameType)).Should().Equal((2023, "DIV"), (2023, "REG"), (2022, "REG"));
        series.PlayoffMeetings.Select(m => m.Season).Should().Equal(2023, 2021);
    }

    [Fact]
    public void Streak_counts_consecutive_wins_by_the_latest_winner_and_treats_ties_as_their_own_streak()
    {
        Meeting[] newestFirst = [Meeting("A", 21, 3), Meeting("A", 30, 0), Meeting("B", 7, 10), Meeting("A", 1, 0)];
        HistoryCalculator.CurrentStreak(newestFirst).Should().Be(new Streak("A", 2));

        Meeting[] tied = [Meeting("A", 10, 10), Meeting("A", 3, 3), Meeting("A", 7, 0)];
        HistoryCalculator.CurrentStreak(tied).Should().Be(new Streak(null, 2));

        HistoryCalculator.CurrentStreak([]).Should().BeNull();
    }

    [Fact]
    public void Season_summary_names_the_champion_conference_champions_and_best_records()
    {
        GameDto[] games =
        [
            Game(2023, "REG", 1, "BAL", "HOU", 25, 9),
            Game(2023, "REG", 2, "SF", "LA", 30, 23),
            Game(2023, "REG", 3, "KC", "CHI", 41, 10),
            Game(2023, "CON", 21, "BAL", "KC", 10, 17),
            Game(2023, "CON", 21, "SF", "DET", 34, 31),
            Game(2023, "SB", 22, "SF", "KC", 22, 25, neutral: true),
        ];
        var teams = new[] { ("BAL", "AFC"), ("KC", "AFC"), ("HOU", "AFC"), ("SF", "NFC"), ("DET", "NFC"), ("LA", "NFC"), ("CHI", "NFC") }
            .ToDictionary(t => t.Item1, t => new TeamDto(t.Item1, t.Item1, t.Item1, t.Item2, $"{t.Item2} West", "#000", "#fff", null));

        var summary = HistoryCalculator.SeasonSummaries(games, teams).Should().ContainSingle().Subject;

        summary.SuperBowl.Should().Be(new SuperBowlResult(2023, games[^1].Id, "KC", "SF", 25, 22));
        summary.AfcChampion.Should().Be("KC");
        summary.NfcChampion.Should().Be("SF");
        summary.BestRecordTeams.Should().Equal("BAL", "KC", "SF");
        (summary.BestWins, summary.BestLosses, summary.BestTies).Should().Be((1, 0, 0));
    }

    private static Meeting Meeting(string home, int homeScore, int awayScore) =>
        new($"g{++_gameNumber}", 2020, "REG", 1, null, home, "B", homeScore, awayScore, false);

    private static GameDto Game(int season, string type, int week, string home, string away, int homeScore, int awayScore, bool neutral = false) =>
        new($"{season}_{week:00}_{away}_{home}_{++_gameNumber}", season, type, week, null, home, away, homeScore, awayScore, GameStatus.Final, "Final", neutral, null);
}
