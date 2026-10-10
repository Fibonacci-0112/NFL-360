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

    /// <summary>
    /// CSS custom properties for a team-colored banner or page: the two team colors and how much of the primary to
    /// keep when darkening it toward black so white text on it reaches 5:1. Light primaries such as the Saints' gold
    /// need a deeper shade; mid-tones such as the Dolphins' teal cannot carry readable text of either color undarkened.
    /// A <paramref name="prefix"/> other than "team" names the properties for one of several teams on a page
    /// (head-to-head uses "a" and "b").
    /// </summary>
    public static string TeamColors(string primary, string secondary, string prefix = "team")
    {
        var depth = BannerDepth(primary);
        return $"--{prefix}-color:{primary};--{prefix}-color-2:{secondary};--{prefix}-depth:{depth}%;--{prefix}-depth-end:{Math.Min(depth, 50)}%";
    }

    /// <summary>
    /// The largest share of the primary (in a color-mix with black, starting from 88%) that keeps white text at 5:1
    /// or better: 4.5:1 for normal text plus headroom for the banner's soft highlight.
    /// </summary>
    internal static int BannerDepth(string primary)
    {
        var rgb = ParseRgb(primary) ?? 0;
        for (var depth = 88; depth > 0; depth -= 2)
        {
            var mixed = RelativeLuminance(Scale(rgb >> 16, depth), Scale(rgb >> 8, depth), Scale(rgb, depth));
            if (ContrastRatio(1, mixed) >= 5)
            {
                return depth;
            }
        }

        return 0;

        static int Scale(int channel, int percent) => (int)Math.Round((channel & 0xFF) * percent / 100.0);
    }

    /// <summary>WCAG contrast ratio between two relative luminances.</summary>
    internal static double ContrastRatio(double a, double b) => (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);

    /// <summary>WCAG relative luminance of a #rrggbb color; unparseable colors count as dark.</summary>
    internal static double RelativeLuminance(string hex) =>
        ParseRgb(hex) is { } rgb ? RelativeLuminance(rgb >> 16, rgb >> 8, rgb) : 0;

    private static double RelativeLuminance(int r, int g, int b)
    {
        static double Channel(int value)
        {
            var c = (value & 0xFF) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(r)) + (0.7152 * Channel(g)) + (0.0722 * Channel(b));
    }

    private static int? ParseRgb(string hex) =>
        hex is ['#', _, _, _, _, _, _] && int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, Invariant, out var rgb) ? rgb : null;

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
