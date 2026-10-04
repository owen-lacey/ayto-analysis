using System.Globalization;
using System.Text;
using AreYouTheOne.Entities;
using AreYouTheOne.Human;
using AreYouTheOne.Simulation;

namespace AreYouTheOne.Console.Experiments;

/// <summary>
/// Paired comparison of the human-computable strategies: every strategy faces the same hidden matchings and the same
/// tie-break seeds. The viable set is used only to measure.
/// </summary>
public static class HumanStrategiesExperiment
{
    private const int Pairs = 10;

    public record Configuration(string Name, string Group, HumanStrategyOptions? Options)
    {
        public IStrategy Create(Random random) =>
            Options == null ? new RandomStrategy(Pairs, random) : new HumanStrategy(Pairs, Options, random);
    }

    public static readonly Configuration[] Configurations =
    [
        new("Random (harness control)", "control", null),
        new("A", "core", new HumanStrategyOptions()),
        new("A without endgame", "core", new HumanStrategyOptions { UseEndgame = false }),
        new("A, highest score", "core", new HumanStrategyOptions { Selection = Selection.HighestScore }),
        new("A + B", "core", new HumanStrategyOptions { UseSwaps = true }),
        new("A + C (T=3)", "padding", new HumanStrategyOptions { TestPairs = 3 }),
        new("A + C (T=5)", "padding", new HumanStrategyOptions { TestPairs = 5 }),
        new("A + C (T=7)", "padding", new HumanStrategyOptions { TestPairs = 7 }),
        new("A + C (T=10)", "padding", new HumanStrategyOptions { TestPairs = 10 }),
        new("A + B + C (T=3)", "combined", new HumanStrategyOptions { UseSwaps = true, TestPairs = 3 }),
        new("A + B + C (T=5)", "combined", new HumanStrategyOptions { UseSwaps = true, TestPairs = 5 }),
        new("A + B + C (T=7)", "combined", new HumanStrategyOptions { UseSwaps = true, TestPairs = 7 })
    ];

    public static void Run(int runs, int baseSeed, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var results = new SimulationRun[Configurations.Length, runs];
        var diagnostics = Configurations.Select(_ => new GridDiagnostics()).ToArray();
        var completed = 0;

        Parallel.For(0, runs, run =>
        {
            var seed = baseSeed + run;
            var hidden = SeasonSimulator.HiddenMatching(Pairs, seed);
            for (var c = 0; c < Configurations.Length; c++)
            {
                var strategy = Configurations[c].Create(new Random(seed ^ 0x5bd1e995));
                var local = new GridDiagnostics();
                results[c, run] = SeasonSimulator.Run(strategy, hidden,
                    afterMatchUp: strategy is HumanStrategy human ? (viable, _) => local.Record(human.Grid, viable) : null);
                diagnostics[c].Merge(local);
            }

            var done = Interlocked.Increment(ref completed);
            if (done % 20 == 0)
            {
                System.Console.Error.WriteLine($"{done}/{runs} seasons simulated");
            }
        });

        var summaries = Configurations.Select((config, c) =>
            new Summary(config, Enumerable.Range(0, runs).Select(r => results[c, r]).ToList())).ToList();

        File.WriteAllText(Path.Combine(outputDirectory, "human_strategies.md"),
            Report(summaries, diagnostics, runs, baseSeed));
        File.WriteAllText(Path.Combine(outputDirectory, "log2_remaining.csv"), Csv(summaries, baseSeed));
        File.WriteAllText(Path.Combine(outputDirectory, "log2_remaining.svg"), Log2Chart.Render(
        [
            ("Core strategies", summaries.Where(s => s.Configuration.Group is "control" or "core" || s.Configuration.Name == BestOf(summaries, "combined")).ToList()),
            ("Padding sweep (A + C)", summaries.Where(s => s.Configuration.Group == "padding").ToList())
        ], SeasonSimulator.Weeks));

        System.Console.WriteLine(Report(summaries, diagnostics, runs, baseSeed));
    }

