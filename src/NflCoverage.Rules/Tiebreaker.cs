namespace NflCoverage.Rules;

/// <summary>
/// Implements the NFL tiebreaking procedures (division and wild card) over a <see cref="SeasonTable"/>.
/// Each step keeps only the clubs with the best value; whenever a step eliminates at least one club,
/// the procedure restarts from step 1 with the survivors, as the official rules require.
/// </summary>
/// <remarks>
/// "Best net touchdowns in all games" is not modeled because game-level scoring detail is not part of
/// <see cref="GameScore"/>; a tie that reaches that step falls through to the coin toss.
/// </remarks>
internal sealed class Tiebreaker(SeasonTable table, ICoinToss coinToss, List<TiebreakNote>? notes)
{
    private const double Epsilon = 1e-12;

    private readonly League _league = table.League;

    private delegate double[]? Step(List<int> clubs);

    /// <summary>1-based finishing position of every club within its division.</summary>
    public int[] DivisionRanks { get; } = new int[table.League.Count];

    public void RankDivisions()
    {
        for (var d = 0; d < _league.DivisionMembers.Length; d++)
        {
            var order = OrderByWinPct(
                [.. _league.DivisionMembers[d]],
                tied => SelectBest(tied, DivisionSteps(), _league.DivisionNames[d]));
            for (var rank = 0; rank < order.Count; rank++)
            {
                DivisionRanks[order[rank]] = rank + 1;
            }
        }
    }

    /// <summary>Orders clubs by win percentage, breaking ties among clubs from different divisions with the wild-card procedure.</summary>
    /// <remarks>Clubs are picked one at a time and the tied group is re-formed after every pick, so at most <paramref name="limit"/> picks are made.</remarks>
    public List<int> OrderAcrossDivisions(List<int> clubs, string context, int limit = int.MaxValue) =>
        OrderByWinPct(clubs, tied => SelectBestWildCard(tied, context), limit);

    private List<int> OrderByWinPct(List<int> clubs, Func<List<int>, int> selectBest, int limit = int.MaxValue)
    {
        var remaining = new List<int>(clubs);
        var order = new List<int>(clubs.Count);
        while (remaining.Count > 0 && order.Count < limit)
        {
            var best = remaining.Max(table.WinPct);
            var tied = remaining.Where(t => Math.Abs(table.WinPct(t) - best) < Epsilon).ToList();
            var winner = tied.Count == 1 ? tied[0] : selectBest(tied);
            order.Add(winner);
            remaining.Remove(winner);
        }

        return order;
    }

    private int SelectBestWildCard(List<int> clubs, string context)
    {
        var divisions = clubs.Select(c => _league.DivisionOf[c]).Distinct().Count();
        if (divisions == 1)
        {
            return SelectBest(clubs, DivisionSteps(), _league.DivisionNames[_league.DivisionOf[clubs[0]]]);
        }

        // Only the highest-ranked club from each division may take part in a wild-card tiebreaker.
        var representatives = clubs
            .GroupBy(c => _league.DivisionOf[c])
            .Select(g => g.MinBy(c => DivisionRanks[c]))
            .ToList();

        return representatives.Count == 1
            ? representatives[0]
            : SelectBest(representatives, WildCardSteps(), context, eliminatedByDivision: clubs.Count - representatives.Count);
    }

    private int SelectBest(List<int> clubs, (string Name, Step Apply)[] steps, string context, int eliminatedByDivision = 0)
    {
        var current = new List<int>(clubs);
        var decidedBy = eliminatedByDivision > 0 ? "Division tiebreaker" : string.Empty;

    Restart:
        if (current.Count > 1)
        {
            foreach (var (name, apply) in steps)
            {
                var scores = apply(current);
                if (scores is null)
                {
                    continue;
                }

                var best = scores.Max();
                var survivors = current.Where((_, i) => Math.Abs(scores[i] - best) < Epsilon).ToList();
                if (survivors.Count < current.Count)
                {
                    current = survivors;
                    decidedBy = name;
                    goto Restart;
                }
            }

            current = [coinToss.Pick(current, _league)];
            decidedBy = "Coin toss";
        }

        notes?.Add(new TiebreakNote(context, [.. clubs.Select(c => _league.Teams[c].Id)], _league.Teams[current[0]].Id, decidedBy));
        return current[0];
    }

    private (string, Step)[] DivisionSteps() =>
    [
        ("Head-to-head", HeadToHead),
        ("Division record", clubs => Each(clubs, c => table.PctVs(c, _league.DivisionMask[_league.DivisionOf[c]]) ?? 0)),
        ("Common games", clubs => CommonGames(clubs, minimumGames: 1)),
        ("Conference record", ConferenceRecord),
        ("Strength of victory", clubs => Each(clubs, c => table.OpponentStrength(c, victoriesOnly: true))),
        ("Strength of schedule", clubs => Each(clubs, c => table.OpponentStrength(c, victoriesOnly: false))),
        ("Points ranking (conference)", clubs => PointsRanking(clubs, conferenceOnly: true)),
        ("Points ranking (all)", clubs => PointsRanking(clubs, conferenceOnly: false)),
        ("Net points (common games)", NetPointsCommonGames),
        ("Net points (all games)", clubs => Each(clubs, c => table.PointsFor[c] - table.PointsAgainst[c])),
    ];

