using AreYouTheOne.Entities;

namespace AreYouTheOne.Simulation;

public record SimulationRun
{
    /// <summary>
    /// Viable matchings remaining: index 0 before any event, then one entry per event in schedule order
    /// (week w's truth booth at 2w+1, its match-up at 2w+2). Once a single matching remains, later entries stay at 1.
    /// </summary>
    public required long[] RemainingCounts { get; init; }

    public required List<Event> Events { get; init; }

    /// <summary>The perfect matching was mathematically determined before the final match-up.</summary>
    public required bool Success { get; init; }

    public double MeanBitsPerEvent => InformationMetrics.MeanBitsPerEvent(RemainingCounts);
}

/// <summary>
/// Plays a fixed schedule of weeks, each a truth booth followed by a match-up, against a hidden matching.
/// </summary>
public static class SeasonSimulator
{
    public const int Weeks = 10;

    /// <param name="afterMatchUp">Optional measurement hook, called after each match-up has been observed.</param>
    public static SimulationRun Run(IStrategy strategy, int[] hiddenMatching, int weeks = Weeks,
        Action<ViableSet, MatchUp>? afterMatchUp = null)
    {
        var pairs = hiddenMatching.Length;
        var viable = new ViableSet(pairs);
        var counts = new long[2 * weeks + 1];
        counts[0] = viable.Count;
        var events = new List<Event>();
        var eventIndex = 0;

        for (var week = 0; week < weeks && viable.Count > 1; week++)
        {
            var tbPair = strategy.NextTruthBooth();
            var truthBooth = new TruthBooth { A = tbPair.A, B = tbPair.B, Result = hiddenMatching[tbPair.A] == tbPair.B };
            viable.ApplyTruthBooth(truthBooth);
            strategy.Observe(truthBooth);
            events.Add(new Event { TruthBooths = [truthBooth] });
            counts[++eventIndex] = viable.Count;

            if (viable.Count == 1)
            {
                break;
            }

            var lineup = strategy.NextMatchUp();
            Validate(lineup, pairs);
            var matchUp = new MatchUp
            {
                Matching = lineup,
                Score = lineup.Count(p => hiddenMatching[p.A] == p.B)
            };
            viable.ApplyMatchUp(matchUp);
            strategy.Observe(matchUp);
            events.Add(new Event { MatchUp = matchUp });
            counts[++eventIndex] = viable.Count;
            afterMatchUp?.Invoke(viable, matchUp);
        }

        for (var i = eventIndex + 1; i < counts.Length; i++)
        {
            counts[i] = 1;
        }

        return new SimulationRun
        {
            RemainingCounts = counts,
            Events = events,
            // after the final week's truth booth, i.e. before its match-up
            Success = counts[2 * weeks - 1] == 1
        };
    }

    public static int[] HiddenMatching(int pairs, int seed)
    {
        var random = new Random(seed);
        var matching = Enumerable.Range(0, pairs).ToArray();
        random.Shuffle(matching);
        return matching;
    }

    private static void Validate(Pair[] lineup, int pairs)
    {
        if (lineup.Length != pairs
            || lineup.Select((p, i) => p.A == i).Any(ok => !ok)
            || lineup.Select(p => p.B).Distinct().Count() != pairs
            || lineup.Any(p => p.B < 0 || p.B >= pairs))
        {
            throw new InvalidOperationException($"Match-up is not a perfect matching: {string.Join(", ", lineup.Select(p => $"{p.A}-{p.B}"))}");
        }
    }
}
