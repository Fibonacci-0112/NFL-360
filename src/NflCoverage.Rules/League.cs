namespace NflCoverage.Rules;

public enum Conference
{
    Afc = 0,
    Nfc = 1,
}

/// <summary>A club as seen by the rules engine: an id plus its conference and division membership.</summary>
public sealed record Team(string Id, Conference Conference, string Division);

/// <summary>Immutable league alignment. Teams are addressed internally by index for speed.</summary>
public sealed class League
{
    private readonly Dictionary<string, int> _indexById;

    private League(Team[] teams)
    {
        Teams = teams;
        _indexById = new Dictionary<string, int>(teams.Length, StringComparer.Ordinal);
        for (var i = 0; i < teams.Length; i++)
        {
            if (!_indexById.TryAdd(teams[i].Id, i))
            {
                throw new ArgumentException($"Duplicate team id '{teams[i].Id}'.", nameof(teams));
            }
        }

        DivisionNames = [.. teams.Select(t => t.Division).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        DivisionOf = [.. teams.Select(t => Array.IndexOf(DivisionNames, t.Division))];
        DivisionMembers = [.. DivisionNames.Select((_, d) => Enumerable.Range(0, teams.Length).Where(i => DivisionOf[i] == d).ToArray())];
        ConferenceMembers =
        [
            .. Enum.GetValues<Conference>().Select(c => Enumerable.Range(0, teams.Length).Where(i => teams[i].Conference == c).ToArray()),
        ];

        DivisionMask = new ulong[DivisionNames.Length];
        for (var d = 0; d < DivisionNames.Length; d++)
        {
            foreach (var i in DivisionMembers[d])
            {
                DivisionMask[d] |= 1UL << i;
            }
        }

        ConferenceMask = new ulong[ConferenceMembers.Length];
        for (var c = 0; c < ConferenceMembers.Length; c++)
        {
            foreach (var i in ConferenceMembers[c])
            {
                ConferenceMask[c] |= 1UL << i;
            }
        }
    }

    public IReadOnlyList<Team> Teams { get; }

    public int Count => Teams.Count;

    internal string[] DivisionNames { get; }

    internal int[] DivisionOf { get; }

    internal int[][] DivisionMembers { get; }

    internal int[][] ConferenceMembers { get; }

    internal ulong[] DivisionMask { get; }

    internal ulong[] ConferenceMask { get; }

    public static League Create(IEnumerable<Team> teams)
    {
        var array = teams.ToArray();
        if (array.Length is 0 or > 64)
        {
            throw new ArgumentException("A league must have between 1 and 64 teams.", nameof(teams));
        }

        return new League(array);
    }

    public int IndexOf(string teamId) =>
        _indexById.TryGetValue(teamId, out var index)
            ? index
            : throw new KeyNotFoundException($"Unknown team id '{teamId}'.");

    public bool TryGetIndex(string teamId, out int index) => _indexById.TryGetValue(teamId, out index);

    internal int ConferenceOf(int team) => (int)Teams[team].Conference;
}
