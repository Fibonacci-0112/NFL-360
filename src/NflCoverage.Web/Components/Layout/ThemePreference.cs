namespace NflCoverage.Web.Components.Layout;

/// <summary>
/// The visitor's light/dark choice, kept in a cookie so the server renders the matching palette on every request.
/// wwwroot/js/theme.js sets it from the system preference on the first visit and flips it from the header toggle.
/// </summary>
internal static class ThemePreference
{
    public const string CookieName = "theme";

    public static bool IsDark(HttpContext? context) =>
        context?.Request.Cookies[CookieName] is "dark";
}
