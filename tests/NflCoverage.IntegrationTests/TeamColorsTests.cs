using NflCoverage.Web.Components.Shared;

namespace NflCoverage.IntegrationTests;

/// <summary>Team-colored banners pick a deeper shade and dark accent text for light team colors.</summary>
public class TeamColorsTests
{
    [Theory]
    [InlineData("#D3BC8D", true)] // Saints gold
    [InlineData("#E31837", false)] // Chiefs red
    [InlineData("#4495D2", false)] // Titans light blue still carries white text
    [InlineData("#000000", false)]
    [InlineData("#FFFFFF", true)]
    public void Light_primaries_get_a_deeper_banner_and_dark_ink(string primary, bool light)
    {
        var style = Format.TeamColors(primary, "#000000");

        style.Should().Contain($"--team-color:{primary};--team-color-2:#000000");
        style.Should().Contain(light ? "--team-depth:60%;--team-ink:#14213D" : "--team-depth:88%;--team-ink:#fff");
    }

    [Theory]
    [InlineData("")]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    public void Unparseable_colors_count_as_dark(string color) =>
        Format.RelativeLuminance(color).Should().Be(0);
}
