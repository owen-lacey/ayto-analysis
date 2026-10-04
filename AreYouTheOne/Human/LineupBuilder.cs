using AreYouTheOne.Entities;

namespace AreYouTheOne.Human;

/// <summary>
/// Builds a match-up one slot at a time, the way you would with the grid in front of you.
/// </summary>
public class LineupBuilder(int pairs, PointsGrid grid, Random random)
{
    private readonly int?[] _womanForMan = new int?[pairs];
    private readonly bool[] _womanUsed = new bool[pairs];

    public int Placed => _womanForMan.Count(w => w != null);

    public void PlaceConfirmed()
    {
        for (var man = 0; man < pairs; man++)
        {
            for (var woman = 0; woman < pairs; woman++)
            {
                if (grid.State(man, woman) == PairState.Confirmed)
                {
                    Place(man, woman);
                }
            }
        }
    }

    /// <summary>
    /// Repeatedly place the unknown pair with the lowest key among the free rows and columns, up to <paramref name="limit"/> pairs.
    /// With <paramref name="keepPaddable"/>, prefer pairs that still let every slot outside the <paramref name="limit"/>
    /// test pairs be padded with a known zero.
    /// </summary>
    public void PlaceGreedy(int limit, Func<Pair, int> key, bool keepPaddable = false)
    {
        for (var placed = 0; placed < limit; placed++)
        {
            var candidates = new List<Pair>();
            for (var man = 0; man < pairs; man++)
            {
                for (var woman = 0; woman < pairs; woman++)
                {
                    if (_womanForMan[man] == null && !_womanUsed[woman] && grid.IsUnknown(man, woman))
                    {
                        candidates.Add(new Pair { A = man, B = woman });
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return;
            }

            var shuffled = candidates.ToArray();
            random.Shuffle(shuffled);
            var best = shuffled.MinBy(key)!;
            if (keepPaddable)
            {
                var testsLeftAfter = limit - placed - 1;
                best = shuffled.OrderBy(key).FirstOrDefault(c => PaddableAfter(c) >= _womanForMan.Count(w => w == null) - 1 - testsLeftAfter) ?? best;
            }

            Place(best.A, best.B);
        }
    }

    /// <summary>How many free slots could be padded with known zeros if <paramref name="pair"/> were placed.</summary>
    private int PaddableAfter(Pair pair)
    {
        var freeMen = Enumerable.Range(0, pairs).Where(m => _womanForMan[m] == null && m != pair.A).ToArray();
        var freeWomen = Enumerable.Range(0, pairs).Where(w => !_womanUsed[w] && w != pair.B).ToArray();
        var manForWoman = new Dictionary<int, int>();
        return freeMen.Count(man => TryAugment(man, freeWomen, manForWoman, []));
    }

    /// <summary>
    /// Fill as many free slots as possible with known zeros (eliminated pairs), via augmenting paths so that one bad
    /// early choice doesn't block a full padding.
    /// </summary>
    public void PadWithKnownPairs()
    {
        var freeMen = Enumerable.Range(0, pairs).Where(m => _womanForMan[m] == null).ToArray();
        var freeWomen = Enumerable.Range(0, pairs).Where(w => !_womanUsed[w]).ToArray();
        random.Shuffle(freeMen);
        random.Shuffle(freeWomen);

        var manForWoman = new Dictionary<int, int>();
        foreach (var man in freeMen)
        {
            TryAugment(man, freeWomen, manForWoman, []);
        }

        foreach (var (woman, man) in manForWoman)
        {
            Place(man, woman);
        }
    }

    /// <summary>Fill whatever is still free arbitrarily and return the lineup indexed by man.</summary>
    public Pair[] Complete()
    {
        var freeWomen = Enumerable.Range(0, pairs).Where(w => !_womanUsed[w]).ToArray();
        random.Shuffle(freeWomen);
        var next = 0;
        for (var man = 0; man < pairs; man++)
        {
            if (_womanForMan[man] == null)
            {
                Place(man, freeWomen[next++]);
            }
        }

        return _womanForMan.Select((woman, man) => new Pair { A = man, B = woman!.Value }).ToArray();
    }

    private bool TryAugment(int man, int[] women, Dictionary<int, int> manForWoman, HashSet<int> visited)
    {
        foreach (var woman in women)
        {
            if (grid.State(man, woman) != PairState.Eliminated || !visited.Add(woman))
            {
                continue;
            }

            if (!manForWoman.TryGetValue(woman, out var other) || TryAugment(other, women, manForWoman, visited))
            {
                manForWoman[woman] = man;
                return true;
            }
        }

        return false;
    }

    private void Place(int man, int woman)
    {
        _womanForMan[man] = woman;
        _womanUsed[woman] = true;
    }
}
