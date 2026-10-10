namespace NflCoverage.Rules;

/// <summary>Resolves a tie that survives every tiebreaking step.</summary>
public interface ICoinToss
{
    int Pick(IReadOnlyList<int> teams, League league);
}

public static class CoinToss
{
    /// <summary>Deterministic stand-in for real standings: lowest team id wins. Flagged as a coin toss in the notes.</summary>
    public static ICoinToss Deterministic { get; } = new DeterministicCoinToss();

    public static ICoinToss Random(Random random) => new RandomCoinToss(random);

    private sealed class DeterministicCoinToss : ICoinToss
    {
        public int Pick(IReadOnlyList<int> teams, League league) =>
            teams.MinBy(t => league.Teams[t].Id, StringComparer.Ordinal);
    }

    private sealed class RandomCoinToss(Random random) : ICoinToss
    {
        public int Pick(IReadOnlyList<int> teams, League league) => teams[random.Next(teams.Count)];
    }
}
