namespace NflCoverage.Rules;

public sealed record TiebreakNote(string Context, IReadOnlyList<string> TiedTeams, string Winner, string DecidedBy);

public sealed record TeamStanding(
    string TeamId,
    Conference Conference,
    string Division,
    int Wins,
    int Losses,
    int Ties,
    double WinPct,
    int PointsFor,
    int PointsAgainst,
    string DivisionRecord,
    string ConferenceRecord,
    int DivisionRank,
    int ConferenceRank,
    int? Seed);

public sealed record LeagueStandings(IReadOnlyList<TeamStanding> Teams, IReadOnlyList<TiebreakNote> Notes)
{
    /// <summary>Playoff seeds for a conference, in seed order.</summary>
    public IReadOnlyList<TeamStanding> Seeds(Conference conference) =>
        [.. Teams.Where(t => t.Conference == conference && t.Seed is not null).OrderBy(t => t.Seed)];

    /// <summary>All teams in a division, in finishing order.</summary>
    public IReadOnlyList<TeamStanding> Division(string division) =>
        [.. Teams.Where(t => t.Division == division).OrderBy(t => t.DivisionRank)];
}

public static class StandingsCalculator
{
    /// <summary>Number of playoff clubs per conference for a given season (expanded from 6 to 7 in 2020).</summary>
    public static int PlayoffTeamsPerConference(int season) => season >= 2020 ? 7 : 6;

    public static LeagueStandings Compute(
        League league,
        IEnumerable<GameScore> games,
        int playoffTeamsPerConference,
        ICoinToss? coinToss = null) =>
        Compute(SeasonTable.From(league, games), playoffTeamsPerConference, coinToss);

    public static LeagueStandings Compute(SeasonTable table, int playoffTeamsPerConference, ICoinToss? coinToss = null)
    {
        var league = table.League;
        var notes = new List<TiebreakNote>();
        var tiebreaker = new Tiebreaker(table, coinToss ?? CoinToss.Deterministic, notes);
        tiebreaker.RankDivisions();

        var seeds = new int?[league.Count];
        var conferenceRanks = new int[league.Count];
        foreach (var members in league.ConferenceMembers)
        {
            var order = OrderConference(tiebreaker, league, members, playoffTeamsPerConference, rankEveryone: true);
            for (var i = 0; i < order.Count; i++)
            {
                conferenceRanks[order[i]] = i + 1;
                seeds[order[i]] = i < playoffTeamsPerConference ? i + 1 : null;
            }
        }

        var standings = Enumerable.Range(0, league.Count).Select(i =>
        {
            var team = league.Teams[i];
            return new TeamStanding(
                team.Id,
                team.Conference,
                team.Division,
                table.Wins[i],
                table.Losses[i],
                table.Ties[i],
                table.WinPct(i),
                table.PointsFor[i],
                table.PointsAgainst[i],
                FormatRecord(table.RecordVs(i, league.DivisionMask[league.DivisionOf[i]])),
                FormatRecord(table.RecordVs(i, league.ConferenceMask[league.ConferenceOf(i)])),
                tiebreaker.DivisionRanks[i],
                conferenceRanks[i],
                seeds[i]);
        }).ToList();

        return new LeagueStandings(standings, notes);
    }

    /// <summary>
    /// Allocation-light path for simulations: returns the seeded team indices per conference
    /// (<c>result[conference][seed - 1]</c>) without building display models or tiebreak notes.
    /// </summary>
    public static int[][] ComputeSeeds(SeasonTable table, int playoffTeamsPerConference, ICoinToss coinToss)
    {
        var league = table.League;
        var tiebreaker = new Tiebreaker(table, coinToss, notes: null);
        tiebreaker.RankDivisions();
        return
        [
            .. league.ConferenceMembers.Select(members =>
                OrderConference(tiebreaker, league, members, playoffTeamsPerConference, rankEveryone: false).ToArray()),
        ];
    }

    private static List<int> OrderConference(Tiebreaker tiebreaker, League league, int[] members, int playoffTeams, bool rankEveryone)
    {
        var conferenceName = league.Teams[members[0]].Conference.ToString().ToUpperInvariant();
        var divisionWinners = members.Where(m => tiebreaker.DivisionRanks[m] == 1).ToList();
        var order = tiebreaker.OrderAcrossDivisions(divisionWinners, $"{conferenceName} division-winner seeding");

        var others = members.Where(m => tiebreaker.DivisionRanks[m] != 1).ToList();
        var limit = rankEveryone ? others.Count : playoffTeams - divisionWinners.Count;
        order.AddRange(tiebreaker.OrderAcrossDivisions(others, $"{conferenceName} wild card", limit));
        return order;
    }

    private static string FormatRecord((int Wins, int Losses, int Ties) r) =>
        r.Ties == 0 ? $"{r.Wins}-{r.Losses}" : $"{r.Wins}-{r.Losses}-{r.Ties}";
}
