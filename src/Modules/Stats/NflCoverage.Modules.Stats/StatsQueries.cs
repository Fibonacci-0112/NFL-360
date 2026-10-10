using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using NflCoverage.Modules.Stats.Contracts;
using NflCoverage.Modules.Stats.Persistence;

namespace NflCoverage.Modules.Stats;

internal sealed class StatsQueries(StatsDbContext db) : IStatsQueries
{
    private static readonly LeaderCategory[] RosterCategories =
    [
        LeaderCategory.PassingYards, LeaderCategory.RushingYards, LeaderCategory.ReceivingYards,
        LeaderCategory.Tackles, LeaderCategory.Sacks, LeaderCategory.Interceptions, LeaderCategory.FgMade,
    ];

    public async Task<IReadOnlyList<int>> GetSeasonsAsync(CancellationToken cancellationToken = default) =>
        await db.PlayerSeasons.AsNoTracking().Select(r => r.Season).Distinct().OrderByDescending(s => s).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LeaderRow>> GetLeadersAsync(
        int season,
        LeaderCategory category,
        SeasonType seasonType = SeasonType.Regular,
        int top = 50,
        CancellationToken cancellationToken = default)
    {
        top = Math.Clamp(top, 1, 500);
        var rows = db.PlayerSeasons.AsNoTracking().Where(r => r.Season == season && r.SeasonType == seasonType);
        List<PlayerSeasonStats> lines;

        if (category == LeaderCategory.PasserRating)
        {
            // Rating needs per-attempt math with clamps, so rank the qualified passers in memory.
            var teamGames = await db.TeamSeasons.AsNoTracking()
                .Where(t => t.Season == season && t.SeasonType == seasonType)
                .MaxAsync(t => (int?)t.Games, cancellationToken)
                ?? await rows.MaxAsync(r => (int?)r.Games, cancellationToken) ?? 0;
            var passers = await Project(rows.Where(r => r.Stats.Passing.Attempts > 0)).ToListAsync(cancellationToken);
            lines = [.. passers
                .Where(p => p.Stats.Passing.Attempts >= StatFormulas.PasserRatingAttemptsPerGame * (seasonType == SeasonType.Regular ? teamGames : p.Games))
                .OrderByDescending(p => LeaderKeys.Value(category, p.Stats))
                .ThenBy(p => p.PlayerId)
                .Take(top)];
        }
        else
        {
            var key = LeaderKeys.OnRow(category);
            lines = await Project(rows.Where(LeaderKeys.NonZero(key)).OrderByDescending(key).ThenBy(r => r.PlayerId).Take(top))
                .ToListAsync(cancellationToken);
        }

        return await ToLeaderRowsAsync(category, lines, cancellationToken);
    }