    private static string BestOf(List<Summary> summaries, string group) =>
        summaries.Where(s => s.Configuration.Group == group).MaxBy(s => s.MeanBits)!.Configuration.Name;

    public class Summary(Configuration configuration, List<SimulationRun> runs)
    {
        public Configuration Configuration { get; } = configuration;
        public List<SimulationRun> Runs { get; } = runs;
        public double MeanBits => Runs.Average(r => r.MeanBitsPerEvent);
        public double StdBits => Math.Sqrt(Runs.Sum(r => Math.Pow(r.MeanBitsPerEvent - MeanBits, 2)) / (Runs.Count - 1));
        public double SuccessRate => Runs.Count(r => r.Success) / (double)Runs.Count;

        public double[] MeanLog2ByEvent => Enumerable.Range(0, Runs[0].RemainingCounts.Length)
            .Select(e => Runs.Average(r => Math.Log2(r.RemainingCounts[e])))
            .ToArray();

        /// <summary>Event index (1-based, in schedule order) at which the matching became determined; null if never.</summary>
        public IEnumerable<int?> DeterminedAt => Runs.Select(r =>
        {
            var index = Array.FindIndex(r.RemainingCounts, c => c == 1);
            return index < 0 ? (int?)null : index;
        });
    }

    private static string Report(List<Summary> summaries, GridDiagnostics[] diagnostics, int runs, int baseSeed)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("# Human-computable strategies: results");
        sb.AppendLine();
        sb.AppendLine($"10 couples, 10 weeks of (truth booth, match-up), {runs} paired seasons (seeds {baseSeed}–{baseSeed + runs - 1}).");
        sb.AppendLine("Success = the perfect matching is determined before the final match-up.");
        sb.AppendLine("Generated by `dotnet run --project AreYouTheOne.Console -c Release -- human`.");
        sb.AppendLine();
        sb.AppendLine("| Method             | Per Event | Success Rate |");
        sb.AppendLine("|--------------------|-----------|--------------|");
        sb.AppendLine("| Random *(quoted)*  | 1.23 bits | 74%          |");
        sb.AppendLine("| Actual *(quoted)*  | 1.39 bits | 71%          |");
        sb.AppendLine("| Information Theory *(quoted)* | 1.59 bits | 98% |");
        foreach (var s in summaries)
        {
            sb.AppendLine(string.Format(inv, "| {0} | {1:F2} ± {2:F2} bits | {3:P0} |",
                s.Configuration.Name, s.MeanBits, s.StdBits, s.SuccessRate).Replace(" %", "%"));
        }

        sb.AppendLine();
        sb.AppendLine("Quoted rows are the figures supplied with the brief; they were not produced by this harness, so they carry no ± and are not paired with the simulated rows.");
        sb.AppendLine();

