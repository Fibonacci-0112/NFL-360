using NflCoverage.Modules.League.Contracts;
using NflCoverage.Web.Client.Models;

namespace NflCoverage.Web.Components.Shared;

internal static class Format
{
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

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
}