    public async Task<PlayerProfile?> GetPlayerAsync(string id, CancellationToken cancellationToken = default) =>
        await db.Players.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new PlayerProfile(
                p.Id, p.Name, p.Position, p.PositionGroup, p.HeadshotUrl, p.BirthDate, p.College, p.DraftYear, p.DraftRound,
                p.DraftPick, p.DraftTeam, p.RookieSeason, p.LastSeason, p.LatestTeam))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PlayerSeasonStats>> GetPlayerCareerAsync(string id, CancellationToken cancellationToken = default) =>
        [.. (await Project(db.PlayerSeasons.AsNoTracking().Where(r => r.PlayerId == id)).ToListAsync(cancellationToken))
            .OrderBy(r => r.Season).ThenBy(r => r.SeasonType)];

    public async Task<IReadOnlyList<PlayerGameStats>> GetPlayerGameLogAsync(string id, int season, CancellationToken cancellationToken = default) =>
        [.. (await db.PlayerGames.AsNoTracking()
                .Where(g => g.PlayerId == id && g.Season == season)
                .Select(g => new PlayerGameStats(g.PlayerId, g.GameId, g.Season, g.Week, g.SeasonType, g.Team, g.Opponent, g.Stats))
                .ToListAsync(cancellationToken))
            .OrderBy(g => g.SeasonType).ThenBy(g => g.Week)];

    public async Task<IReadOnlyList<PlayerSummary>> SearchPlayersAsync(string query, int limit = 25, CancellationToken cancellationToken = default)
    {
        query = query.Trim();
        if (query.Length < 2)
        {
            return [];
        }

        var pattern = "%" + query.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("%", @"\%", StringComparison.Ordinal).Replace("_", @"\_", StringComparison.Ordinal) + "%";
        return await db.Players.AsNoTracking()
            .Where(p => EF.Functions.ILike(p.Name, pattern))
            .OrderByDescending(p => p.LastSeason).ThenBy(p => p.Name)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(p => new PlayerSummary(p.Id, p.Name, p.Position, p.LatestTeam, p.HeadshotUrl, p.RookieSeason, p.LastSeason))
            .ToListAsync(cancellationToken);
    }

    public async Task<TeamSeasonStats?> GetTeamSeasonAsync(
        string team,
        int season,
        SeasonType seasonType = SeasonType.Regular,
        CancellationToken cancellationToken = default) =>
        await db.TeamSeasons.AsNoTracking()
            .Where(t => t.Team == team && t.Season == season && t.SeasonType == seasonType)
            .Select(t => new TeamSeasonStats(t.Team, t.Season, t.SeasonType, t.Games, t.Offense, t.Defense, t.Penalties, t.PenaltyYards))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TeamGameStats>> GetTeamGamesAsync(string team, int season, CancellationToken cancellationToken = default) =>
        [.. (await db.TeamGames.AsNoTracking()
                .Where(g => g.Team == team && g.Season == season)
                .Select(g => new TeamGameStats(g.Team, g.GameId, g.Season, g.Week, g.SeasonType, g.Opponent, g.Offense))
                .ToListAsync(cancellationToken))
            .OrderBy(g => g.SeasonType).ThenBy(g => g.Week)];

    public async Task<IReadOnlyList<TeamGameEfficiency>> GetTeamGameEfficiencyAsync(string team, int season, CancellationToken cancellationToken = default)
    {
        var rows = await db.TeamGames.AsNoTracking()
            .Where(g => g.Season == season && (g.Team == team || g.Opponent == team))
            .Select(g => new { g.Team, g.GameId, g.Week, g.SeasonType, g.Opponent, g.Offense })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Where(r => r.Team == team)
                .Select(own => (own, allowed: rows.FirstOrDefault(r => r.GameId == own.GameId && r.Team == own.Opponent)))
                .Where(g => g.allowed is not null)
                .Select(g => new TeamGameEfficiency(
                    g.own.GameId, g.own.Week, g.own.SeasonType, g.own.Opponent,
                    StatFormulas.EpaPerPlay(g.own.Offense.Epa, g.own.Offense.Plays) ?? 0,
                    StatFormulas.EpaPerPlay(g.allowed!.Offense.Epa, g.allowed.Offense.Plays) ?? 0))
                .OrderBy(g => g.SeasonType).ThenBy(g => g.Week),
        ];
    }

    public async Task<IReadOnlyList<TeamComparisonRow>> GetTeamComparisonAsync(
        int season,
        SeasonType seasonType = SeasonType.Regular,
        CancellationToken cancellationToken = default)
    {
        var games = await db.TeamGames.AsNoTracking()
            .Where(g => g.Season == season && g.SeasonType == seasonType)
            .Select(g => new { g.Team, g.Opponent, g.Offense })
            .ToListAsync(cancellationToken);
        var allowed = games.ToLookup(g => g.Opponent, g => g.Offense);

        var rows = games.GroupBy(g => g.Team).Select(own =>
        {
            var offense = own.Select(g => g.Offense).ToList();
            var defense = allowed[own.Key].ToList();
            return new TeamComparisonRow(
                own.Key,
                offense.Count,
                offense.Sum(o => o.Plays),
                StatFormulas.EpaPerPlay(offense.Sum(o => o.Epa), offense.Sum(o => o.Plays)) ?? 0,
                StatFormulas.EpaPerPlay(offense.Sum(o => o.PassingEpa), offense.Sum(o => o.PassAttempts + o.SacksTaken)) ?? 0,
                StatFormulas.EpaPerPlay(offense.Sum(o => o.RushingEpa), offense.Sum(o => o.Carries)) ?? 0,
                StatFormulas.EpaPerPlay(offense.Sum(o => o.Yards), offense.Sum(o => o.Plays)) ?? 0,
                defense.Sum(o => o.Plays),
                StatFormulas.DefensiveEpaPerPlay(defense.Select(o => (o.Epa, o.Plays))) ?? 0,
                StatFormulas.EpaPerPlay(defense.Sum(o => o.Yards), defense.Sum(o => o.Plays)) ?? 0,
                offense.Sum(o => o.Turnovers),
                defense.Sum(o => o.Turnovers),
                OffenseRank: 0,
                DefenseRank: 0);
        }).ToList();

        return
        [
            .. rows.Select(r => r with
            {
                OffenseRank = 1 + rows.Count(o => o.OffenseEpaPerPlay > r.OffenseEpaPerPlay),
                DefenseRank = 1 + rows.Count(o => o.DefenseEpaPerPlay < r.DefenseEpaPerPlay),
            })
            .OrderBy(r => r.OffenseRank).ThenBy(r => r.Team),
        ];
    }

    public async Task<IReadOnlyList<TeamLeader>> GetTeamRosterLeadersAsync(string team, int season, CancellationToken cancellationToken = default)
    {
        var roster = await Project(db.PlayerSeasons.AsNoTracking()
                .Where(r => r.Team == team && r.Season == season && r.SeasonType == SeasonType.Regular))
            .ToListAsync(cancellationToken);
        var leaders = new List<(LeaderCategory Category, PlayerSeasonStats Line)>();
        foreach (var category in RosterCategories)
        {
            var best = roster.Where(r => LeaderKeys.Value(category, r.Stats) > 0)
                .OrderByDescending(r => LeaderKeys.Value(category, r.Stats)).ThenBy(r => r.PlayerId)
                .FirstOrDefault();
            if (best is not null)
            {
                leaders.Add((category, best));
            }
        }

        var players = await SummariesAsync(leaders.Select(l => l.Line.PlayerId), cancellationToken);
        return
        [
            .. leaders.Where(l => players.ContainsKey(l.Line.PlayerId)).Select(l => new TeamLeader(
                l.Category,
                new LeaderRow(1, players[l.Line.PlayerId], l.Line.Team, l.Line.Games, LeaderKeys.Value(l.Category, l.Line.Stats), l.Line.Stats))),
        ];
    }

    private async Task<IReadOnlyList<LeaderRow>> ToLeaderRowsAsync(LeaderCategory category, List<PlayerSeasonStats> lines, CancellationToken cancellationToken)
    {
        var players = await SummariesAsync(lines.Select(l => l.PlayerId), cancellationToken);
        var result = new List<LeaderRow>(lines.Count);
        var rank = 0;
        double? previous = null;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var value = LeaderKeys.Value(category, line.Stats);
            if (value != previous)
            {
                rank = i + 1;
                previous = value;
            }

            var player = players.GetValueOrDefault(line.PlayerId) ?? new PlayerSummary(line.PlayerId, line.PlayerId, null, null, null, null, null);
            result.Add(new LeaderRow(rank, player, line.Team, line.Games, value, line.Stats));
        }

        return result;
    }

    private async Task<Dictionary<string, PlayerSummary>> SummariesAsync(IEnumerable<string> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToList();
        return await db.Players.AsNoTracking()
            .Where(p => wanted.Contains(p.Id))
            .Select(p => new PlayerSummary(p.Id, p.Name, p.Position, p.LatestTeam, p.HeadshotUrl, p.RookieSeason, p.LastSeason))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
    }

    private static IQueryable<PlayerSeasonStats> Project(IQueryable<PlayerSeason> rows) =>
        rows.Select(r => new PlayerSeasonStats(r.PlayerId, r.Season, r.SeasonType, r.Team, r.Games, r.Stats));
}

