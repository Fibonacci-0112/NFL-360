using NflCoverage.Modules.Ingestion;
using NflCoverage.Modules.Ingestion.Espn;
using NflCoverage.Modules.Ingestion.Nflverse;
using NflCoverage.Modules.League.Contracts;

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
}
