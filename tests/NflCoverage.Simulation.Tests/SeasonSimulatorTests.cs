using NflCoverage.Rules;
using NflCoverage.Rules.Tests;

namespace NflCoverage.Simulation.Tests;

public class SeasonSimulatorTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void Same_seed_produces_identical_results()
    {
        var input = MidSeasonInput(2024, throughWeek: 9);

        var first = SeasonSimulator.Run(input, 500, seed: 42);
        var second = SeasonSimulator.Run(input, 500, seed: 42);

        second.Should().BeEquivalentTo(first);
    }

    [Fact]
    public void Probabilities_are_internally_consistent()
    {
        var input = MidSeasonInput(2024, throughWeek: 9);
        var result = SeasonSimulator.Run(input, 2_000, seed: 7);
        var conferenceOf = input.Teams.ToDictionary(t => t.Id, t => t.Conference);

        foreach (var conference in Enum.GetValues<Conference>())
        {
            var teams = result.Teams.Where(t => conferenceOf[t.TeamId] == conference).ToList();
            teams.Sum(t => t.MakePlayoffs).Should().BeApproximately(7, Tolerance);
            teams.Sum(t => t.WinDivision).Should().BeApproximately(4, Tolerance);
            teams.Sum(t => t.WinConference).Should().BeApproximately(1, Tolerance);
            for (var seed = 0; seed < 7; seed++)
            {
                teams.Sum(t => t.SeedProbabilities[seed]).Should().BeApproximately(1, Tolerance);
            }
        }

        result.Teams.Sum(t => t.WinSuperBowl).Should().BeApproximately(1, Tolerance);
        result.Teams.Sum(t => t.MeanWins).Should().BeApproximately(272, Tolerance, "each of the 272 games produces one win (or two half-wins)");
    }

    [Fact]
    public void Completed_season_reproduces_actual_seeds_with_certainty()
    {
        const int season = 2023;
        var input = MidSeasonInput(season, throughWeek: 18);
        var actual = StandingsCalculator.Compute(HistoricalData.LeagueFor(season), HistoricalData.RegularSeason(season), 7);

        var result = SeasonSimulator.Run(input, 50, seed: 1);

        foreach (var team in actual.Teams)
        {
            var odds = result.Teams.Single(t => t.TeamId == team.TeamId);
            odds.MakePlayoffs.Should().Be(team.Seed is null ? 0 : 1, team.TeamId);
            if (team.Seed is { } seed)
            {
                odds.SeedProbabilities[seed - 1].Should().Be(1, team.TeamId);
            }
        }
    }

    [Fact]
    public void Forcing_a_team_to_lose_every_remaining_game_lowers_its_odds()
    {
        var input = MidSeasonInput(2024, throughWeek: 9);
        var baseline = SeasonSimulator.Run(input, 2_000, seed: 3).Teams.Single(t => t.TeamId == "KC");

        var forced = input with
        {
            ForcedOutcomes = input.RemainingGames
                .Where(g => g.HomeTeam == "KC" || g.AwayTeam == "KC")
                .ToDictionary(g => g.Id, g => g.HomeTeam == "KC" ? ForcedOutcome.AwayWin : ForcedOutcome.HomeWin),
        };
        var whatIf = SeasonSimulator.Run(forced, 2_000, seed: 3).Teams.Single(t => t.TeamId == "KC");

        whatIf.MakePlayoffs.Should().BeLessThan(baseline.MakePlayoffs);
        whatIf.MeanWins.Should().BeApproximately(8, Tolerance, "KC was 8-0 through week 9 of 2024 and loses out");
    }

    [Fact]
    public void Merge_weights_parts_by_iterations()
    {
        var input = MidSeasonInput(2024, throughWeek: 9);
        var a = SeasonSimulator.Run(input, 300, seed: 1);
        var b = SeasonSimulator.Run(input, 100, seed: 2);

        var merged = SeasonSimulator.Merge([a, b]);

        merged.Iterations.Should().Be(400);
        merged.Teams[0].MakePlayoffs.Should().BeApproximately(((a.Teams[0].MakePlayoffs * 300) + (b.Teams[0].MakePlayoffs * 100)) / 400, Tolerance);
    }

    [Fact]
    public void Elo_update_is_zero_sum_and_rewards_upsets_more()
    {
        var favoriteWins = Elo.HomeRatingChange(1600, 1400, neutralSite: false, homeScore: 24, awayScore: 17);
        var upset = Elo.HomeRatingChange(1400, 1600, neutralSite: false, homeScore: 24, awayScore: 17);

        favoriteWins.Should().BePositive();
        upset.Should().BeGreaterThan(favoriteWins);
        Elo.HomeWinProbability(1500, 1500, neutralSite: true).Should().Be(0.5);
        Elo.HomeWinProbability(1500, 1500, neutralSite: false).Should().BeGreaterThan(0.5);
    }

    private static SimulationInput MidSeasonInput(int season, int throughWeek)
    {
        var league = HistoricalData.LeagueFor(season);
        var games = HistoricalData.Games.Where(g => g.Season == season && g.GameType == "REG").ToList();
        var ratings = Elo.BuildRatings(HistoricalData.Games
            .Where(g => g.Season < season && g.GameType == "REG")
            .Select((g, i) => new HistoricalGame(g.Season, i, new GameScore(g.Home, g.Away, g.HomeScore, g.AwayScore), g.Neutral)));

        return new SimulationInput(
            season,
            league.Teams,
            [.. games.Where(g => g.Week <= throughWeek).Select(g => new GameScore(g.Home, g.Away, g.HomeScore, g.AwayScore))],
            [.. games.Where(g => g.Week > throughWeek).Select((g, i) => new ScheduledGame($"{season}-{i}", g.Week, g.Home, g.Away, g.Neutral))],
            ratings);
    }
}
