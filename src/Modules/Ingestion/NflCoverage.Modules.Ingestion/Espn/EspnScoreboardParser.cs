using System.Text.Json;
using NflCoverage.Modules.League.Contracts;

namespace NflCoverage.Modules.Ingestion.Espn;

/// <summary>A live score as ESPN reports it, keyed by ESPN's event id.</summary>
internal sealed record EspnScore(string EventId, int HomeScore, int AwayScore, GameStatus Status, string? Detail);

/// <summary>
/// Anti-corruption layer for ESPN's public (undocumented) scoreboard JSON. If the format changes or the
/// endpoint disappears, only this file and the poller need replacing.
/// </summary>
internal static class EspnScoreboardParser
{
    public static List<EspnScore> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var scores = new List<EspnScore>();
        if (!doc.RootElement.TryGetProperty("events", out var events))
        {
            return scores;
        }

        foreach (var e in events.EnumerateArray())
        {
            var competition = e.GetProperty("competitions")[0];
            var statusType = competition.GetProperty("status").GetProperty("type");
            var state = statusType.GetProperty("state").GetString();
            var completed = statusType.GetProperty("completed").GetBoolean();
            var status = (state, completed) switch
            {
                ("post", true) => GameStatus.Final,
                ("in", _) => GameStatus.InProgress,
                _ => GameStatus.Scheduled,
            };
            if (status == GameStatus.Scheduled)
            {
                continue;
            }

            int home = 0, away = 0;
            foreach (var competitor in competition.GetProperty("competitors").EnumerateArray())
            {
                var score = int.TryParse(competitor.GetProperty("score").GetString(), out var s) ? s : 0;
                if (competitor.GetProperty("homeAway").GetString() == "home")
                {
                    home = score;
                }
                else
                {
                    away = score;
                }
            }

            var detail = statusType.TryGetProperty("shortDetail", out var d) ? d.GetString() : null;
            scores.Add(new EspnScore(e.GetProperty("id").GetString()!, home, away, status, detail));
        }

        return scores;
    }
}