/// <summary>The value each leaderboard ranks by, as an expression for SQL and as a function for in-memory ranking.</summary>
internal static class LeaderKeys
{
    private static readonly Dictionary<LeaderCategory, Expression<Func<StatLine, double>>> Expressions = new()
    {
        [LeaderCategory.PassingYards] = s => s.Passing.Yards,
        [LeaderCategory.PassingTds] = s => s.Passing.Touchdowns,
        [LeaderCategory.PassingEpa] = s => s.Passing.Epa,
        [LeaderCategory.RushingYards] = s => s.Rushing.Yards,
        [LeaderCategory.RushingTds] = s => s.Rushing.Touchdowns,
        [LeaderCategory.ReceivingYards] = s => s.Receiving.Yards,
        [LeaderCategory.Receptions] = s => s.Receiving.Receptions,
        [LeaderCategory.ReceivingTds] = s => s.Receiving.Touchdowns,
        [LeaderCategory.Sacks] = s => s.Defense.Sacks,
        [LeaderCategory.Interceptions] = s => s.Defense.Interceptions,
        [LeaderCategory.Tackles] = s => s.Defense.SoloTackles + s.Defense.AssistedTackles,
        [LeaderCategory.FgMade] = s => s.Kicking.FgMade,
        [LeaderCategory.FantasyPointsPpr] = s => s.FantasyPointsPpr,
    };

    private static readonly Dictionary<LeaderCategory, Func<StatLine, double>> Functions =
        Expressions.ToDictionary(e => e.Key, e => e.Value.Compile());

    public static double Value(LeaderCategory category, StatLine stats) =>
        category == LeaderCategory.PasserRating ? StatFormulas.PasserRating(stats.Passing) : Functions[category](stats);

    /// <summary><c>row => key(row.Stats)</c>, so the ranking runs in the database.</summary>
    public static Expression<Func<PlayerSeason, double>> OnRow(LeaderCategory category)
    {
        var key = Expressions[category];
        var row = Expression.Parameter(typeof(PlayerSeason), "row");
        var body = ReplacingExpressionVisitor.Replace(key.Parameters[0], Expression.Property(row, nameof(PlayerSeason.Stats)), key.Body);
        return Expression.Lambda<Func<PlayerSeason, double>>(body, row);
    }

    public static Expression<Func<PlayerSeason, bool>> NonZero(Expression<Func<PlayerSeason, double>> key) =>
        Expression.Lambda<Func<PlayerSeason, bool>>(Expression.NotEqual(key.Body, Expression.Constant(0d)), key.Parameters);
}
