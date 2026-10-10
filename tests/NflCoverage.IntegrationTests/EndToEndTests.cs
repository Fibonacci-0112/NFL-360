using System.Net;
using Microsoft.Extensions.DependencyInjection;
using NflCoverage.Modules.PlayoffOdds.Contracts;
using NflCoverage.Modules.Standings.Contracts;
using NflCoverage.Web.Client.Models;

namespace NflCoverage.IntegrationTests;

[Collection(NflAppGroup.Name)]
public class EndToEndTests(NflAppFixture app)
{
    private readonly HttpClient _client = app.Factory.CreateClient();

    [Fact]
    public async Task Imported_2023_season_produces_the_actual_playoff_seeds()
    {
        var standings = await _client.GetFromJsonAsync<SeasonStandings>("/api/seasons/2023/standings", TestContext.Current.CancellationToken);

        standings!.ThroughWeek.Should().Be(18);
        Seeds(standings, "AFC").Should().Equal("BAL", "BUF", "KC", "HOU", "CLE", "MIA", "PIT");
        Seeds(standings, "NFC").Should().Equal("SF", "DAL", "DET", "TB", "PHI", "LA", "GB");
    }

    [Fact]
    public async Task Standings_can_be_viewed_as_of_an_earlier_week()
    {
        var standings = await _client.GetFromJsonAsync<SeasonStandings>("/api/seasons/2023/standings?week=4", TestContext.Current.CancellationToken);

        standings!.ThroughWeek.Should().Be(4);
        standings.Teams.Sum(t => t.Wins + t.Losses + t.Ties).Should().BeLessThan(2 * 16 * 4 + 1);
    }

    [Fact]
    public async Task Reimporting_the_same_data_changes_nothing()
    {
        (await app.ImportFixtureAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Playoff_odds_are_calculated_stored_and_served()
    {
        await using (var scope = app.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IPlayoffOddsCalculator>().RecalculateAsync(2023, throughWeek: 10, TestContext.Current.CancellationToken);
        }

        var odds = await _client.GetFromJsonAsync<PlayoffOddsSnapshot>("/api/seasons/2023/playoff-odds", TestContext.Current.CancellationToken);

        odds!.ThroughWeek.Should().Be(10);
        odds.Teams.Should().HaveCount(32);
        odds.Teams.Sum(t => t.MakePlayoffs).Should().BeApproximately(14, 1e-6);
        odds.Teams.Single(t => t.TeamId == "SF").MakePlayoffs.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task What_if_payload_round_trips_through_the_client_serializer()
    {
        var response = await _client.GetAsync("/api/what-if/2023", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = await response.Content.ReadFromJsonAsync(ClientJsonContext.Default.WhatIfData, TestContext.Current.CancellationToken);

        data!.Teams.Should().HaveCount(32);
        data.Input.CompletedGames.Should().HaveCount(272);
        data.Input.RemainingGames.Should().BeEmpty();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/standings/2023")]
    [InlineData("/playoffs/odds/2023")]
    [InlineData("/teams/KC/2023")]
    [InlineData("/playoffs/what-if/2023")]
    public async Task Pages_render(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("NFL 360");
    }

    private static IEnumerable<string> Seeds(SeasonStandings standings, string conference) =>
        standings.Teams.Where(t => t.Conference == conference && t.Seed is not null).OrderBy(t => t.Seed).Select(t => t.TeamId);
}
