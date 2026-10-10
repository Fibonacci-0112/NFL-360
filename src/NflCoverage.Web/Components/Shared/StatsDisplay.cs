using NflCoverage.Modules.Stats.Contracts;

namespace NflCoverage.Web.Components.Shared;

/// <summary>Which box-score columns a table shows.</summary>
public enum StatGroup
{
    Passing,
    Rushing,
    Receiving,
    Defense,
    Kicking,
    Fantasy,
}

internal sealed record CategoryInfo(LeaderCategory Category, string Label, StatGroup Group, string ValueFormat);

/// <summary>Display metadata for leaderboards and player tables.</summary>
internal static class StatsDisplay
{
    public static readonly IReadOnlyList<CategoryInfo> Categories =
    [
        new(LeaderCategory.PassingYards, "Passing yards", StatGroup.Passing, "N0"),
        new(LeaderCategory.PassingTds, "Passing TD", StatGroup.Passing, "0"),
        new(LeaderCategory.PasserRating, "Passer rating", StatGroup.Passing, "0.0"),
        new(LeaderCategory.PassingEpa, "Passing EPA", StatGroup.Passing, "+0.0;-0.0"),
        new(LeaderCategory.RushingYards, "Rushing yards", StatGroup.Rushing, "N0"),
        new(LeaderCategory.RushingTds, "Rushing TD", StatGroup.Rushing, "0"),
        new(LeaderCategory.ReceivingYards, "Receiving yards", StatGroup.Receiving, "N0"),
        new(LeaderCategory.Receptions, "Receptions", StatGroup.Receiving, "0"),
        new(LeaderCategory.ReceivingTds, "Receiving TD", StatGroup.Receiving, "0"),
        new(LeaderCategory.Tackles, "Tackles", StatGroup.Defense, "0"),
        new(LeaderCategory.Sacks, "Sacks", StatGroup.Defense, "0.#"),
        new(LeaderCategory.Interceptions, "Interceptions", StatGroup.Defense, "0"),
        new(LeaderCategory.FgMade, "Field goals", StatGroup.Kicking, "0"),
        new(LeaderCategory.FantasyPointsPpr, "Fantasy (PPR)", StatGroup.Fantasy, "0.0"),
    ];

    public static CategoryInfo Info(LeaderCategory category) => Categories.First(c => c.Category == category);

    /// <summary>The columns that best describe a player of this position group.</summary>
    public static IReadOnlyList<StatGroup> GroupsFor(string? positionGroup, IEnumerable<PlayerSeasonStats> career)
    {
        IReadOnlyList<StatGroup> groups = positionGroup switch
        {
            "QB" => [StatGroup.Passing, StatGroup.Rushing],
            "RB" => [StatGroup.Rushing, StatGroup.Receiving],
            "WR" or "TE" => [StatGroup.Receiving, StatGroup.Rushing],
            "DL" or "LB" or "DB" => [StatGroup.Defense],
            "SPEC" or "K" => [StatGroup.Kicking],
            _ => [],
        };

        if (groups.Count > 0)
        {
            return groups;
        }

        // Unknown position: show whatever the player actually did.
        var lines = career.Select(c => c.Stats).ToList();
        return
        [
            .. new (StatGroup Group, bool Used)[]
            {
                (StatGroup.Passing, lines.Any(s => s.Passing.Attempts > 0)),
                (StatGroup.Rushing, lines.Any(s => s.Rushing.Carries > 0)),
                (StatGroup.Receiving, lines.Any(s => s.Receiving.Targets > 0)),
                (StatGroup.Defense, lines.Any(s => s.Defense.Tackles > 0)),
                (StatGroup.Kicking, lines.Any(s => s.Kicking.FgAttempted > 0)),
            }.Where(g => g.Used).Select(g => g.Group),
        ];
    }

