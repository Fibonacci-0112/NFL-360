using NflCoverage.Modules.Stats.Contracts;

namespace NflCoverage.Modules.Tests;

public class StatFormulasTests
{
    [Theory]
    [InlineData(336, 497, 4557, 49, 10, 121.1)] // Peyton Manning, 2004
    [InlineData(388, 560, 4624, 29, 14, 101.1)] // Tua Tagovailoa, 2023
    [InlineData(308, 444, 4280, 31, 11, 113.0)] // Brock Purdy, 2023
    [InlineData(401, 597, 4183, 27, 14, 92.6)] // Patrick Mahomes, 2023
    public void Passer_rating_matches_official_figures(int completions, int attempts, int yards, int touchdowns, int interceptions, double expected) =>
        Math.Round(StatFormulas.PasserRating(completions, attempts, yards, touchdowns, interceptions), 1).Should().Be(expected);

    [Fact]
    public void Passer_rating_components_are_capped_so_a_perfect_game_is_158_3() =>
        Math.Round(StatFormulas.PasserRating(20, 20, 400, 6, 0), 1).Should().Be(158.3);

    [Fact]
    public void Passer_rating_components_have_a_floor_of_zero() =>
        StatFormulas.PasserRating(0, 10, 0, 0, 5).Should().Be(0);

    [Fact]
    public void Passer_rating_without_attempts_is_zero() =>
        StatFormulas.PasserRating(0, 0, 0, 0, 0).Should().Be(0);

    [Fact]
    public void Ratios_are_undefined_without_a_denominator()
    {
        StatFormulas.YardsPerAttempt(4624, 560).Should().BeApproximately(8.257, 0.001);
        StatFormulas.YardsPerCarry(1459, 272).Should().BeApproximately(5.364, 0.001);
        StatFormulas.CatchRate(119, 171).Should().BeApproximately(0.696, 0.001);
        StatFormulas.YardsPerAttempt(0, 0).Should().BeNull();
        StatFormulas.CatchRate(0, 0).Should().BeNull();
        StatFormulas.EpaPerPlay(5, 0).Should().BeNull();
    }

    [Fact]
    public void Plays_count_dropbacks_and_carries() =>
        StatFormulas.Plays(passAttempts: 35, sacks: 3, carries: 25).Should().Be(63);

    [Fact]
    public void Epa_per_play_divides_total_epa_by_plays() =>
        StatFormulas.EpaPerPlay(12.5, 50).Should().Be(0.25);

    [Fact]
    public void Defensive_epa_per_play_pools_the_opponents_plays_rather_than_averaging_games() =>
        StatFormulas.DefensiveEpaPerPlay([(OpponentEpa: 10, OpponentPlays: 50), (OpponentEpa: -20, OpponentPlays: 50)]).Should().Be(-0.1);

    [Fact]
    public void Team_offense_derives_plays_yards_turnovers_and_epa()
    {
        var offense = new TeamOffense(
            PassAttempts: 30, Completions: 20, PassingYards: 250, PassingTds: 2, Interceptions: 1, SacksTaken: 2, Carries: 25,
            RushingYards: 110, RushingTds: 1, FumblesLost: 1, PassingEpa: 6.5, RushingEpa: -1.5);

        offense.Plays.Should().Be(57);
        offense.Yards.Should().Be(360);
        offense.Turnovers.Should().Be(2);
        offense.Epa.Should().Be(5);
    }
}
