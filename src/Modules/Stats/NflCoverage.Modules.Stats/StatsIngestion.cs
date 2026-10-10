using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NflCoverage.Modules.Stats.Contracts;
using NflCoverage.Modules.Stats.Persistence;

namespace NflCoverage.Modules.Stats;

internal sealed class StatsIngestion(StatsDbContext db) : IStatsIngestion
{
    public async Task<int> UpsertPlayersAsync(IReadOnlyCollection<PlayerUpsert> players, CancellationToken cancellationToken = default)
    {
        var existing = await db.Players.ToDictionaryAsync(p => p.Id, cancellationToken);
        foreach (var (profile, extra) in players.DistinctBy(p => p.Profile.Id))
        {
            if (!existing.TryGetValue(profile.Id, out var player))
            {
                player = new Player { Id = profile.Id, Name = profile.Name };
                db.Players.Add(player);
            }

            player.Name = profile.Name;
            player.Position = profile.Position;
            player.PositionGroup = profile.PositionGroup;
            player.HeadshotUrl = profile.HeadshotUrl;
            player.BirthDate = profile.BirthDate;
            player.College = profile.College;
            player.DraftYear = profile.DraftYear;
            player.DraftRound = profile.DraftRound;
            player.DraftPick = profile.DraftPick;
            player.DraftTeam = profile.DraftTeam;
            player.RookieSeason = profile.RookieSeason;
            player.LastSeason = profile.LastSeason;
            player.LatestTeam = profile.LatestTeam;
            player.Extra = Json(extra);
        }

        var changed = await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return changed;
    }

    public Task<int> UpsertPlayerSeasonsAsync(int season, SeasonType seasonType, IReadOnlyCollection<PlayerSeasonUpsert> rows, CancellationToken cancellationToken = default)
    {
        var lines = rows.Where(r => r.Stats.Season == season && r.Stats.SeasonType == seasonType)
            .DistinctBy(r => r.Stats.PlayerId)
            .ToList();
        return ReplaceAsync(
            db.PlayerSeasons.Where(r => r.Season == season && r.SeasonType == seasonType),
            async () =>
            {
                await AddMissingPlayersAsync(lines, cancellationToken);
                db.PlayerSeasons.AddRange(lines.Select(r => new PlayerSeason
                {
                    PlayerId = r.Stats.PlayerId, Season = season, SeasonType = seasonType, Team = r.Stats.Team, Games = r.Stats.Games,
                    Stats = r.Stats.Stats, Extra = Json(r.Extra),
                }));
                return lines.Count;
            },
            cancellationToken);
    }

    public Task<int> UpsertTeamSeasonsAsync(int season, SeasonType seasonType, IReadOnlyCollection<TeamSeasonUpsert> rows, CancellationToken cancellationToken = default)
    {
        var lines = rows.Where(r => r.Stats.Season == season && r.Stats.SeasonType == seasonType).DistinctBy(r => r.Stats.Team).ToList();
        return ReplaceAsync(
            db.TeamSeasons.Where(r => r.Season == season && r.SeasonType == seasonType),
            () =>
            {
                db.TeamSeasons.AddRange(lines.Select(r => new TeamSeason
                {
                    Team = r.Stats.Team, Season = season, SeasonType = seasonType, Games = r.Stats.Games, Offense = r.Stats.Offense,
                    Defense = r.Stats.Defense, Penalties = r.Stats.Penalties, PenaltyYards = r.Stats.PenaltyYards, Extra = Json(r.Extra),
                }));
                return Task.FromResult(lines.Count);
            },
            cancellationToken);
    }

    public Task<int> UpsertTeamGamesAsync(int season, IReadOnlyCollection<TeamGameUpsert> rows, CancellationToken cancellationToken = default)
    {
        var lines = rows.Where(r => r.Stats.Season == season).DistinctBy(r => (r.Stats.Team, r.Stats.GameId)).ToList();
        return ReplaceAsync(
            db.TeamGames.Where(r => r.Season == season),
            () =>
            {
                db.TeamGames.AddRange(lines.Select(r => new TeamGame
                {
                    Team = r.Stats.Team, GameId = r.Stats.GameId, Season = season, Week = r.Stats.Week, SeasonType = r.Stats.SeasonType,
                    Opponent = r.Stats.Opponent, Offense = r.Stats.Offense, Extra = Json(r.Extra),
                }));
                return Task.FromResult(lines.Count);
            },
            cancellationToken);
    }