    private (string, Step)[] WildCardSteps() =>
    [
        ("Head-to-head", clubs => clubs.Count == 2 ? HeadToHead(clubs) : HeadToHeadSweep(clubs)),
        ("Conference record", ConferenceRecord),
        ("Common games", clubs => CommonGames(clubs, minimumGames: 4)),
        ("Strength of victory", clubs => Each(clubs, c => table.OpponentStrength(c, victoriesOnly: true))),
        ("Strength of schedule", clubs => Each(clubs, c => table.OpponentStrength(c, victoriesOnly: false))),
        ("Points ranking (conference)", clubs => PointsRanking(clubs, conferenceOnly: true)),
        ("Points ranking (all)", clubs => PointsRanking(clubs, conferenceOnly: false)),
        ("Net points (conference games)", clubs => Each(clubs, c => table.NetPointsVs(c, _league.ConferenceMask[_league.ConferenceOf(c)]))),
        ("Net points (all games)", clubs => Each(clubs, c => table.PointsFor[c] - table.PointsAgainst[c])),
    ];

    private static double[] Each(List<int> clubs, Func<int, double> value) => [.. clubs.Select(value)];

    private ulong MaskOf(List<int> clubs) => clubs.Aggregate(0UL, (mask, c) => mask | (1UL << c));

    private double[]? HeadToHead(List<int> clubs)
    {
        var mask = MaskOf(clubs);
        var scores = new double[clubs.Count];
        for (var i = 0; i < clubs.Count; i++)
        {
            // Every club must have played the others for head-to-head to apply.
            var others = mask & ~(1UL << clubs[i]);
            if ((table.Opponents(clubs[i]) & others) != others)
            {
                return null;
            }

            scores[i] = table.PctVs(clubs[i], others) ?? 0;
        }

        return scores;
    }

    /// <summary>Three or more clubs: applies only if one club beat each of the others, or lost to each of the others.</summary>
    private double[]? HeadToHeadSweep(List<int> clubs)
    {
        foreach (var club in clubs)
        {
            var others = clubs.Where(o => o != club).ToList();
            if (others.All(o => table.GamesVs(club, o) > 0 && table.WinsVs(club, o) == table.GamesVs(club, o)))
            {
                return Each(clubs, c => c == club ? 1 : 0);
            }
        }

        foreach (var club in clubs)
        {
            var others = clubs.Where(o => o != club).ToList();
            if (others.All(o => table.GamesVs(club, o) > 0 && table.LossesVs(club, o) == table.GamesVs(club, o)))
            {
                return Each(clubs, c => c == club ? 0 : 1);
            }
        }

        return null;
    }

    private double[] ConferenceRecord(List<int> clubs) =>
        Each(clubs, c => table.PctVs(c, _league.ConferenceMask[_league.ConferenceOf(c)]) ?? 0);

    private ulong CommonOpponents(List<int> clubs) =>
        clubs.Aggregate(ulong.MaxValue, (mask, c) => mask & table.Opponents(c)) & ~MaskOf(clubs);

    private double[]? CommonGames(List<int> clubs, int minimumGames)
    {
        var common = CommonOpponents(clubs);
        var scores = new double[clubs.Count];
        for (var i = 0; i < clubs.Count; i++)
        {
            var (w, l, t) = table.RecordVs(clubs[i], common);
            if (w + l + t < minimumGames)
            {
                return null;
            }

            scores[i] = (w + (0.5 * t)) / (w + l + t);
        }

        return scores;
    }

    private double[] NetPointsCommonGames(List<int> clubs)
    {
        var common = CommonOpponents(clubs);
        return Each(clubs, c => table.NetPointsVs(c, common));
    }

    /// <summary>Combined rank in points scored and points allowed; lower combined rank is better, so it is negated.</summary>
    private double[] PointsRanking(List<int> clubs, bool conferenceOnly)
    {
        return Each(clubs, c =>
        {
            var pool = conferenceOnly ? _league.ConferenceMembers[_league.ConferenceOf(c)] : Enumerable.Range(0, _league.Count);
            var scoredRank = 1 + pool.Count(o => table.PointsFor[o] > table.PointsFor[c]);
            var allowedRank = 1 + pool.Count(o => table.PointsAgainst[o] < table.PointsAgainst[c]);
            return -(scoredRank + allowedRank);
        });
    }
}