    /// <summary>Career totals. Rates (CPOE, target share) do not add up, so they are left empty.</summary>
    public static StatLine Sum(IEnumerable<StatLine> lines)
    {
        var all = lines.ToList();
        return new StatLine(
            new PassingStats(
                all.Sum(s => s.Passing.Completions), all.Sum(s => s.Passing.Attempts), all.Sum(s => s.Passing.Yards), all.Sum(s => s.Passing.Touchdowns),
                all.Sum(s => s.Passing.Interceptions), all.Sum(s => s.Passing.Sacks), all.Sum(s => s.Passing.Epa), null),
            new RushingStats(all.Sum(s => s.Rushing.Carries), all.Sum(s => s.Rushing.Yards), all.Sum(s => s.Rushing.Touchdowns), all.Sum(s => s.Rushing.Epa)),
            new ReceivingStats(
                all.Sum(s => s.Receiving.Receptions), all.Sum(s => s.Receiving.Targets), all.Sum(s => s.Receiving.Yards), all.Sum(s => s.Receiving.Touchdowns),
                all.Sum(s => s.Receiving.Epa), null),
            new DefenseStats(
                all.Sum(s => s.Defense.SoloTackles), all.Sum(s => s.Defense.AssistedTackles), all.Sum(s => s.Defense.TacklesForLoss), all.Sum(s => s.Defense.Sacks),
                all.Sum(s => s.Defense.QbHits), all.Sum(s => s.Defense.Interceptions), all.Sum(s => s.Defense.PassesDefended), all.Sum(s => s.Defense.ForcedFumbles)),
            new KickingStats(
                all.Sum(s => s.Kicking.FgMade), all.Sum(s => s.Kicking.FgAttempted), all.Max(s => s.Kicking.FgLong), all.Sum(s => s.Kicking.PatMade),
                all.Sum(s => s.Kicking.PatAttempted)),
            all.Sum(s => s.FantasyPointsPpr));
    }

    /// <summary>The single season stat that best sums up a player of this group, for the career chart.</summary>
    public static (string Label, Func<StatLine, double> Value) PrimaryStat(StatGroup group) => group switch
    {
        StatGroup.Passing => ("Passing yards", s => s.Passing.Yards),
        StatGroup.Rushing => ("Rushing yards", s => s.Rushing.Yards),
        StatGroup.Receiving => ("Receiving yards", s => s.Receiving.Yards),
        StatGroup.Defense => ("Tackles", s => s.Defense.Tackles),
        StatGroup.Kicking => ("Field goals made", s => s.Kicking.FgMade),
        _ => ("Fantasy points (PPR)", s => s.FantasyPointsPpr),
    };
}

/// <summary>A leaderboard row flattened to simple properties, so QuickGrid can sort it by property name.</summary>
internal sealed record LeaderView(
    int Rank,
    string PlayerId,
    string Name,
    string? Position,
    string? Team,
    string? HeadshotUrl,
    int Games,
    double Value,
    int Completions,
    int Attempts,
    int PassingYards,
    int PassingTds,
    int Interceptions,
    double Rating,
    double PassingEpa,
    int Carries,
    int RushingYards,
    double? YardsPerCarry,
    int RushingTds,
    double RushingEpa,
    int Receptions,
    int Targets,
    int ReceivingYards,
    double? YardsPerReception,
    int ReceivingTds,
    double? CatchRate,
    int Tackles,
    double Sacks,
    double TacklesForLoss,
    int QbHits,
    int DefInterceptions,
    int PassesDefended,
    int ForcedFumbles,
    int FgMade,
    int FgAttempted,
    double? FgPct,
    int? FgLong,
    int PatMade,
    int PatAttempted,
    double FantasyPointsPpr,
    int Touchdowns)
{
    public static LeaderView From(LeaderRow row)
    {
        var s = row.Stats;
        return new LeaderView(
            row.Rank, row.Player.Id, row.Player.Name, row.Player.Position, row.Team, row.Player.HeadshotUrl, row.Games, row.Value,
            s.Passing.Completions, s.Passing.Attempts, s.Passing.Yards, s.Passing.Touchdowns, s.Passing.Interceptions,
            StatFormulas.PasserRating(s.Passing), s.Passing.Epa,
            s.Rushing.Carries, s.Rushing.Yards, StatFormulas.YardsPerCarry(s.Rushing.Yards, s.Rushing.Carries), s.Rushing.Touchdowns, s.Rushing.Epa,
            s.Receiving.Receptions, s.Receiving.Targets, s.Receiving.Yards, StatFormulas.YardsPerReception(s.Receiving.Yards, s.Receiving.Receptions),
            s.Receiving.Touchdowns, StatFormulas.CatchRate(s.Receiving.Receptions, s.Receiving.Targets),
            s.Defense.Tackles, s.Defense.Sacks, s.Defense.TacklesForLoss, s.Defense.QbHits, s.Defense.Interceptions, s.Defense.PassesDefended,
            s.Defense.ForcedFumbles,
            s.Kicking.FgMade, s.Kicking.FgAttempted, StatFormulas.FieldGoalPct(s.Kicking.FgMade, s.Kicking.FgAttempted), s.Kicking.FgLong,
            s.Kicking.PatMade, s.Kicking.PatAttempted,
            s.FantasyPointsPpr, s.Passing.Touchdowns + s.Rushing.Touchdowns + s.Receiving.Touchdowns);
    }
}
