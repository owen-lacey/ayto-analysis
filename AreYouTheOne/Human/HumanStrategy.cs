using AreYouTheOne.Entities;
using AreYouTheOne.Simulation;

namespace AreYouTheOne.Human;

public enum Selection
{
    /// <summary>Prefer pairs whose absolute log-odds is closest to 0 (evens).</summary>
    ClosestToEvens,

    /// <summary>Prefer pairs with the highest absolute log-odds.</summary>
    HighestScore
}

public record HumanStrategyOptions
{
    public Selection Selection { get; init; } = Selection.ClosestToEvens;

    /// <summary>Switch to a greedy maximum-likelihood lineup once every open row has a clear favourite.</summary>
    public bool UseEndgame { get; init; } = true;

    /// <summary>Strategy B: build each match-up by swapping two pairs of the previous one.</summary>
    public bool UseSwaps { get; init; }

    /// <summary>
    /// Strategy C: test at most this many unknown pairs per match-up and pad the remaining slots with known pairs.
    /// Null tests as many unknown pairs as possible (Strategy A).
    /// </summary>
    public int? TestPairs { get; init; }
}

/// <summary>
/// Strategies A, B and C. Decisions read only the <see cref="PointsGrid"/>, the previous lineup and a queued truth booth.
/// </summary>
public class HumanStrategy(int pairs, HumanStrategyOptions options, Random random) : IStrategy
{
    /// <summary>Endgame trigger: every open row's favourite leads its runner-up by at least this many points.</summary>
    public const int EndgameMargin = 10;

    /// <summary>Swap cost for each of the four pairs that is already known (contributes no information).</summary>
    private const int KnownPairSwapCost = 30;

    private (int ManA, int WomanA, int ManB, int WomanB)? _pendingSwap;
    private MatchUp? _previousMatchUp;
    private Pair? _queuedTruthBooth;

    public PointsGrid Grid { get; } = new(pairs);

    public Pair NextTruthBooth()
    {
        if (_queuedTruthBooth is { } queued)
        {
            _queuedTruthBooth = null;
            if (Grid.IsUnknown(queued.A, queued.B))
            {
                return queued;
            }
        }

        var candidates = UnknownPairs(_ => true, _ => true);
        if (candidates.Count == 0)
        {
            // Everything is known on paper; spend the booth on any pair.
            return new Pair { A = 0, B = Enumerable.Range(0, pairs).First(w => Grid.State(0, w) == PairState.Confirmed) };
        }

        return candidates.MinBy(Key)!;
    }

    public Pair[] NextMatchUp()
    {
        _pendingSwap = null;
        if (options.UseEndgame && EndgameReached())
        {
            return MaximumLikelihoodLineup();
        }

        if (options.UseSwaps && _previousMatchUp != null && TryBestSwap(_previousMatchUp.Matching, out var swapped))
        {
            return swapped;
        }

        return FreshLineup();
    }

    public void Observe(TruthBooth truthBooth) => Grid.ApplyTruthBooth(truthBooth);

    public void Observe(MatchUp matchUp)
    {
        if (_pendingSwap is { } swap && _previousMatchUp != null)
        {
            var deduction = SwapDeduction.Deduce(swap.ManA, swap.WomanA, swap.ManB, swap.WomanB,
                matchUp.Score - _previousMatchUp.Score);
            foreach (var pair in deduction.Confirmed)
            {
                Grid.Confirm(pair.A, pair.B);
            }

            foreach (var pair in deduction.Eliminated.Where(p => Grid.State(p.A, p.B) != PairState.Eliminated))
            {
                Grid.Eliminate(pair.A, pair.B);
            }

            _queuedTruthBooth = deduction.NextTruthBooth;
        }

        Grid.ApplyMatchUp(matchUp);
        _previousMatchUp = matchUp;
        _pendingSwap = null;
    }

    public bool EndgameReached()
    {
        for (var man = 0; man < pairs; man++)
        {
            if (Enumerable.Range(0, pairs).Any(w => Grid.State(man, w) == PairState.Confirmed))
            {
                continue;
            }

            var scores = Enumerable.Range(0, pairs)
                .Where(w => Grid.IsUnknown(man, w))
                .Select(w => Grid.Scores[man, w])
                .OrderDescending()
                .ToList();
            if (scores.Count > 1 && scores[0] - scores[1] < EndgameMargin)
            {
                return false;
            }
        }

        return true;
    }

    private int Key(Pair pair)
    {
        var logOdds = Grid.AbsoluteLogOdds(pair.A, pair.B);
        return options.Selection == Selection.ClosestToEvens ? Math.Abs(logOdds) : -logOdds;
    }

    private Pair[] MaximumLikelihoodLineup()
    {
        var builder = new LineupBuilder(pairs, Grid, random);
        builder.PlaceConfirmed();
        builder.PlaceGreedy(int.MaxValue, pair => -Grid.Scores[pair.A, pair.B]);
        builder.PadWithKnownPairs();
        return builder.Complete();
    }

    private Pair[] FreshLineup()
    {
        var builder = new LineupBuilder(pairs, Grid, random);
        builder.PlaceConfirmed();
        builder.PlaceGreedy(options.TestPairs ?? int.MaxValue, Key, keepPaddable: options.TestPairs != null);
        builder.PadWithKnownPairs();
        // Strategy C fallback: slots that couldn't be padded with known pairs are filled as Strategy A would.
        builder.PlaceGreedy(int.MaxValue, Key);
        return builder.Complete();
    }

    private bool TryBestSwap(Pair[] previous, out Pair[] lineup)
    {
        lineup = [];
        var bestCost = int.MaxValue;
        (int, int, int, int)? best = null;
        var men = Enumerable.Range(0, pairs).ToArray();
        random.Shuffle(men);
        for (var i = 0; i < pairs; i++)
        {
            for (var j = i + 1; j < pairs; j++)
            {
                int manA = men[i], manB = men[j];
                int womanA = previous[manA].B, womanB = previous[manB].B;
                if (Grid.State(manA, womanA) == PairState.Confirmed || Grid.State(manB, womanB) == PairState.Confirmed)
                {
                    continue;
                }

                Pair[] four =
                [
                    new() { A = manA, B = womanA }, new() { A = manB, B = womanB },
                    new() { A = manA, B = womanB }, new() { A = manB, B = womanA }
                ];
                var unknown = four.Where(p => Grid.IsUnknown(p.A, p.B)).ToList();
                if (unknown.Count == 0)
                {
                    continue;
                }

                var cost = unknown.Sum(Key) + KnownPairSwapCost * (4 - unknown.Count);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = (manA, womanA, manB, womanB);
                }
            }
        }

        if (best is not { } swap)
        {
            return false;
        }

        lineup = previous.Select(p => new Pair { A = p.A, B = p.B }).ToArray();
        lineup[swap.Item1] = new Pair { A = swap.Item1, B = swap.Item4 };
        lineup[swap.Item3] = new Pair { A = swap.Item3, B = swap.Item2 };
        _pendingSwap = swap;
        return true;
    }

    private List<Pair> UnknownPairs(Func<int, bool> manFree, Func<int, bool> womanFree)
    {
        var result = new List<Pair>();
        for (var man = 0; man < pairs; man++)
        {
            for (var woman = 0; woman < pairs; woman++)
            {
                if (manFree(man) && womanFree(woman) && Grid.IsUnknown(man, woman))
                {
                    result.Add(new Pair { A = man, B = woman });
                }
            }
        }

        var shuffled = result.ToArray();
        random.Shuffle(shuffled);
        return shuffled.ToList();
    }
}
