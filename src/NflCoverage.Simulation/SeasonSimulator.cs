using NflCoverage.Rules;

namespace NflCoverage.Simulation;

/// <summary>
/// Monte Carlo simulation of the rest of a season plus the playoff bracket. Single-threaded and allocation-light
/// so it can run in the browser; servers parallelize by running several seeds and calling <see cref="Merge"/>.
/// </summary>
public static class SeasonSimulator
{
    /// <summary>Standard deviation of NFL game margins around the expected margin.</summary>
    public const double MarginStandardDeviation = 13.5;

    public static SimulationResult Run(SimulationInput input, int iterations, int seed)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);

        var league = League.Create(input.Teams);
        var playoffTeams = StandingsCalculator.PlayoffTeamsPerConference(input.Season);
        var random = new Random(seed);
        var coinToss = CoinToss.Random(random);
        var ratings = Enumerable.Range(0, league.Count)
            .Select(i => input.Ratings.GetValueOrDefault(league.Teams[i].Id, Elo.Mean))
            .ToArray();
        var games = Prepare(league, input, ratings);

        var baseTable = SeasonTable.From(league, input.CompletedGames);
        var table = new SeasonTable(league);
        var tally = new Tally(league.Count, playoffTeams);

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            table.CopyFrom(baseTable);
            foreach (var game in games)
            {
                var margin = game.Forced switch
                {
                    ForcedOutcome.HomeWin => 1 + random.Next(14),
                    ForcedOutcome.AwayWin => -1 - random.Next(14),
                    ForcedOutcome.Tie => 0,
                    _ => SampleMargin(random, game.ExpectedMargin),
                };
                var loserScore = 10 + random.Next(18);
                table.Add(game.Home, game.Away, loserScore + Math.Max(margin, 0), loserScore + Math.Max(-margin, 0));
            }

            var seeds = StandingsCalculator.ComputeSeeds(table, playoffTeams, coinToss);
            tally.Record(table, seeds);
            SimulatePlayoffs(seeds, ratings, random, tally);
        }

        return tally.ToResult(input.Season, iterations, league);
    }

    /// <summary>Combines results from independent runs (e.g. parallel server partitions) into one weighted result.</summary>
    public static SimulationResult Merge(IReadOnlyList<SimulationResult> parts)
    {
        var total = parts.Sum(p => p.Iterations);
        double Weighted(Func<SimulationResult, double> value) => parts.Sum(p => value(p) * p.Iterations) / total;

        var teams = parts[0].Teams.Select((team, i) => new TeamOdds(
            team.TeamId,
            Weighted(p => p.Teams[i].MeanWins),
            Weighted(p => p.Teams[i].MakePlayoffs),
            Weighted(p => p.Teams[i].WinDivision),
            Weighted(p => p.Teams[i].FirstSeed),
            [.. team.SeedProbabilities.Select((_, s) => Weighted(p => p.Teams[i].SeedProbabilities[s]))],
            Weighted(p => p.Teams[i].WinConference),
            Weighted(p => p.Teams[i].WinSuperBowl))).ToList();

        return new SimulationResult(parts[0].Season, total, teams);
    }

    private static PreparedGame[] Prepare(League league, SimulationInput input, double[] ratings) =>
    [
        .. input.RemainingGames.Select(g =>
        {
            var home = league.IndexOf(g.HomeTeam);
            var away = league.IndexOf(g.AwayTeam);
            var expected = g.HomeSpread ?? Elo.ExpectedHomeMargin(ratings[home], ratings[away], g.NeutralSite);
            ForcedOutcome? forced = input.ForcedOutcomes.TryGetValue(g.Id, out var outcome) ? outcome : null;
            return new PreparedGame(home, away, expected, forced);
        }),
    ];

    private static int SampleMargin(Random random, double expectedMargin)
    {
        // Box-Muller normal sample; NFL ties are rare enough that a rounded zero is pushed to a one-point game.
        var u1 = 1.0 - random.NextDouble();
        var u2 = random.NextDouble();
        var normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        var margin = (int)Math.Round(expectedMargin + (MarginStandardDeviation * normal));
        return margin != 0 ? margin : random.Next(2) == 0 ? 1 : -1;
    }

    private static void SimulatePlayoffs(int[][] seeds, double[] ratings, Random random, Tally tally)
    {
        var champions = new int[seeds.Length];
        for (var c = 0; c < seeds.Length; c++)
        {
            var bracket = seeds[c];
            var byes = bracket.Length == 7 ? 1 : 2;

            // Wild card round: remaining clubs pair highest vs lowest, higher seed hosting.
            var alive = new List<int>(bracket[..byes]);
            for (int high = byes, low = bracket.Length - 1; high < low; high++, low--)
            {
                alive.Add(Play(bracket[high], bracket[low], ratings, random, neutral: false));
            }

            // Later rounds re-seed: top remaining seed hosts the lowest remaining seed.
            while (alive.Count > 1)
            {
                alive.Sort((a, b) => Array.IndexOf(bracket, a).CompareTo(Array.IndexOf(bracket, b)));
                var next = new List<int>();
                for (int high = 0, low = alive.Count - 1; high < low; high++, low--)
                {
                    next.Add(Play(alive[high], alive[low], ratings, random, neutral: false));
                }

                alive = next;
            }

            champions[c] = alive[0];
            tally.ConferenceChampion(alive[0]);
        }

        tally.SuperBowlChampion(Play(champions[0], champions[1], ratings, random, neutral: true));
    }

    private static int Play(int home, int away, double[] ratings, Random random, bool neutral) =>
        random.NextDouble() < Elo.HomeWinProbability(ratings[home], ratings[away], neutral) ? home : away;

    private readonly record struct PreparedGame(int Home, int Away, double ExpectedMargin, ForcedOutcome? Forced);

    private sealed class Tally(int teams, int playoffTeams)
    {
        private readonly double[] _wins = new double[teams];
        private readonly int[,] _seedCounts = new int[teams, playoffTeams];
        private readonly int[] _conferenceTitles = new int[teams];
        private readonly int[] _superBowls = new int[teams];

        public void Record(SeasonTable table, int[][] seeds)
        {
            for (var t = 0; t < teams; t++)
            {
                var (wins, _, ties, _, _) = table.RecordOf(t);
                _wins[t] += wins + (0.5 * ties);
            }

            foreach (var conference in seeds)
            {
                for (var s = 0; s < conference.Length; s++)
                {
                    _seedCounts[conference[s], s]++;
                }
            }
        }

        public void ConferenceChampion(int team) => _conferenceTitles[team]++;

        public void SuperBowlChampion(int team) => _superBowls[team]++;

        public SimulationResult ToResult(int season, int iterations, League league)
        {
            double N(double count) => count / iterations;
            var odds = Enumerable.Range(0, teams).Select(t =>
            {
                var seedProbabilities = Enumerable.Range(0, playoffTeams).Select(s => N(_seedCounts[t, s])).ToArray();
                return new TeamOdds(
                    league.Teams[t].Id,
                    N(_wins[t]),
                    seedProbabilities.Sum(),
                    seedProbabilities.Take(4).Sum(),
                    seedProbabilities[0],
                    seedProbabilities,
                    N(_conferenceTitles[t]),
                    N(_superBowls[t]));
            }).ToList();

            return new SimulationResult(season, iterations, odds);
        }
    }
}
