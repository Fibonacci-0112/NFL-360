namespace NflCoverage.Rules;

/// <summary>A completed game. Ties are represented by equal scores.</summary>
public readonly record struct GameScore(string HomeTeam, string AwayTeam, int HomeScore, int AwayScore);
