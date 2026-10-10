using NflCoverage.Modules.Ingestion;
using NflCoverage.Modules.Ingestion.Espn;
using NflCoverage.Modules.Ingestion.Nflverse;
using NflCoverage.Modules.League.Contracts;
using NflCoverage.Modules.Stats.Contracts;

namespace NflCoverage.IntegrationTests;

/// <summary>Anti-corruption layer tests: provider formats are pinned by captured fixtures.</summary>
public class ProviderParsingTests
{
    [Fact]
    public void Nflverse_games_map_to_canonical_franchises_with_eastern_kickoffs()
    {
        var games = NflverseParser.ParseGames(new StringReader(File.ReadAllText(NflAppFixture.DataPath("games_2023.csv"))));

        games.Should().HaveCount(285);
        var opener = games.Single(g => g.Game.Id == "2023_01_DET_KC").Game;
        opener.Status.Should().Be(GameStatus.Final);
        opener.Kickoff.Should().Be(new DateTimeOffset(2023, 9, 8, 0, 20, 0, TimeSpan.Zero), "Thursday 8:20 PM ET");
        games.Should().OnlyContain(g => g.EspnId != null);
        games.Where(g => g.Game.GameType == "SB").Should().ContainSingle().Which.Game.NeutralSite.Should().BeTrue();
    }

    [Fact]
    public void Nflverse_teams_skip_historical_aliases()
    {
        var teams = NflverseParser.ParseTeams(new StringReader(File.ReadAllText(NflAppFixture.DataPath("teams_colors_logos.csv"))));

        teams.Should().HaveCount(32);
        teams.Select(t => t.Id).Should().NotContain(["OAK", "SD", "STL", "LAR"]);
    }

    [Theory]
    [InlineData("OAK", "LV")]
    [InlineData("SD", "LAC")]
    [InlineData("STL", "LA")]
    [InlineData("WSH", "WAS")]
    [InlineData("kc", "KC")]
    public void Franchise_aliases_map_to_current_ids(string code, string expected) =>
        FranchiseMap.ToFranchise(code).Should().Be(expected);

    [Fact]
    public void Espn_scoreboard_reports_only_started_games()
    {
        var scores = EspnScoreboardParser.Parse(File.ReadAllText(NflAppFixture.DataPath("espn_scoreboard.json")));

        // Captured during 2026 week 5 after Thursday night: TB 24 at DAL 16 was final, the rest not started.
        scores.Should().ContainSingle().Which.Should().Be(new EspnScore("401872980", 16, 24, GameStatus.Final, "Final"));
    }

    [Fact]
    public void Nflverse_player_seasons_keep_curated_columns_typed_and_the_rest_as_extras()
    {
        var rows = NflverseStatsParser.ParsePlayerSeasons(StatsFixture("stats_player_reg_2023.csv"));

        rows.Should().HaveCount(260);
        var tua = rows.Single(r => r.Name == "Tua Tagovailoa");
        tua.Stats.Should().BeEquivalentTo(new { PlayerId = "00-0036212", Season = 2023, SeasonType = SeasonType.Regular, Team = "MIA", Games = 17 });
        tua.Stats.Stats.Passing.Should().BeEquivalentTo(new { Completions = 388, Attempts = 560, Yards = 4624, Touchdowns = 29, Interceptions = 14 });
        tua.Stats.Stats.Passing.Cpoe.Should().NotBeNull();
        tua.Extra.Should().ContainKey("passing_air_yards").And.NotContainKey("passing_yards").And.NotContainKey("player_id");
        tua.Extra.Values.Should().NotContain(["", "0", "NA"]);

        rows.Single(r => r.Name == "Josh Hines-Allen").Stats.Stats.Defense.Sacks.Should().Be(17.5, "half sacks are kept");
        rows.Single(r => r.Name == "Bobby Wagner").Stats.Stats.Defense.Tackles.Should().Be(183, "solo + assisted is the official combined total");
    }

    [Fact]
    public void Nflverse_team_week_rows_link_to_schedule_game_ids()
    {
        var games = NflverseStatsParser.ParseTeamGames(StatsFixture("stats_team_week_2023.csv"));

        games.Should().HaveCount(570);
        games.Count(g => g.Stats.SeasonType == SeasonType.Postseason).Should().Be(26);
        var opener = games.Where(g => g.Stats.GameId == "2023_01_DET_KC").Select(g => g.Stats).ToList();
        opener.Select(g => (g.Team, g.Opponent)).Should().BeEquivalentTo([("KC", "DET"), ("DET", "KC")]);
        opener.Should().OnlyContain(g => g.Offense.Plays > 40);
    }

    [Fact]
    public void Nflverse_stats_map_historical_team_codes_and_skip_rows_without_a_team()
    {
        const string Csv = """
            season,week,team,season_type,game_id,opponent_team,attempts,carries,passing_epa
            2001,1,OAK,REG,2001_01_KC_OAK,KC,30,25,1.5
            2001,1,KC,REG,2001_01_KC_OAK,OAK,28,20,-2
            2001,1,,REG,2001_01_KC_OAK,OAK,0,0,
            """;

        var games = NflverseStatsParser.ParseTeamGames(new StringReader(Csv));

        games.Select(g => (g.Stats.Team, g.Stats.Opponent)).Should().Equal(("LV", "KC"), ("KC", "LV"));
        games[1].Stats.Offense.PassingEpa.Should().Be(-2);
    }

    [Fact]
    public void Nflverse_players_map_bio_and_draft_fields()
    {
        var players = NflverseStatsParser.ParsePlayers(StatsFixture("players.csv"));

        var mahomes = players.Single(p => p.Profile.Id == "00-0033873").Profile;
        mahomes.Should().Be(new PlayerProfile(
            "00-0033873", "Patrick Mahomes", "QB", "QB", mahomes.HeadshotUrl, new DateOnly(1995, 9, 17), "Texas Tech",
            2017, 1, 10, "KC", 2017, mahomes.LastSeason, "KC"));
        players.Single(p => p.Profile.Id == "00-0033873").Extra.Should().ContainKey("pfr_id");
    }

    private static StringReader StatsFixture(string file) => new(File.ReadAllText(NflAppFixture.StatsDataPath(file)));
}
