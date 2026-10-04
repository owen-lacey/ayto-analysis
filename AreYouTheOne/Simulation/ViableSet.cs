using System.Collections.Concurrent;
using AreYouTheOne.Entities;

namespace AreYouTheOne.Simulation;

/// <summary>
/// The exact set of perfect matchings still consistent with every event so far.
/// Equivalent to enumerating <see cref="Solver"/>'s solutions, but filtered incrementally in memory so that
/// hundreds of 10-couple seasons can be measured quickly.
/// Used only to MEASURE information; human strategies never see it.
/// </summary>
public class ViableSet
{
    private static readonly ConcurrentDictionary<int, byte[]> PermutationCache = new();

    private readonly byte[] _perms;
    private int[] _indices;

    public ViableSet(int pairs)
    {
        Pairs = pairs;
        _perms = PermutationCache.GetOrAdd(pairs, AllPermutations);
        _indices = Enumerable.Range(0, _perms.Length / pairs).ToArray();
    }

    public int Pairs { get; }

    public int Count => _indices.Length;

    public void ApplyTruthBooth(TruthBooth truthBooth)
    {
        var a = truthBooth.A;
        var b = (byte)truthBooth.B;
        Filter(offset => (_perms[offset + a] == b) == truthBooth.Result);
    }

    public void ApplyMatchUp(MatchUp matchUp)
    {
        var lineup = matchUp.Matching.OrderBy(p => p.A).Select(p => (byte)p.B).ToArray();
        Filter(offset =>
        {
            var score = 0;
            for (var man = 0; man < Pairs; man++)
            {
                if (_perms[offset + man] == lineup[man])
                {
                    score++;
                }
            }

            return score == matchUp.Score;
        });
    }

    /// <summary>
    /// Fraction of viable matchings in which each (man, woman) pair is a perfect match.
    /// </summary>
    public double[,] MarginalProbabilities()
    {
        var counts = new long[Pairs, Pairs];
        foreach (var index in _indices)
        {
            var offset = index * Pairs;
            for (var man = 0; man < Pairs; man++)
            {
                counts[man, _perms[offset + man]]++;
            }
        }

        var marginals = new double[Pairs, Pairs];
        for (var man = 0; man < Pairs; man++)
        {
            for (var woman = 0; woman < Pairs; woman++)
            {
                marginals[man, woman] = (double)counts[man, woman] / Count;
            }
        }

        return marginals;
    }

    private void Filter(Func<int, bool> keep)
    {
        var kept = new List<int>(_indices.Length);
        foreach (var index in _indices)
        {
            if (keep(index * Pairs))
            {
                kept.Add(index);
            }
        }

        if (kept.Count == 0)
        {
            throw new InvalidOperationException("Event is inconsistent with every remaining matching");
        }

        _indices = kept.ToArray();
    }

    private static byte[] AllPermutations(int pairs)
    {
        var total = 1;
        for (var i = 2; i <= pairs; i++)
        {
            total *= i;
        }

        var result = new byte[total * pairs];
        var current = Enumerable.Range(0, pairs).Select(i => (byte)i).ToArray();
        for (var p = 0; p < total; p++)
        {
            Array.Copy(current, 0, result, p * pairs, pairs);
            NextPermutation(current);
        }

        return result;
    }

    private static void NextPermutation(byte[] values)
    {
        var i = values.Length - 2;
        while (i >= 0 && values[i] >= values[i + 1])
        {
            i--;
        }

        if (i < 0)
        {
            return;
        }

        var j = values.Length - 1;
        while (values[j] <= values[i])
        {
            j--;
        }

        (values[i], values[j]) = (values[j], values[i]);
        Array.Reverse(values, i + 1, values.Length - i - 1);
    }
}
