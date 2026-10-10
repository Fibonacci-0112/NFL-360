using Microsoft.EntityFrameworkCore;
using NflCoverage.Modules.League.Contracts;
using NflCoverage.Modules.League.Persistence;
using NflCoverage.SharedKernel;

namespace NflCoverage.Modules.League;

internal sealed class LeagueIngestion(LeagueDbContext db, IEventBus events, TimeProvider clock) : ILeagueIngestion
{
    public async Task UpsertTeamsAsync(IReadOnlyCollection<TeamUpsert> teams, CancellationToken cancellationToken = default)
    {
        var existing = await db.Teams.ToDictionaryAsync(t => t.Id, cancellationToken);
        foreach (var t in teams)
        {
            if (!existing.TryGetValue(t.Id, out var team))
            {
                db.Teams.Add(new Team
                {
                    Id = t.Id, Name = t.Name, Nickname = t.Nickname, Conference = t.Conference, Division = t.Division,
                    PrimaryColor = t.PrimaryColor, SecondaryColor = t.SecondaryColor, LogoUrl = t.LogoUrl,
                });
                continue;
            }

            team.Name = t.Name;
            team.Nickname = t.Nickname;
            team.Conference = t.Conference;
            team.Division = t.Division;
            team.PrimaryColor = t.PrimaryColor;
            team.SecondaryColor = t.SecondaryColor;
            team.LogoUrl = t.LogoUrl;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> UpsertGamesAsync(IReadOnlyCollection<GameUpsert> games, CancellationToken cancellationToken = default)
    {
        var seasons = games.Select(g => g.Season).Distinct().ToList();
        var existing = await db.Games.Where(g => seasons.Contains(g.Season)).ToDictionaryAsync(g => g.Id, cancellationToken);
        var now = clock.GetUtcNow();
        var changedResults = new HashSet<int>();
        var changed = 0;

        foreach (var g in games)
        {
            if (!existing.TryGetValue(g.Id, out var game))
            {
                game = new Game { Id = g.Id, GameType = g.GameType, HomeTeamId = g.HomeTeam, AwayTeamId = g.AwayTeam };
                db.Games.Add(game);
            }
            else if (game.HomeScore == g.HomeScore && game.AwayScore == g.AwayScore && game.Status == g.Status
                && game.Kickoff == g.Kickoff && game.Week == g.Week && game.HomeSpread == g.HomeSpread
                && game.NeutralSite == g.NeutralSite && game.HomeTeamId == g.HomeTeam && game.AwayTeamId == g.AwayTeam)
            {
                continue;
            }

            if (g.Status == GameStatus.Final || game.Status == GameStatus.Final)
            {
                changedResults.Add(g.Season);
            }

            game.Season = g.Season;
            game.GameType = g.GameType;
            game.Week = g.Week;
            game.Kickoff = g.Kickoff;
            game.HomeTeamId = g.HomeTeam;
            game.AwayTeamId = g.AwayTeam;
            game.HomeScore = g.HomeScore;
            game.AwayScore = g.AwayScore;
            game.Status = g.Status;
            game.StatusDetail = g.Status == GameStatus.Final ? "Final" : game.StatusDetail;
            game.NeutralSite = g.NeutralSite;
            game.HomeSpread = g.HomeSpread;
            game.UpdatedAt = now;
            changed++;
        }

        await db.SaveChangesAsync(cancellationToken);
        await PublishAsync(changedResults, cancellationToken);
        return changed;
    }

    public async Task ApplyLiveScoresAsync(IReadOnlyCollection<LiveScoreUpdate> updates, CancellationToken cancellationToken = default)
    {
        var ids = updates.Select(u => u.GameId).ToList();
        var games = await db.Games.Where(g => ids.Contains(g.Id)).ToDictionaryAsync(g => g.Id, cancellationToken);
        var changedResults = new HashSet<int>();

        foreach (var update in updates)
        {
            if (!games.TryGetValue(update.GameId, out var game)
                || (game.HomeScore == update.HomeScore && game.AwayScore == update.AwayScore
                    && game.Status == update.Status && game.StatusDetail == update.StatusDetail))
            {
                continue;
            }

            if (update.Status == GameStatus.Final || game.Status == GameStatus.Final)
            {
                changedResults.Add(game.Season);
            }

            game.HomeScore = update.HomeScore;
            game.AwayScore = update.AwayScore;
            game.Status = update.Status;
            game.StatusDetail = update.StatusDetail;
            game.UpdatedAt = clock.GetUtcNow();
        }

        await db.SaveChangesAsync(cancellationToken);
        await PublishAsync(changedResults, cancellationToken);
    }

    private async Task PublishAsync(IEnumerable<int> seasons, CancellationToken cancellationToken)
    {
        foreach (var season in seasons)
        {
            await events.PublishAsync(new GameResultsChanged(season), cancellationToken);
        }
    }
}
