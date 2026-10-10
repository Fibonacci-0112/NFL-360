using System.Net;
using Microsoft.Extensions.DependencyInjection;
using NflCoverage.Modules.History.Contracts;
using NflCoverage.Modules.Stats.Contracts;

namespace NflCoverage.IntegrationTests;

/// <summary>Stats and History over the imported 2023 fixtures, checked against the real 2023 numbers.</summary>
[Collection(NflAppGroup.Name)]
public class StatsAndHistoryTests(NflAppFixture app)
{
    private const string Tua = "00-0036212";
    private const string Mahomes = "00-0033873";

    /// <summary>A 49ers fullback deliberately left out of the trimmed player directory fixture.</summary>
    private const string Juszczyk = "00-0029892";

    private readonly HttpClient _client = app.Factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Passing_yards_leader_is_tua_tagovailoa()
    {
        var leaders = await _client.GetFromJsonAsync<List<LeaderRow>>("/api/stats/2023/leaders/PassingYards?top=3", Ct);

        leaders.Should().HaveCount(3);
        leaders![0].Player.Name.Should().Be("Tua Tagovailoa");
        leaders[0].Team.Should().Be("MIA");
        leaders[0].Value.Should().Be(4624);
        leaders.Select(l => l.Rank).Should().Equal(1, 2, 3);
        leaders.Select(l => l.Value).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Passer_rating_ranks_qualified_passers_only()
    {
        var leaders = await _client.GetFromJsonAsync<List<LeaderRow>>("/api/stats/2023/leaders/passerrating?top=10", Ct);

        leaders![0].Player.Name.Should().Be("Brock Purdy");
        Math.Round(leaders[0].Value, 1).Should().Be(113.0);
        leaders.Should().OnlyContain(l => l.Stats.Passing.Attempts >= StatFormulas.PasserRatingAttemptsPerGame * 17);
    }

    [Theory]
    [InlineData("Sacks", "T.J. Watt", 19)]
    [InlineData("Tackles", "Bobby Wagner", 183)]
    [InlineData("RushingYards", "Christian McCaffrey", 1459)]
    [InlineData("ReceivingYards", "Tyreek Hill", 1799)]
    public async Task Category_leaders_match_2023(string category, string name, double value)
    {
        var leader = (await _client.GetFromJsonAsync<List<LeaderRow>>($"/api/stats/2023/leaders/{category}?top=1", Ct))!.Single();

        leader.Player.Name.Should().Be(name);
        leader.Value.Should().Be(value);
    }

    [Fact]
    public async Task Postseason_leaders_come_from_the_postseason_file()
    {
        var leader = (await _client.GetFromJsonAsync<List<LeaderRow>>("/api/stats/2023/leaders/PassingYards?type=POST&top=1", Ct))!.Single();

        leader.Player.Name.Should().Be("Patrick Mahomes");
        leader.Value.Should().Be(1051);
    }

    [Fact]
    public async Task Team_comparison_covers_all_32_teams_and_balances_offense_against_defense()
    {
        var teams = await _client.GetFromJsonAsync<List<TeamComparisonRow>>("/api/stats/2023/teams", Ct);

        teams.Should().HaveCount(32);
        teams!.Select(t => t.OffenseRank).Order().Should().Equal(Enumerable.Range(1, 32));
        teams.Select(t => t.DefenseRank).Order().Should().Equal(Enumerable.Range(1, 32));

        // Every offensive snap is some defense's snap, so league-wide totals must agree.
        teams.Sum(t => t.OffensePlays).Should().Be(teams.Sum(t => t.DefensePlays));
        teams.Sum(t => t.OffenseEpaPerPlay * t.OffensePlays).Should().BeApproximately(teams.Sum(t => t.DefenseEpaPerPlay * t.DefensePlays), 1e-6);
        teams.Sum(t => t.Giveaways).Should().Be(teams.Sum(t => t.Takeaways));
        teams.Should().OnlyContain(t => t.Games == 17);
    }

    [Fact]
    public async Task Team_season_and_roster_leaders()
    {
        var season = await _client.GetFromJsonAsync<TeamSeasonStats>("/api/teams/kc/stats/2023", Ct);
        season!.Games.Should().Be(17);
        season.Offense.Plays.Should().BeGreaterThan(900);

        var leaders = await _client.GetFromJsonAsync<List<TeamLeader>>("/api/teams/KC/stats/2023/leaders", Ct);
        leaders!.ToDictionary(l => l.Category, l => l.Leader.Player.Name).Should().Contain(new Dictionary<LeaderCategory, string>
        {
            [LeaderCategory.PassingYards] = "Patrick Mahomes",
            [LeaderCategory.RushingYards] = "Isiah Pacheco",
            [LeaderCategory.ReceivingYards] = "Travis Kelce",
            [LeaderCategory.Sacks] = "Chris Jones",
            [LeaderCategory.FgMade] = "Harrison Butker",
        });
    }

    [Fact]
    public async Task Player_profile_career_and_game_log()
    {
        var player = await _client.GetFromJsonAsync<PlayerProfile>($"/api/players/{Mahomes}", Ct);
        player!.Name.Should().Be("Patrick Mahomes");
        player.BirthDate.Should().Be(new DateOnly(1995, 9, 17));
        (player.DraftYear, player.DraftRound, player.DraftPick).Should().Be((2017, 1, 10));

        var career = await _client.GetFromJsonAsync<List<PlayerSeasonStats>>($"/api/players/{Tua}/career", Ct);
        career!.Select(c => (c.Season, c.SeasonType)).Should().Equal((2023, SeasonType.Regular), (2023, SeasonType.Postseason));
        career![0].Stats.Passing.Yards.Should().Be(4624);

        var games = await _client.GetFromJsonAsync<List<PlayerGameStats>>($"/api/players/{Mahomes}/games/2023", Ct);
        games.Should().HaveCount(20);
        games!.Count(g => g.SeasonType == SeasonType.Postseason).Should().Be(4);
        games[^1].GameId.Should().Be("2023_22_SF_KC", "the log ends with the Super Bowl");
        games.Where(g => g.SeasonType == SeasonType.Regular).Sum(g => g.Stats.Passing.Yards).Should().Be(4183);
    }

    [Fact]
    public async Task A_player_missing_from_the_directory_still_gets_a_profile_from_the_stats_row()
    {
        var player = await _client.GetFromJsonAsync<PlayerProfile>($"/api/players/{Juszczyk}", Ct);

        player!.Name.Should().Be("Kyle Juszczyk");
        player.LatestTeam.Should().Be("SF");
    }

    [Fact]
    public async Task Player_search_is_case_insensitive_and_partial()
    {
        var results = await _client.GetFromJsonAsync<List<PlayerSummary>>("/api/players?q=tagov", Ct);
        results.Should().ContainSingle().Which.Id.Should().Be(Tua);

        (await _client.GetFromJsonAsync<List<PlayerSummary>>("/api/players?q=t", Ct)).Should().BeEmpty("one letter is too short to search");
        (await _client.GetFromJsonAsync<List<PlayerSummary>>("/api/players?q=%25", Ct)).Should().BeEmpty("wildcards are matched literally");
    }

    [Fact]
    public async Task Reimporting_statistics_replaces_rather_than_duplicates()
    {
        await using var scope = app.Factory.Services.CreateAsyncScope();
        var stats = scope.ServiceProvider.GetRequiredService<IStatsIngestion>();
        var before = await stats.GetCoverageAsync(Ct);

        await app.ImportStatsFixtureAsync();

        (await stats.GetCoverageAsync(Ct)).Should().Equal(before);
        // The postseason file has one row without a player id, which is skipped.
        before.Should().ContainSingle().Which.Should().Be(new StatsCoverage(2023, PlayerSeasons: 260 + 217, TeamSeasons: 32 + 14, TeamGames: 570, PlayerGames: 76));
    }

    [Fact]
    public async Task Champions_include_the_2023_super_bowl()
    {
        var champions = await _client.GetFromJsonAsync<List<SuperBowlResult>>("/api/history/champions", Ct);

        champions.Should().ContainSingle().Which.Should().Be(new SuperBowlResult(2023, "2023_22_SF_KC", "KC", "SF", 25, 22));
    }

    [Fact]
    public async Task Head_to_head_totals_regular_season_and_playoff_meetings()
    {
        var series = await _client.GetFromJsonAsync<HeadToHead>("/api/history/head-to-head?a=KC&b=lv", Ct);

        series!.Overall.Should().Be(new SeriesRecord(WinsA: 1, WinsB: 1, Ties: 0));
        series.Playoffs.Games.Should().Be(0);
        (series.PointsA, series.PointsB).Should().Be((31 + 14, 17 + 20));
        series.Streak.Should().Be(new Streak("LV", 1));
        series.LastMeetings.Select(m => m.GameId).Should().Equal("2023_16_LV_KC", "2023_12_KC_LV");

        var playoff = await _client.GetFromJsonAsync<HeadToHead>("/api/history/head-to-head?a=BAL&b=KC", Ct);
        playoff!.Playoffs.Should().Be(new SeriesRecord(0, 1, 0));
        playoff.PlayoffMeetings.Should().ContainSingle().Which.Winner.Should().Be("KC");

        (await _client.GetAsync("/api/history/head-to-head?a=KC&b=KC", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Franchise_history_combines_results_playoffs_and_division_titles()
    {
        var history = await _client.GetFromJsonAsync<FranchiseHistory>("/api/history/teams/KC", Ct);

        history!.Seasons.Should().ContainSingle().Which.Should().Be(new FranchiseSeason(
            2023, Wins: 11, Losses: 6, Ties: 0, WinPct: 11 / 17.0, PointsFor: 371, PointsAgainst: 294, PlayoffRound.Champion,
            PlayoffWins: 4, PlayoffLosses: 0, DivisionTitle: true, Seed: 3));
        history.Totals.SuperBowlWins.Should().Be(1);
        history.Totals.ConferenceTitles.Should().Be(1);
    }

    [Fact]
    public async Task Season_summary_names_conference_champions_and_best_record()
    {
        var summary = await _client.GetFromJsonAsync<SeasonSummary>("/api/history/seasons/2023", Ct);

        (summary!.AfcChampion, summary.NfcChampion).Should().Be(("KC", "SF"));
        summary.BestRecordTeams.Should().Equal("BAL");
        (summary.BestWins, summary.BestLosses).Should().Be((13, 4));
    }

    [Theory]
    [InlineData("/stats/players/2023")]
    [InlineData("/stats/players/2023?category=PasserRating&type=POST")]
    [InlineData("/stats/teams/2023")]
    [InlineData("/players?q=mahomes")]
    [InlineData($"/players/{Mahomes}")]
    [InlineData($"/players/{Juszczyk}")]
    [InlineData("/history")]
    [InlineData("/history/teams/KC")]
    [InlineData("/history/head-to-head?a=KC&b=LV")]
    public async Task Stats_and_history_pages_render(string path)
    {
        var response = await _client.GetAsync(path, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("NFL 360");
    }

    [Fact]
    public async Task Leaders_page_lists_the_leader_and_pages_through_the_url()
    {
        var first = await _client.GetStringAsync("/stats/players/2023", Ct);
        first.Should().Contain("Tua Tagovailoa");

        var second = await _client.GetStringAsync("/stats/players/2023?page=2", Ct);
        second.Should().NotContain("Tua Tagovailoa");
    }
}
