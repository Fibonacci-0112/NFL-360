using System.Numerics;

namespace NflCoverage.Rules;

/// <summary>
/// Mutable accumulation of game results, stored as dense per-opponent matrices so tiebreakers
/// (head-to-head, common games, strength of victory) are cheap enough to run inside Monte Carlo loops.
/// </summary>
public sealed class SeasonTable
{
    private readonly int _n;
    private readonly int[] _winsVs;
    private readonly int[] _lossesVs;
    private readonly int[] _tiesVs;
    private readonly int[] _pointsForVs;
    private readonly int[] _pointsAgainstVs;
    private readonly ulong[] _opponents;

    public SeasonTable(League league)
    {
        League = league;
        _n = league.Count;
        _winsVs = new int[_n * _n];
        _lossesVs = new int[_n * _n];
        _tiesVs = new int[_n * _n];
        _pointsForVs = new int[_n * _n];
        _pointsAgainstVs = new int[_n * _n];
        _opponents = new ulong[_n];
        Wins = new int[_n];
        Losses = new int[_n];
        Ties = new int[_n];
        PointsFor = new int[_n];
        PointsAgainst = new int[_n];
    }

    public League League { get; }

    internal int[] Wins { get; }

    internal int[] Losses { get; }

    internal int[] Ties { get; }

    internal int[] PointsFor { get; }

    internal int[] PointsAgainst { get; }

    public static SeasonTable From(League league, IEnumerable<GameScore> games)
    {
        var table = new SeasonTable(league);
        foreach (var game in games)
        {
            table.Add(game);
        }

        return table;
    }

    public void Add(GameScore game) =>
        Add(League.IndexOf(game.HomeTeam), League.IndexOf(game.AwayTeam), game.HomeScore, game.AwayScore);

    public void Add(int home, int away, int homeScore, int awayScore)
    {
        Record(home, away, homeScore, awayScore);
        Record(away, home, awayScore, homeScore);
    }

    /// <summary>Overwrites this table with the contents of <paramref name="other"/> without allocating.</summary>
    public void CopyFrom(SeasonTable other)
    {
        if (!ReferenceEquals(other.League, League))
        {
            throw new ArgumentException("Tables must share the same league.", nameof(other));
        }

        other._winsVs.CopyTo(_winsVs, 0);
        other._lossesVs.CopyTo(_lossesVs, 0);
        other._tiesVs.CopyTo(_tiesVs, 0);
        other._pointsForVs.CopyTo(_pointsForVs, 0);
        other._pointsAgainstVs.CopyTo(_pointsAgainstVs, 0);
        other._opponents.CopyTo(_opponents, 0);
        other.Wins.CopyTo(Wins, 0);
        other.Losses.CopyTo(Losses, 0);
        other.Ties.CopyTo(Ties, 0);
        other.PointsFor.CopyTo(PointsFor, 0);
        other.PointsAgainst.CopyTo(PointsAgainst, 0);
    }

    public (int Wins, int Losses, int Ties, int PointsFor, int PointsAgainst) RecordOf(int team) =>
        (Wins[team], Losses[team], Ties[team], PointsFor[team], PointsAgainst[team]);

    public int GamesPlayed(int team) => Wins[team] + Losses[team] + Ties[team];

    public double WinPct(int team)
    {
        var games = GamesPlayed(team);
        return games == 0 ? 0 : (Wins[team] + (0.5 * Ties[team])) / games;
    }

    internal ulong Opponents(int team) => _opponents[team];

    internal int WinsVs(int team, int opponent) => _winsVs[(team * _n) + opponent];

    internal int LossesVs(int team, int opponent) => _lossesVs[(team * _n) + opponent];

    internal int GamesVs(int team, int opponent)
    {
        var k = (team * _n) + opponent;
        return _winsVs[k] + _lossesVs[k] + _tiesVs[k];
    }

    /// <summary>Win-loss-tie record against every opponent in <paramref name="mask"/>.</summary>
    internal (int Wins, int Losses, int Ties) RecordVs(int team, ulong mask)
    {
        int w = 0, l = 0, t = 0;
        var row = team * _n;
        while (mask != 0)
        {
            var j = BitOperations.TrailingZeroCount(mask);
            mask &= mask - 1;
            w += _winsVs[row + j];
            l += _lossesVs[row + j];
            t += _tiesVs[row + j];
        }

        return (w, l, t);
    }

    /// <summary>Win percentage against opponents in <paramref name="mask"/>, or <c>null</c> when no such games exist.</summary>
    internal double? PctVs(int team, ulong mask)
    {
        var (w, l, t) = RecordVs(team, mask);
        var games = w + l + t;
        return games == 0 ? null : (w + (0.5 * t)) / games;
    }

    internal int NetPointsVs(int team, ulong mask)
    {
        var net = 0;
        var row = team * _n;
        while (mask != 0)
        {
            var j = BitOperations.TrailingZeroCount(mask);
            mask &= mask - 1;
            net += _pointsForVs[row + j] - _pointsAgainstVs[row + j];
        }

        return net;
    }

    /// <summary>Combined win percentage of opponents, weighted by games won (SOV) or played (SOS) against each.</summary>
    internal double OpponentStrength(int team, bool victoriesOnly)
    {
        double opponentPoints = 0;
        double opponentGames = 0;
        var row = team * _n;
        for (var j = 0; j < _n; j++)
        {
            var weight = victoriesOnly
                ? _winsVs[row + j]
                : _winsVs[row + j] + _lossesVs[row + j] + _tiesVs[row + j];
            if (weight == 0)
            {
                continue;
            }

            opponentPoints += weight * (Wins[j] + (0.5 * Ties[j]));
            opponentGames += weight * GamesPlayed(j);
        }

        return opponentGames == 0 ? 0 : opponentPoints / opponentGames;
    }

    private void Record(int team, int opponent, int scored, int allowed)
    {
        var k = (team * _n) + opponent;
        if (scored > allowed)
        {
            Wins[team]++;
            _winsVs[k]++;
        }
        else if (scored < allowed)
        {
            Losses[team]++;
            _lossesVs[k]++;
        }
        else
        {
            Ties[team]++;
            _tiesVs[k]++;
        }

        PointsFor[team] += scored;
        PointsAgainst[team] += allowed;
        _pointsForVs[k] += scored;
        _pointsAgainstVs[k] += allowed;
        _opponents[team] |= 1UL << opponent;
    }
}
