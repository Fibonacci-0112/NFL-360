namespace NflCoverage.Modules.Stats.Contracts;

/// <summary>Derived football metrics. Pure functions, shared by the Stats module and the web pages.</summary>
public static class StatFormulas
{
    /// <summary>Minimum pass attempts per team game to qualify for the passer rating leaderboard (NFL rule).</summary>
    public const int PasserRatingAttemptsPerGame = 14;

    /// <summary>
    /// The NFL passer rating: four components (completion %, yards, touchdowns and interceptions per attempt), each
    /// clamped to [0, 2.375], averaged and scaled so that 158.3 is a perfect rating.
    /// </summary>
    /// <returns>0 when there are no attempts.</returns>
    public static double PasserRating(int completions, int attempts, int yards, int touchdowns, int interceptions)
    {
        if (attempts <= 0)
        {
            return 0;
        }

        double att = attempts;
        var completion = Clamp(((completions / att) - 0.3) * 5);
        var gain = Clamp(((yards / att) - 3) * 0.25);
        var score = Clamp(touchdowns / att * 20);
        var turnover = Clamp(2.375 - (interceptions / att * 25));
        return (completion + gain + score + turnover) / 6 * 100;

        static double Clamp(double component) => Math.Clamp(component, 0, 2.375);
    }

    public static double PasserRating(PassingStats passing) =>
        PasserRating(passing.Completions, passing.Attempts, passing.Yards, passing.Touchdowns, passing.Interceptions);

    /// <summary>Offensive snaps that count toward EPA per play: dropbacks (attempts and sacks) plus designed runs.</summary>
    public static int Plays(int passAttempts, int sacks, int carries) => passAttempts + sacks + carries;

    public static double? CompletionPct(int completions, int attempts) => Ratio(completions, attempts);

    public static double? YardsPerAttempt(int yards, int attempts) => Ratio(yards, attempts);

    public static double? YardsPerCarry(int yards, int carries) => Ratio(yards, carries);

    public static double? YardsPerReception(int yards, int receptions) => Ratio(yards, receptions);

    public static double? CatchRate(int receptions, int targets) => Ratio(receptions, targets);

    public static double? FieldGoalPct(int made, int attempted) => Ratio(made, attempted);

    public static double? EpaPerPlay(double epa, int plays) => Ratio(epa, plays);

    /// <summary>
    /// EPA a defense allowed per play: the opponents' offensive EPA summed over the games against this defense,
    /// divided by the opponents' plays in those games. Lower is better.
    /// </summary>
    public static double? DefensiveEpaPerPlay(IEnumerable<(double OpponentEpa, int OpponentPlays)> games)
    {
        double epa = 0;
        var plays = 0;
        foreach (var (opponentEpa, opponentPlays) in games)
        {
            epa += opponentEpa;
            plays += opponentPlays;
        }

        return EpaPerPlay(epa, plays);
    }

    private static double? Ratio(double numerator, double denominator) => denominator > 0 ? numerator / denominator : null;
}
