using Microsoft.EntityFrameworkCore;
using NflCoverage.Modules.League.Contracts;
using NflCoverage.Modules.League.Persistence;

namespace NflCoverage.Modules.League;

internal sealed class LeagueQueries(LeagueDbContext db) : ILeagueQueries
{
    public async Task<IReadOnlyList<TeamDto>> GetTeamsAsync(CancellationToken cancellationToken = default) =>
        await db.Teams.AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => new TeamDto(t.Id, t.Name, t.Nickname, t.Conference, t.Division, t.PrimaryColor, t.SecondaryColor, t.LogoUrl))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<int>> GetSeasonsAsync(CancellationToken cancellationToken = default) =>
        await db.Games.AsNoTracking().Select(g => g.Season).Distinct().OrderByDescending(s => s).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<GameDto>> GetGamesAsync(int season, CancellationToken cancellationToken = default) =>
        await Project(db.Games.Where(g => g.Season == season)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<GameDto>> GetFinalGamesBeforeAsync(int beforeSeason, CancellationToken cancellationToken = default) =>
        await Project(db.Games.Where(g => g.Season < beforeSeason && g.Status == GameStatus.Final)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<GameDto>> GetGamesKickingOffBetweenAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
        await Project(db.Games.Where(g => g.Kickoff >= from && g.Kickoff <= to)).ToListAsync(cancellationToken);

    private static IQueryable<GameDto> Project(IQueryable<Game> games) =>
        games.AsNoTracking()
            .OrderBy(g => g.Season).ThenBy(g => g.Kickoff).ThenBy(g => g.Id)
            .Select(g => new GameDto(
                g.Id, g.Season, g.GameType, g.Week, g.Kickoff, g.HomeTeamId, g.AwayTeamId,
                g.HomeScore, g.AwayScore, g.Status, g.StatusDetail, g.NeutralSite, g.HomeSpread));
}
