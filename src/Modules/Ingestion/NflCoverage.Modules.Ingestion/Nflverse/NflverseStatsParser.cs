using System.Globalization;
using nietras.SeparatedValues;
using NflCoverage.Modules.Stats.Contracts;

namespace NflCoverage.Modules.Ingestion.Nflverse;

/// <summary>
/// Anti-corruption layer for the nflverse statistics files (<c>stats_player_*</c>, <c>stats_team_*</c> and
/// <c>players.csv</c>). The curated columns become typed fields; every other non-empty, non-zero column is passed on
/// as an "extra" so nothing from the source is lost.
/// </summary>
internal static class NflverseStatsParser
{
    public static List<PlayerUpsert> ParsePlayers(TextReader csv) =>
        Parse(csv, r =>
        {
            var id = r.Text("gsis_id");
            var name = r.Text("display_name");
            return id is null || name is null
                ? null
                : new PlayerUpsert(
                    new PlayerProfile(
                        id,
                        name,
                        r.Text("position"),
                        r.Text("position_group"),
                        r.Text("headshot"),
                        r.Date("birth_date"),
                        r.Text("college_name"),
                        r.IntOrNull("draft_year"),
                        r.IntOrNull("draft_round"),
                        r.IntOrNull("draft_pick"),
                        r.Team("draft_team"),
                        r.IntOrNull("rookie_season"),
                        r.IntOrNull("last_season"),
                        r.Team("latest_team")),
                    r.Extra());
        });

    public static List<PlayerSeasonUpsert> ParsePlayerSeasons(TextReader csv) =>
        Parse(csv, r =>
        {
            var id = r.Text("player_id");
            if (id is null)
            {
                return null;
            }

            var stats = new PlayerSeasonStats(id, r.Int("season"), SeasonTypes.Parse(r.Text("season_type")), r.Team("recent_team"), r.Int("games"), Line(r));
            return new PlayerSeasonUpsert(
                stats, r.Text("player_display_name") ?? id, r.Text("position"), r.Text("position_group"), r.Text("headshot_url"), r.Extra());
        });

    public static List<PlayerGameUpsert> ParsePlayerGames(TextReader csv) =>
        Parse(csv, r =>
        {
            var id = r.Text("player_id");
            var gameId = r.Text("game_id");
            if (id is null || gameId is null)
            {
                return null;
            }

            // Name, position and headshot repeat on every row; they live in the player directory.
            r.Skip("player_name", "player_display_name", "position", "position_group", "headshot_url");
            return new PlayerGameUpsert(
                new PlayerGameStats(
                    id, gameId, r.Int("season"), r.Int("week"), SeasonTypes.Parse(r.Text("season_type")), r.Team("team"), r.Team("opponent_team"), Line(r)),
                r.Extra());
        });

    public static List<TeamSeasonUpsert> ParseTeamSeasons(TextReader csv) =>
        Parse(csv, r =>
        {
            var team = r.Team("team");
            return team is null
                ? null
                : new TeamSeasonUpsert(
                    new TeamSeasonStats(
                        team, r.Int("season"), SeasonTypes.Parse(r.Text("season_type")), r.Int("games"), Offense(r), Defense(r), r.Int("penalties"), r.Int("penalty_yards")),
                    r.Extra());
        });

    public static List<TeamGameUpsert> ParseTeamGames(TextReader csv) =>
        Parse(csv, r =>
        {
            var team = r.Team("team");
            var opponent = r.Team("opponent_team");
            var gameId = r.Text("game_id");
            return team is null || opponent is null || gameId is null
                ? null
                : new TeamGameUpsert(
                    new TeamGameStats(team, gameId, r.Int("season"), r.Int("week"), SeasonTypes.Parse(r.Text("season_type")), opponent, Offense(r)),
                    r.Extra());
        });