        sb.AppendLine("## When the matching became determined");
        sb.AppendLine();
        sb.AppendLine("Event 19 is the week-10 truth booth (the last chance for success); 20 is the final match-up.");
        sb.AppendLine();
        sb.AppendLine("| Method | Median event | Determined by event 17 (week 9 TB) | Never determined | Repeated lineups per season | Pairs changed between match-ups |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var s in summaries)
        {
            var at = s.DeterminedAt.ToList();
            var sorted = at.Select(a => a ?? int.MaxValue).Order().ToList();
            var median = sorted[sorted.Count / 2];
            sb.AppendLine(string.Format(inv, "| {0} | {1} | {2:P0} | {3:P0} | {4:F2} | {5:F1} |", s.Configuration.Name,
                median == int.MaxValue ? "—" : median.ToString(inv),
                at.Count(a => a <= 17) / (double)at.Count, at.Count(a => a == null) / (double)at.Count,
                s.Runs.Average(LineupChurn.RepeatedLineups), s.Runs.SelectMany(LineupChurn.PairsChanged).DefaultIfEmpty().Average()).Replace(" %", "%"));
        }

        sb.AppendLine();
        sb.AppendLine("## Paired differences against A");
        sb.AppendLine();
        sb.AppendLine("Mean per-season difference in bits per event (same hidden matching, same tie-break seed), with a 95% interval.");
        sb.AppendLine();
        sb.AppendLine("| Method | Δ bits/event vs A | Seasons better / worse |");
        sb.AppendLine("|---|---|---|");
        var baseline = summaries.Single(s => s.Configuration.Name == "A");
        foreach (var s in summaries.Where(s => s != baseline))
        {
            var diffs = s.Runs.Zip(baseline.Runs, (x, y) => x.MeanBitsPerEvent - y.MeanBitsPerEvent).ToList();
            var mean = diffs.Average();
            var se = Math.Sqrt(diffs.Sum(d => Math.Pow(d - mean, 2)) / (diffs.Count - 1) / diffs.Count);
            sb.AppendLine(string.Format(inv, "| {0} | {1:+0.000;-0.000} ± {2:0.000} | {3} / {4} |", s.Configuration.Name, mean, 1.96 * se,
                diffs.Count(d => d > 1e-9), diffs.Count(d => d < -1e-9)));
        }

        sb.AppendLine();
        sb.AppendLine("## Where the naive Bayes grid misleads");
        sb.AppendLine();
        sb.AppendLine("Measured after every match-up, over pairs the grid still marks unknown. *Implied* is the grid's own probability, " +
                      "1/(1+10^(−log-odds/10)); *actual* is the pair's share of the remaining viable matchings.");
        sb.AppendLine();
        sb.AppendLine("| Method | Unknown pairs the exact model has already ruled out | Unknown pairs already certain | Row favourites (≥ evens) that are impossible |");
        sb.AppendLine("|---|---|---|---|");
        for (var c = 0; c < summaries.Count; c++)
        {
            var d = diagnostics[c];
            if (d.UnknownPairs == 0)
            {
                continue;
            }

            sb.AppendLine(string.Format(inv, "| {0} | {1:P0} | {2:P1} | {3:P0} of {4} |", summaries[c].Configuration.Name,
                d.ImpossibleUnknown / (double)d.UnknownPairs, d.CertainUnknown / (double)d.UnknownPairs,
                d.ImpossibleFavourites / (double)Math.Max(1, d.Favourites), d.Favourites).Replace(" %", "%"));
        }

        sb.AppendLine();
        sb.AppendLine("### Calibration of strategy A's grid, by how many match-ups the pair has appeared in");
        sb.AppendLine();
        sb.AppendLine("If the grid were calibrated, *actual* would equal *implied* in every row. Double counting shows up as the gap widening with appearances.");
        sb.AppendLine();
        sb.AppendLine("| Implied | Appearances | Pairs | Mean implied | Mean actual |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var row in diagnostics[summaries.IndexOf(baseline)].CalibrationRows())
        {
            sb.AppendLine(string.Format(inv, "| {0} | {1} | {2} | {3:P0} | {4:P0} |", row.Bin, row.Appearances, row.Count, row.Implied, row.Actual).Replace(" %", "%"));
        }

        return sb.ToString();
    }

    private static class LineupChurn
    {
        private static List<MatchUp> MatchUps(SimulationRun run) =>
            run.Events.Where(e => e.MatchUp != null).Select(e => e.MatchUp!).ToList();

        /// <summary>Match-ups identical to an earlier one: zero information by construction.</summary>
        public static double RepeatedLineups(SimulationRun run)
        {
            var seen = new HashSet<string>();
            return MatchUps(run).Count(m => !seen.Add(string.Join(",", m.Matching.Select(p => p.B))));
        }

        public static IEnumerable<double> PairsChanged(SimulationRun run)
        {
            var matchUps = MatchUps(run);
            return matchUps.Zip(matchUps.Skip(1), (a, b) => (double)a.Matching.Zip(b.Matching).Count(x => x.First.B != x.Second.B));
        }
    }

    private static string Csv(List<Summary> summaries, int baseSeed)
    {
        var sb = new StringBuilder();
        var events = summaries[0].Runs[0].RemainingCounts.Length;
        sb.AppendLine("strategy,seed,success,bits_per_event," + string.Join(",", Enumerable.Range(0, events).Select(e => $"log2_remaining_{e}")));
        foreach (var s in summaries)
        {
            for (var run = 0; run < s.Runs.Count; run++)
            {
                var r = s.Runs[run];
                sb.Append(CultureInfo.InvariantCulture, $"\"{s.Configuration.Name}\",{baseSeed + run},{(r.Success ? 1 : 0)},{r.MeanBitsPerEvent:F4},");
                sb.AppendLine(string.Join(",", r.RemainingCounts.Select(c => Math.Log2(c).ToString("F4", CultureInfo.InvariantCulture))));
            }
        }

        return sb.ToString();
    }

    /// <summary>Measurement only: compares the grid's beliefs with the exact viable set.</summary>
    private class GridDiagnostics
    {
        private static readonly double[] BinEdges = [0, 0.1, 0.25, 0.5, 0.75, 1.0001];
        private readonly Dictionary<(int Bin, int Appearances), (int Count, double Implied, double Actual)> _calibration = new();

        public long UnknownPairs { get; private set; }
        public long ImpossibleUnknown { get; private set; }
        public long CertainUnknown { get; private set; }
        public long Favourites { get; private set; }
        public long ImpossibleFavourites { get; private set; }

        public void Record(PointsGrid grid, ViableSet viable)
        {
            var marginals = viable.MarginalProbabilities();
            for (var man = 0; man < grid.Pairs; man++)
            {
                var unknown = Enumerable.Range(0, grid.Pairs).Where(w => grid.IsUnknown(man, w)).ToList();
                foreach (var woman in unknown)
                {
                    var actual = marginals[man, woman];
                    var implied = 1 / (1 + Math.Pow(10, -grid.AbsoluteLogOdds(man, woman) / 10d));
                    UnknownPairs++;
                    if (actual == 0) ImpossibleUnknown++;
                    if (actual == 1) CertainUnknown++;

                    var appearances = Math.Min(3, grid.History.Count(m => m.Matching[man].B == woman));
                    var bin = Array.FindLastIndex(BinEdges, e => implied >= e);
                    _calibration.TryGetValue((bin, appearances), out var cell);
                    _calibration[(bin, appearances)] = (cell.Count + 1, cell.Implied + implied, cell.Actual + actual);
                }

                if (unknown.Count > 1)
                {
                    var ordered = unknown.OrderByDescending(w => grid.Scores[man, w]).ToList();
                    if (grid.Scores[man, ordered[0]] > grid.Scores[man, ordered[1]] && grid.AbsoluteLogOdds(man, ordered[0]) >= 0)
                    {
                        Favourites++;
                        if (marginals[man, ordered[0]] == 0) ImpossibleFavourites++;
                    }
                }
            }
        }

        public void Merge(GridDiagnostics other)
        {
            lock (this)
            {
                UnknownPairs += other.UnknownPairs;
                ImpossibleUnknown += other.ImpossibleUnknown;
                CertainUnknown += other.CertainUnknown;
                Favourites += other.Favourites;
                ImpossibleFavourites += other.ImpossibleFavourites;
                foreach (var (key, value) in other._calibration)
                {
                    _calibration.TryGetValue(key, out var cell);
                    _calibration[key] = (cell.Count + value.Count, cell.Implied + value.Implied, cell.Actual + value.Actual);
                }
            }
        }

        public IEnumerable<(string Bin, string Appearances, int Count, double Implied, double Actual)> CalibrationRows() =>
            _calibration
                .Where(kv => kv.Key.Appearances > 0)
                .OrderBy(kv => kv.Key.Bin).ThenBy(kv => kv.Key.Appearances)
                .Select(kv => (
                    $"{BinEdges[kv.Key.Bin]:P0}–{Math.Min(1, BinEdges[kv.Key.Bin + 1]):P0}".Replace(" %", "%"),
                    kv.Key.Appearances == 3 ? "3+" : kv.Key.Appearances.ToString(CultureInfo.InvariantCulture),
                    kv.Value.Count,
                    kv.Value.Implied / kv.Value.Count,
                    kv.Value.Actual / kv.Value.Count));
    }
}
