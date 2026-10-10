using System.Text.RegularExpressions;
using NflCoverage.Modules.Ingestion.Nflverse;
using NflCoverage.Web.Components.Shared;

namespace NflCoverage.IntegrationTests;

/// <summary>Team-colored banners and filled accents keep white text readable whatever the team's colors.</summary>
public partial class TeamColorsTests
{
    private const double Minimum = 5; // 4.5:1 for normal text, plus headroom for the banner's soft highlight.

    public static TheoryData<string, string> Teams()
    {
        var data = new TheoryData<string, string>();
        foreach (var team in NflverseParser.ParseTeams(new StringReader(File.ReadAllText(NflAppFixture.DataPath("teams_colors_logos.csv")))))
        {
            data.Add(team.Id, team.PrimaryColor);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Teams))]
    public void Every_team_shade_carries_white_text(string team, string primary)
    {
        var style = Format.TeamColors(primary, "#000000");

        WhiteContrast(primary, Percent(style, "team-depth")).Should().BeGreaterThanOrEqualTo(Minimum, $"banner and pill text on {team}'s {primary}");
    }

    [Theory]
    [InlineData("#E31837", 88)] // Chiefs red already reads at the default shade.
    [InlineData("#000000", 88)]
    [InlineData("#4495D2", 78)] // Titans blue: white is only 3.2:1 on the raw color.
    [InlineData("#008E97", 86)] // Dolphins teal: neither white nor dark text reaches 4.5:1 on the raw color.
    [InlineData("#D3BC8D", 58)] // Saints gold needs a much deeper shade.
    [InlineData("#FFFFFF", 42)]
    public void Shade_is_the_lightest_that_reaches_the_minimum(string primary, int depth)
    {
        var style = Format.TeamColors(primary, "#000000");

        style.Should().StartWith($"--team-color:{primary};--team-color-2:#000000;");
        Percent(style, "team-depth").Should().Be(depth);
        if (depth < 88)
        {
            WhiteContrast(primary, depth + 2).Should().BeLessThan(Minimum, "two points lighter would not be readable");
        }

        Percent(style, "team-depth-end").Should().Be(Math.Min(depth, 50), "the banner gradient never lightens toward its end");
    }

    [Theory]
    [InlineData("")]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    public void Unparseable_colors_count_as_dark(string color) =>
        Format.RelativeLuminance(color).Should().Be(0);

    // CSS color-mix with black in sRGB scales each channel by the depth.
    private static double WhiteContrast(string primary, int depth)
    {
        var rgb = Convert.ToInt32(primary[1..], 16);
        int Scale(int shift) => (int)Math.Round(((rgb >> shift) & 0xFF) * depth / 100.0);
        var mixed = $"#{Scale(16):X2}{Scale(8):X2}{Scale(0):X2}";
        return Format.ContrastRatio(1, Format.RelativeLuminance(mixed));
    }

    private static string Variable(string style, string name) => VariablePattern().Matches(style).Single(m => m.Groups[1].Value == name).Groups[2].Value;

    private static int Percent(string style, string name) => int.Parse(Variable(style, name).TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture);

    [GeneratedRegex("--([a-z0-9-]+):([^;]+)")]
    private static partial Regex VariablePattern();
}
