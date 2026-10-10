namespace NflCoverage.Rules.Tests;

/// <summary>Small hand-built leagues that isolate individual tiebreaking rules.</summary>
public class TiebreakerScenarioTests
{
    // Two conferences x two divisions x three clubs keeps scenarios readable.
    private static readonly League League = Rules.League.Create(
    [
        new Team("A1", Conference.Afc, "AFC A"), new Team("A2", Conference.Afc, "AFC A"), new Team("A3", Conference.Afc, "AFC A"),
        new Team("B1", Conference.Afc, "AFC B"), new Team("B2", Conference.Afc, "AFC B"), new Team("B3", Conference.Afc, "AFC B"),
        new Team("C1", Conference.Nfc, "NFC C"), new Team("C2", Conference.Nfc, "NFC C"), new Team("C3", Conference.Nfc, "NFC C"),
        new Team("D1", Conference.Nfc, "NFC D"), new Team("D2", Conference.Nfc, "NFC D"), new Team("D3", Conference.Nfc, "NFC D"),
    ]);

    private static GameScore Win(string winner, string loser) => new(winner, loser, 24, 17);

    [Fact]
    public void Tied_game_counts_as_half_a_win()
    {
        var standings = StandingsCalculator.Compute(League, [new GameScore("A1", "A2", 20, 20), Win("A1", "A3")], 2);

        var a1 = standings.Teams.Single(t => t.TeamId == "A1");
        a1.WinPct.Should().Be(0.75);
        a1.DivisionRecord.Should().Be("1-0-1");
    }

    [Fact]
    public void Division_tie_is_broken_by_head_to_head()
    {
        // A1 and A2 both 2-1; A2 beat A1.
        GameScore[] games = [Win("A2", "A1"), Win("A1", "A3"), Win("A1", "B1"), Win("A2", "B2"), Win("B3", "A2")];

        var standings = StandingsCalculator.Compute(League, games, 2);

        standings.Division("AFC A").Select(t => t.TeamId).Should().StartWith(["A2", "A1"]);
        standings.Notes.Should().Contain(n => n.Context == "AFC A" && n.Winner == "A2" && n.DecidedBy == "Head-to-head");
    }

    [Fact]
    public void Wild_card_tie_keeps_only_top_club_per_division_then_uses_head_to_head()
    {
        // C2, D2 and D3 all finish 2-1. D2 and D3 share a division, so only D2 (who beat D3) takes part;
        // C2 then beats D2 on head-to-head for the last wild card.
        GameScore[] games =
        [
            Win("C1", "C3"), Win("C1", "C2"), Win("C1", "B3"),
            Win("C2", "D2"), Win("C2", "C3"),
            Win("D1", "A1"), Win("D1", "B2"), Win("D1", "A3"),
            Win("D2", "D3"), Win("D2", "B1"),
            Win("D3", "C3"), Win("D3", "A2"),
        ];

        var standings = StandingsCalculator.Compute(League, games, 3);

        standings.Seeds(Conference.Nfc).Select(t => t.TeamId).Should().Equal("C1", "D1", "C2");
        standings.Notes.Should().Contain(n => n.Context == "NFC wild card" && n.Winner == "C2" && n.DecidedBy == "Head-to-head");
    }

    [Fact]
    public void Unresolvable_tie_falls_through_to_coin_toss()
    {
        var standings = StandingsCalculator.Compute(League, [new GameScore("A1", "A2", 10, 10)], 1);

        standings.Notes.Should().Contain(n => n.Context == "AFC A" && n.DecidedBy == "Coin toss");
    }

    [Fact]
    public void Fast_seed_path_matches_full_computation()
    {
        for (var season = 2002; season <= 2025; season++)
        {
            var league = HistoricalData.LeagueFor(season);
            var table = SeasonTable.From(league, HistoricalData.RegularSeason(season));
            var playoffTeams = StandingsCalculator.PlayoffTeamsPerConference(season);

            var full = StandingsCalculator.Compute(table, playoffTeams);
            var fast = StandingsCalculator.ComputeSeeds(table, playoffTeams, CoinToss.Deterministic);

            foreach (var conference in Enum.GetValues<Conference>())
            {
                fast[(int)conference].Select(i => league.Teams[i].Id)
                    .Should().Equal(full.Seeds(conference).Select(s => s.TeamId), $"season {season} {conference}");
            }
        }
    }
}