    public Task<int> UpsertPlayerGamesAsync(int season, IReadOnlyCollection<PlayerGameUpsert> rows, CancellationToken cancellationToken = default)
    {
        var lines = rows.Where(r => r.Stats.Season == season).DistinctBy(r => (r.Stats.PlayerId, r.Stats.GameId)).ToList();
        return ReplaceAsync(
            db.PlayerGames.Where(r => r.Season == season),
            () =>
            {
                db.PlayerGames.AddRange(lines.Select(r => new PlayerGame
                {
                    PlayerId = r.Stats.PlayerId, GameId = r.Stats.GameId, Season = season, Week = r.Stats.Week, SeasonType = r.Stats.SeasonType,
                    Team = r.Stats.Team, Opponent = r.Stats.Opponent, Stats = r.Stats.Stats, Extra = Json(r.Extra),
                }));
                return Task.FromResult(lines.Count);
            },
            cancellationToken);
    }

    public Task<int> DeletePlayerGamesBeforeAsync(int season, CancellationToken cancellationToken = default) =>
        db.PlayerGames.Where(g => g.Season < season).ExecuteDeleteAsync(cancellationToken);

    public async Task<IReadOnlyList<StatsCoverage>> GetCoverageAsync(CancellationToken cancellationToken = default)
    {
        var playerSeasons = await CountBySeasonAsync(db.PlayerSeasons.Select(r => r.Season), cancellationToken);
        var teamSeasons = await CountBySeasonAsync(db.TeamSeasons.Select(r => r.Season), cancellationToken);
        var teamGames = await CountBySeasonAsync(db.TeamGames.Select(r => r.Season), cancellationToken);
        var playerGames = await CountBySeasonAsync(db.PlayerGames.Select(r => r.Season), cancellationToken);
        return
        [
            .. playerSeasons.Keys.Union(teamSeasons.Keys).Union(teamGames.Keys).Union(playerGames.Keys).Order()
                .Select(s => new StatsCoverage(
                    s, playerSeasons.GetValueOrDefault(s), teamSeasons.GetValueOrDefault(s), teamGames.GetValueOrDefault(s), playerGames.GetValueOrDefault(s))),
        ];
    }

    /// <summary>Deletes the rows in <paramref name="scope"/> and inserts the new ones atomically.</summary>
    private async Task<int> ReplaceAsync<T>(IQueryable<T> scope, Func<Task<int>> addRows, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await scope.ExecuteDeleteAsync(cancellationToken);
        var count = await addRows();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return count;
    }

    /// <summary>Players missing from the directory get a minimal record from the stats row, so every line has a name.</summary>
    private async Task AddMissingPlayersAsync(IReadOnlyCollection<PlayerSeasonUpsert> lines, CancellationToken cancellationToken)
    {
        var ids = lines.Select(l => l.Stats.PlayerId).ToList();
        var known = (await db.Players.Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToListAsync(cancellationToken)).ToHashSet();
        db.Players.AddRange(lines.Where(l => !known.Contains(l.Stats.PlayerId)).Select(l => new Player
        {
            Id = l.Stats.PlayerId,
            Name = l.Name,
            Position = l.Position,
            PositionGroup = l.PositionGroup,
            HeadshotUrl = l.HeadshotUrl,
            LatestTeam = l.Stats.Team,
        }));
    }

    private static Task<Dictionary<int, int>> CountBySeasonAsync(IQueryable<int> seasons, CancellationToken cancellationToken) =>
        seasons.GroupBy(s => s).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);

    private static string Json(IReadOnlyDictionary<string, string> extra) => JsonSerializer.Serialize(extra);
}
