using System.Globalization;
using NflCoverage.Modules.History.Contracts;
using NflCoverage.Modules.League.Contracts;
using NflCoverage.Web.Client.Models;

namespace NflCoverage.Web.Components.Shared;

internal static class Format
{
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Percent(double p) => p switch
    {
        >= 0.9995 => "✓",
        <= 0.0005 => "–",
        < 0.01 => "<1%",
        > 0.99 => ">99%",
        _ => $"{p * 100:0}%",
    };

    public static string WinPct(double pct) => pct.ToString(".000", System.Globalization.CultureInfo.InvariantCulture).TrimStart('0');

    public static string Record(int wins, int losses, int ties) => ties == 0 ? $"{wins}-{losses}" : $"{wins}-{losses}-{ties}";

    public static string Kickoff(DateTimeOffset? kickoff) =>
        kickoff is { } k ? TimeZoneInfo.ConvertTime(k, Eastern).ToString("ddd MMM d, h:mm tt 'ET'", System.Globalization.CultureInfo.InvariantCulture) : "TBD";

    public static Dictionary<string, TeamInfo> Directory(IEnumerable<TeamDto> teams) =>
        teams.ToDictionary(t => t.Id, t => new TeamInfo(t.Id, t.Name, t.Conference, t.Division, t.PrimaryColor, t.LogoUrl));

    public static string Number(double value) => value.ToString("#,0.#", Invariant);

    public static string Decimal(double? value, string format = "0.0") => value?.ToString(format, Invariant) ?? "–";

    /// <summary>Expected points added, signed: +0.12.</summary>
    public static string Epa(double? value, string format = "+0.00;-0.00;0.00") => value?.ToString(format, Invariant) ?? "–";

    public static string Ratio(double? value) => value is { } v ? (v * 100).ToString("0.0", Invariant) + "%" : "–";

    public static string Ordinal(int n) => (n % 100) switch
    {
        11 or 12 or 13 => $"{n}th",
        _ => (n % 10) switch { 1 => $"{n}st", 2 => $"{n}nd", 3 => $"{n}rd", _ => $"{n}th" },
    };

    /// <param name="eliminated">Whether the team lost its last playoff game, rather than still being alive.</param>
    public static string Playoffs(PlayoffRound round, bool eliminated = true) => (round, eliminated) switch
    {
        (PlayoffRound.None, _) => "",
        (PlayoffRound.Champion, _) => "Won Super Bowl",
        (PlayoffRound.WildCard, true) => "Lost wild card round",
        (PlayoffRound.Divisional, true) => "Lost divisional round",
        (PlayoffRound.Conference, true) => "Lost conference championship",
        (PlayoffRound.SuperBowl, true) => "Lost Super Bowl",
        (PlayoffRound.WildCard, false) => "Wild card round",
        (PlayoffRound.Divisional, false) => "Divisional round",
        (PlayoffRound.Conference, false) => "Conference championship",
        _ => "Super Bowl",
    };

    public static string Invariantly(FormattableString text) => FormattableString.Invariant(text);
}
