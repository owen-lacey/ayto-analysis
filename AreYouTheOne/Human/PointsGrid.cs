using AreYouTheOne.Entities;

namespace AreYouTheOne.Human;

public enum PairState
{
    Unknown,
    Confirmed,
    Eliminated
}

/// <summary>
/// The pen-and-paper state a contestant could keep: an integer score per (man, woman) pair, which pairs are
/// confirmed or eliminated, and the past match-ups. Scores are log-odds evidence in tenths of a decade
/// ("decibels"), so evidence from separate match-ups simply adds.
/// </summary>
public class PointsGrid
{
    private readonly PairState[,] _states;

    public PointsGrid(int pairs)
    {
        Pairs = pairs;
        Scores = new int[pairs, pairs];
        _states = new PairState[pairs, pairs];
    }

    public int Pairs { get; }

    public int[,] Scores { get; }

    public List<MatchUp> History { get; } = [];

    public PairState State(int man, int woman) => _states[man, woman];

    public bool IsUnknown(int man, int woman) => _states[man, woman] == PairState.Unknown;

    public void ApplyTruthBooth(TruthBooth truthBooth)
    {
        if (truthBooth.Result)
        {
            Confirm(truthBooth.A, truthBooth.B);
        }
        else
        {
            Eliminate(truthBooth.A, truthBooth.B);
        }
    }

    /// <summary>
    /// Naive Bayes update for a match-up scoring k: remove confirmed pairs (k' = k - confirmed) and known zeros from
    /// the lineup, then credit each remaining unknown pair with the same log-odds increment.
    /// </summary>
    public void ApplyMatchUp(MatchUp matchUp)
    {
        History.Add(matchUp);

        var confirmed = matchUp.Matching.Count(p => _states[p.A, p.B] == PairState.Confirmed);
        var unknown = matchUp.Matching.Where(p => _states[p.A, p.B] == PairState.Unknown).ToList();
        var kPrime = matchUp.Score - confirmed;
        var nPrime = unknown.Count;

        if (nPrime == 0)
        {
            return;
        }

        if (kPrime == 0)
        {
            // Blackout among the unknown pairs
            unknown.ForEach(p => Eliminate(p.A, p.B));
        }
        else if (kPrime == nPrime)
        {
            unknown.ForEach(p => Confirm(p.A, p.B));
        }
        else
        {
            var increment = Increment(kPrime, nPrime);
            unknown.ForEach(p => Scores[p.A, p.B] += increment);
        }
    }

    /// <summary>
    /// round(10 * log10((k'/(n'-k')) / (1/(n'-1)))): the likelihood ratio of "this pair is a match" given k' of
    /// n' unknown pairs scored, against the prior odds of a pair in a row of n' candidates.
    /// </summary>
    public static int Increment(int kPrime, int nPrime)
    {
        var odds = (double)kPrime / (nPrime - kPrime);
        var priorOdds = 1d / (nPrime - 1);
        return (int)Math.Round(10 * Math.Log10(odds / priorOdds), MidpointRounding.AwayFromZero);
    }

    /// <summary>Non-eliminated candidates left in the man's row.</summary>
    public int RowCandidates(int man) =>
        Enumerable.Range(0, Pairs).Count(woman => _states[man, woman] != PairState.Eliminated);

    /// <summary>
    /// Integer log-odds of the pair being a match: the row prior round(10*log10(1/(r-1))) plus the grid score.
    /// 0 is evens.
    /// </summary>
    public int AbsoluteLogOdds(int man, int woman)
    {
        return _states[man, woman] switch
        {
            PairState.Confirmed => int.MaxValue / 2,
            PairState.Eliminated => int.MinValue / 2,
            _ => RowPrior(RowCandidates(man)) + Scores[man, woman]
        };
    }

    public static int RowPrior(int candidates) =>
        candidates <= 1 ? int.MaxValue / 2 : (int)Math.Round(10 * Math.Log10(1d / (candidates - 1)), MidpointRounding.AwayFromZero);

    public void Confirm(int man, int woman)
    {
        if (_states[man, woman] == PairState.Confirmed)
        {
            return;
        }

        if (_states[man, woman] == PairState.Eliminated)
        {
            throw new InvalidOperationException($"Cannot confirm eliminated pair {man}-{woman}");
        }

        _states[man, woman] = PairState.Confirmed;
        for (var i = 0; i < Pairs; i++)
        {
            if (i != woman)
            {
                SetEliminated(man, i);
            }

            if (i != man)
            {
                SetEliminated(i, woman);
            }
        }

        Propagate();
    }

    public void Eliminate(int man, int woman)
    {
        if (_states[man, woman] == PairState.Confirmed)
        {
            throw new InvalidOperationException($"Cannot eliminate confirmed pair {man}-{woman}");
        }

        SetEliminated(man, woman);
        Propagate();
    }

    private void SetEliminated(int man, int woman)
    {
        if (_states[man, woman] == PairState.Confirmed)
        {
            throw new InvalidOperationException($"Cannot eliminate confirmed pair {man}-{woman}");
        }

        _states[man, woman] = PairState.Eliminated;
    }

    /// <summary>
    /// The one deduction a human makes without thinking: a row or column with a single possible partner left is confirmed.
    /// </summary>
    private void Propagate()
    {
        for (var i = 0; i < Pairs; i++)
        {
            var rowOpen = Enumerable.Range(0, Pairs).Where(w => _states[i, w] != PairState.Eliminated).ToList();
            if (rowOpen.Count == 1 && _states[i, rowOpen[0]] == PairState.Unknown)
            {
                Confirm(i, rowOpen[0]);
                return;
            }

            var columnOpen = Enumerable.Range(0, Pairs).Where(m => _states[m, i] != PairState.Eliminated).ToList();
            if (columnOpen.Count == 1 && _states[columnOpen[0], i] == PairState.Unknown)
            {
                Confirm(columnOpen[0], i);
                return;
            }
        }
    }
}
