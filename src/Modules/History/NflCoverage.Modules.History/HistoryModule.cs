using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NflCoverage.Modules.History.Contracts;
using NflCoverage.Modules.League.Contracts;
using NflCoverage.Modules.Standings.Contracts;
using NflCoverage.SharedKernel;

namespace NflCoverage.Modules.History;

/// <summary>
/// A read model over League results with no storage of its own. Everything is derived from all final games, cached,
/// and rebuilt after any results change.
/// </summary>
public sealed class HistoryModule : IModule
{
    public string Name => "History";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddSingleton<HistoryVersion>();
        services.AddScoped<IHistoryQueries, HistoryQueries>();
        services.AddScoped<IHandles<GameResultsChanged>, InvalidateOnResultsChanged>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/history").WithTags(Name);
        api.MapGet("/champions", (IHistoryQueries history, CancellationToken ct) => history.GetChampionsAsync(ct));
        api.MapGet("/seasons", (IHistoryQueries history, CancellationToken ct) => history.GetSeasonSummariesAsync(ct));
        api.MapGet("/seasons/{season:int}", async (int season, IHistoryQueries history, CancellationToken ct) =>
            await history.GetSeasonSummaryAsync(season, ct) is { } summary ? Results.Ok(summary) : Results.NotFound());
        api.MapGet("/teams/{team}", async (string team, IHistoryQueries history, CancellationToken ct) =>
            await history.GetFranchiseHistoryAsync(team, ct) is { } franchise ? Results.Ok(franchise) : Results.NotFound());
        api.MapGet("/head-to-head", async (string a, string b, IHistoryQueries history, CancellationToken ct) =>
            await history.GetHeadToHeadAsync(a, b, ct) is { } series ? Results.Ok(series) : Results.NotFound());
    }

    /// <summary>History spans every season, so one version covers it; any results change bumps it.</summary>
    internal sealed class HistoryVersion
    {
        private int _version;

        public int Current => Volatile.Read(ref _version);

        public void Bump() => Interlocked.Increment(ref _version);
    }

    private sealed class InvalidateOnResultsChanged(HistoryVersion version) : IHandles<GameResultsChanged>
    {
        public Task HandleAsync(GameResultsChanged @event, CancellationToken cancellationToken)
        {
            version.Bump();
            return Task.CompletedTask;
        }
    }
}

internal sealed record HistoryIndex(
    IReadOnlyList<GameDto> Finals,
    IReadOnlyDictionary<string, TeamDto> Teams,
    IReadOnlyDictionary<int, IReadOnlyDictionary<string, DivisionFinish>> Divisions,
    IReadOnlyList<SeasonSummary> Summaries);

internal sealed class HistoryQueries(
    ILeagueQueries league,
    IStandingsQueries standings,
    IMemoryCache cache,
    HistoryModule.HistoryVersion version) : IHistoryQueries
{
    public async Task<FranchiseHistory?> GetFranchiseHistoryAsync(string team, CancellationToken cancellationToken = default)
    {
        var index = await GetIndexAsync(cancellationToken);
        team = team.ToUpperInvariant();
        if (!index.Teams.ContainsKey(team))
        {
            return null;
        }

        var seasons = HistoryCalculator.FranchiseSeasons(team, index.Finals, index.Divisions);
        return new FranchiseHistory(team, seasons, HistoryCalculator.Totals(seasons));
    }

    public async Task<HeadToHead?> GetHeadToHeadAsync(string teamA, string teamB, CancellationToken cancellationToken = default)
    {
        var index = await GetIndexAsync(cancellationToken);
        teamA = teamA.ToUpperInvariant();
        teamB = teamB.ToUpperInvariant();
        return teamA != teamB && index.Teams.ContainsKey(teamA) && index.Teams.ContainsKey(teamB)
            ? HistoryCalculator.HeadToHead(teamA, teamB, index.Finals)
            : null;
    }

    public async Task<IReadOnlyList<SuperBowlResult>> GetChampionsAsync(CancellationToken cancellationToken = default) =>
        [.. (await GetIndexAsync(cancellationToken)).Summaries.Select(s => s.SuperBowl).OfType<SuperBowlResult>()];

    public async Task<SeasonSummary?> GetSeasonSummaryAsync(int season, CancellationToken cancellationToken = default) =>
        (await GetIndexAsync(cancellationToken)).Summaries.FirstOrDefault(s => s.Season == season);

    public async Task<IReadOnlyList<SeasonSummary>> GetSeasonSummariesAsync(CancellationToken cancellationToken = default) =>
        (await GetIndexAsync(cancellationToken)).Summaries;

    private async Task<HistoryIndex> GetIndexAsync(CancellationToken cancellationToken) =>
        (await cache.GetOrCreateAsync((nameof(HistoryIndex), version.Current), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            return await BuildIndexAsync(cancellationToken);
        }))!;

    private async Task<HistoryIndex> BuildIndexAsync(CancellationToken cancellationToken)
    {
        var finals = await league.GetFinalGamesBeforeAsync(int.MaxValue, cancellationToken);
        var teams = (await league.GetTeamsAsync(cancellationToken)).ToDictionary(t => t.Id);

        // Division titles and seeds come from the tiebreaker engine, once a regular season is complete.
        var divisions = new Dictionary<int, IReadOnlyDictionary<string, DivisionFinish>>();
        foreach (var season in finals.Select(g => g.Season).Where(s => s >= IStandingsQueries.FirstSupportedSeason).Distinct())
        {
            if (!await RegularSeasonCompleteAsync(season, finals, cancellationToken)
                || await standings.GetAsync(season, cancellationToken: cancellationToken) is not { } final)
            {
                continue;
            }

            divisions[season] = final.Teams.ToDictionary(t => t.TeamId, t => new DivisionFinish(t.DivisionRank == 1, t.Seed));
        }

        return new HistoryIndex(finals, teams, divisions, HistoryCalculator.SeasonSummaries(finals, teams));
    }

    private async Task<bool> RegularSeasonCompleteAsync(int season, IReadOnlyList<GameDto> finals, CancellationToken cancellationToken)
    {
        if (finals.Any(g => g.Season == season && GameTypes.IsPostseason(g.GameType)))
        {
            return true;
        }

        var games = await league.GetGamesAsync(season, cancellationToken);
        return games.Where(g => g.GameType == GameTypes.Regular).All(g => g.Status == GameStatus.Final);
    }
}