    /// <remarks>
    /// nflverse splits tackles three ways: unassisted (<c>def_tackles_solo</c>), shared tackles where the player is
    /// credited as a tackler (<c>def_tackles_with_assist</c>) and assists (<c>def_tackle_assists</c>). The official
    /// solo count is the first two together, so solo + assists matches the official combined total.
    /// </remarks>
    private static StatLine Line(Row r) => new(
        new PassingStats(
            r.Int("completions"), r.Int("attempts"), r.Int("passing_yards"), r.Int("passing_tds"), r.Int("passing_interceptions"),
            r.Int("sacks_suffered"), r.Double("passing_epa") ?? 0, r.Double("passing_cpoe")),
        new RushingStats(r.Int("carries"), r.Int("rushing_yards"), r.Int("rushing_tds"), r.Double("rushing_epa") ?? 0),
        new ReceivingStats(
            r.Int("receptions"), r.Int("targets"), r.Int("receiving_yards"), r.Int("receiving_tds"), r.Double("receiving_epa") ?? 0, r.Double("target_share")),
        new DefenseStats(
            r.Int("def_tackles_solo") + r.Int("def_tackles_with_assist"), r.Int("def_tackle_assists"), r.Double("def_tackles_for_loss") ?? 0,
            r.Double("def_sacks") ?? 0, r.Int("def_qb_hits"), r.Int("def_interceptions"), r.Int("def_pass_defended"), r.Int("def_fumbles_forced")),
        new KickingStats(r.Int("fg_made"), r.Int("fg_att"), r.IntOrNull("fg_long"), r.Int("pat_made"), r.Int("pat_att")),
        r.Double("fantasy_points_ppr") ?? 0);

    /// <remarks>Fumbles lost include special teams, so giveaways match the official turnover count.</remarks>
    private static TeamOffense Offense(Row r) => new(
        r.Int("attempts"), r.Int("completions"), r.Int("passing_yards"), r.Int("passing_tds"), r.Int("passing_interceptions"), r.Int("sacks_suffered"),
        r.Int("carries"), r.Int("rushing_yards"), r.Int("rushing_tds"), r.Int("fumbles_lost_total"), r.Double("passing_epa") ?? 0, r.Double("rushing_epa") ?? 0);

    private static TeamDefense Defense(Row r) => new(
        r.Int("def_tackles_solo") + r.Int("def_tackles_with_assist"), r.Double("def_tackles_for_loss") ?? 0, r.Double("def_sacks") ?? 0, r.Int("def_qb_hits"),
        r.Int("def_interceptions"), r.Int("def_pass_defended"), r.Int("def_fumbles_forced"), r.Int("def_tds"));

    private static List<T> Parse<T>(TextReader csv, Func<Row, T?> map)
        where T : class
    {
        var results = new List<T>();
        using var reader = Sep.Reader(o => o with { Unescape = true }).From(csv);
        var row = new Row(reader.Header.ColNames);
        foreach (var source in reader)
        {
            row.Load(source);
            if (map(row) is { } result)
            {
                results.Add(result);
            }
        }

        return results;
    }

    /// <summary>
    /// One CSV row, read by column name. Columns that are read are remembered, so the rest can be returned as extras.
    /// A missing column reads as empty, which tolerates nflverse adding or dropping columns between releases.
    /// </summary>
    private sealed class Row(IReadOnlyList<string> names)
    {
        private readonly Dictionary<string, int> _index = names.Select((name, i) => (name, i)).ToDictionary(c => c.name, c => c.i, StringComparer.Ordinal);
        private readonly bool[] _read = new bool[names.Count];
        private readonly string[] _values = new string[names.Count];

        public void Load(SepReader.Row source)
        {
            for (var i = 0; i < _values.Length; i++)
            {
                _values[i] = i < source.ColCount ? source[i].ToString() : "";
            }
        }

        public string? Text(string name)
        {
            if (!_index.TryGetValue(name, out var i))
            {
                return null;
            }

            _read[i] = true;
            var value = _values[i];
            return value.Length == 0 || value == "NA" ? null : value;
        }

        public string? Team(string name) => Text(name) is { } code ? FranchiseMap.ToFranchise(code) : null;

        public double? Double(string name) =>
            double.TryParse(Text(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

        /// <summary>Counts are integers, but parse leniently in case a file writes them as "3.0".</summary>
        public int? IntOrNull(string name) => Double(name) is { } value ? (int)Math.Round(value) : null;

        public int Int(string name) => IntOrNull(name) ?? 0;

        public DateOnly? Date(string name) =>
            DateOnly.TryParseExact(Text(name), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

        public void Skip(params string[] columns)
        {
            foreach (var column in columns)
            {
                _ = Text(column);
            }
        }

        /// <summary>Unread columns with a value; empty, NA and zero are left out to keep the JSON small.</summary>
        public Dictionary<string, string> Extra()
        {
            var extra = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < _values.Length; i++)
            {
                var value = _values[i];
                if (!_read[i] && value.Length > 0 && value != "NA"
                    && !(double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number == 0))
                {
                    extra[names[i]] = value;
                }
            }

            return extra;
        }
    }
}
