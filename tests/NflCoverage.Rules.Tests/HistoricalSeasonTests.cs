namespace NflCoverage.Rules.Tests;

public class HistoricalSeasonTests
{
    public static TheoryData<int> Seasons => [.. Enumerable.Range(2002, 24)];

    [Theory]
    [MemberData(nameof(Seasons))]
    public void Playoff_field_matches_actual_postseason(int season)
    {
        var standings = Compute(season);
        var actual = Postseason(season).SelectMany(g => new[] { g.Home, g.Away }).ToHashSet();

        standings.Teams.Where(t => t.Seed is not null).Select(t => t.TeamId)
            .Should().BeEquivalentTo(actual, $"the {season} playoff field should be reproduced from regular-season results");
    }

    [Theory]
    [MemberData(nameof(Seasons))]
    public void Wild_card_matchups_and_byes_match_actual_bracket(int season)
    {
        var seed = Compute(season).Teams.Where(t => t.Seed is not null).ToDictionary(t => t.TeamId, t => t.Seed!.Value);
        var wildCardGames = Postseason(season).Where(g => g.GameType == "WC").ToList();

        foreach (var game in wildCardGames)
        {
            (seed[game.Home] + seed[game.Away]).Should().Be(9, $"{season} WC {game.Away}@{game.Home} pairs seeds that sum to 9");
            if (!game.Neutral)
            {
                seed[game.Home].Should().BeLessThan(seed[game.Away], $"{season} WC {game.Away}@{game.Home}: higher seed hosts");
            }
        }

        var playingWildCard = wildCardGames.SelectMany(g => new[] { g.Home, g.Away }).ToHashSet();
        var byes = seed.Where(s => !playingWildCard.Contains(s.Key)).Select(s => s.Value).Distinct().Order();
        byes.Should().Equal(season >= 2020 ? [1] : [1, 2]);
    }

    [Theory]
    [MemberData(nameof(Seasons))]
    public void Top_seed_hosts_lowest_remaining_seed_in_divisional_round(int season)
    {
        var standings = Compute(season);
        var postseason = Postseason(season).ToList();

        foreach (var conference in Enum.GetValues<Conference>())
        {
            var seeds = standings.Seeds(conference);
            var topSeed = seeds[0].TeamId;
            var seedOf = seeds.ToDictionary(s => s.TeamId, s => s.Seed!.Value);
            var wildCardWinners = postseason.Where(g => g.GameType == "WC" && seedOf.ContainsKey(g.Home))
                .Select(g => g.HomeScore > g.AwayScore ? g.Home : g.Away);
            var lowestRemaining = wildCardWinners.MaxBy(t => seedOf[t]);

            var topSeedGame = postseason.Single(g => g.GameType == "DIV" && (g.Home == topSeed || g.Away == topSeed));
            new[] { topSeedGame.Home, topSeedGame.Away }.Should().Contain(lowestRemaining!,
                $"{season} {conference}: #1 seed {topSeed} faces the lowest remaining seed");
        }
    }

    private static LeagueStandings Compute(int season) =>
        StandingsCalculator.Compute(
            HistoricalData.LeagueFor(season),
            HistoricalData.RegularSeason(season),
            StandingsCalculator.PlayoffTeamsPerConference(season));

    private static IEnumerable<HistoricalData.Game> Postseason(int season) =>
        HistoricalData.Games.Where(g => g.Season == season && g.GameType != "REG");
}
