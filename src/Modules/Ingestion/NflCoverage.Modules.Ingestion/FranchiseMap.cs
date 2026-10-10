namespace NflCoverage.Modules.Ingestion;

/// <summary>
/// Canonical team ids are current franchise abbreviations, so relocated clubs keep one continuous history.
/// Providers' historical or alternative codes are mapped here, at the edge.
/// </summary>
internal static class FranchiseMap
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OAK"] = "LV",
        ["SD"] = "LAC",
        ["STL"] = "LA",
        ["LAR"] = "LA",
        ["WSH"] = "WAS", // ESPN
    };

    public static string ToFranchise(string code) => Aliases.TryGetValue(code, out var id) ? id : code.ToUpperInvariant();

    public static bool IsHistoricalAlias(string code) => Aliases.ContainsKey(code);
}
