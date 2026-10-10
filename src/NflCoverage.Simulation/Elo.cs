using NflCoverage.Rules;

namespace NflCoverage.Simulation;

/// <summary>
/// FiveThirtyEight-style NFL Elo: K = 20, home field worth 48 points, margin-of-victory multiplier with
/// autocorrelation correction, and one-third regression to the mean between seasons.
/// </summary>
public static class Elo
{
    public const double Mean = 1500;
    public const double HomeFieldAdvantage = 48;
    public const double K = 20;
    public const double SeasonRegression = 1.0 / 3;

    /// <summary>Points of expected margin per Elo point (25 Elo ≈ 1 point).</summary>
    public const double PointsPerEloPoint = 1.0 / 25;

    public static double HomeWinProbability(double homeRating, double awayRating, bool neutralSite) =>
        1 / (1 + Math.Pow(10, -RatingEdge(homeRating, awayRating, neutralSite) / 400));

    public static double ExpectedHomeMargin(double homeRating, double awayRating, bool neutralSite) =>
        RatingEdge(homeRating, awayRating, neutralSite) * PointsPerEloPoint;

    /// <summary>Returns the rating change to add to the home team (and subtract from the away team).</summary>
    public static double HomeRatingChange(double homeRating, double awayRating, bool neutralSite, int homeScore, int awayScore)
    {
        var edge = RatingEdge(homeRating, awayRating, neutralSite);
        var expected = 1 / (1 + Math.Pow(10, -edge / 400));
        var actual = homeScore > awayScore ? 1.0 : homeScore < awayScore ? 0.0 : 0.5;
        var margin = Math.Abs(homeScore - awayScore);
        var winnerEdge = homeScore >= awayScore ? edge : -edge;
        var movMultiplier = Math.Log(margin + 1) * (2.2 / ((winnerEdge * 0.001) + 2.2));
        return K * Math.Max(movMultiplier, 1e-9) * (actual - expected);
    }

    public static double RegressToMean(double rating) => rating + ((Mean - rating) * SeasonRegression);

    /// <summary>Replays history in order and returns each team's current rating.</summary>
    public static Dictionary<string, double> BuildRatings(IEnumerable<HistoricalGame> history)
    {
        var ratings = new Dictionary<string, double>(StringComparer.Ordinal);
        int? currentSeason = null;
        foreach (var game in history.OrderBy(g => g.Season).ThenBy(g => g.Order))
        {
            if (currentSeason != game.Season)
            {
                if (currentSeason is not null)
                {
                    foreach (var team in ratings.Keys.ToList())
                    {
                        ratings[team] = RegressToMean(ratings[team]);
                    }
                }

                currentSeason = game.Season;
            }

            var home = ratings.GetValueOrDefault(game.Score.HomeTeam, Mean);
            var away = ratings.GetValueOrDefault(game.Score.AwayTeam, Mean);
            var change = HomeRatingChange(home, away, game.NeutralSite, game.Score.HomeScore, game.Score.AwayScore);
            ratings[game.Score.HomeTeam] = home + change;
            ratings[game.Score.AwayTeam] = away - change;
        }

        return ratings;
    }

    private static double RatingEdge(double homeRating, double awayRating, bool neutralSite) =>
        homeRating - awayRating + (neutralSite ? 0 : HomeFieldAdvantage);
}

/// <summary>A completed game in rating history. <paramref name="Order"/> sorts games within a season (e.g. by date).</summary>
public sealed record HistoricalGame(int Season, long Order, GameScore Score, bool NeutralSite);
