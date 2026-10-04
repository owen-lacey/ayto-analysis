using AreYouTheOne.Entities;
using AreYouTheOne.Human;
using AreYouTheOne.Simulation;

namespace AreYouTheOne.Tests;

public class StrategyTests
{
    public static TheoryData<string> Configurations => new() { "A", "A-noEndgame", "A-highest", "B", "C3", "BC3" };

    private static HumanStrategyOptions Options(string name) => name switch
    {
        "A" => new HumanStrategyOptions(),
        "A-noEndgame" => new HumanStrategyOptions { UseEndgame = false },
        "A-highest" => new HumanStrategyOptions { Selection = Selection.HighestScore },
        "B" => new HumanStrategyOptions { UseSwaps = true },
        "C3" => new HumanStrategyOptions { TestPairs = 3 },
        "BC3" => new HumanStrategyOptions { UseSwaps = true, TestPairs = 3 },
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    /// <summary>
    /// Every confirmation and elimination the grid makes (truth booths, blackouts, swap deductions, singles) must
    /// agree with the hidden matching.
    /// </summary>
    [Theory]
    [MemberData(nameof(Configurations))]
    public void GridDeductions_NeverContradictTheHiddenMatching(string configuration)
    {
        for (var seed = 0; seed < 40; seed++)
        {
            var hidden = SeasonSimulator.HiddenMatching(8, seed);
            var strategy = new HumanStrategy(8, Options(configuration), new Random(seed));
            SeasonSimulator.Run(strategy, hidden);

            for (var man = 0; man < 8; man++)
            {
                for (var woman = 0; woman < 8; woman++)
                {
                    var state = strategy.Grid.State(man, woman);
                    if (state == PairState.Confirmed) Assert.Equal(woman, hidden[man]);
                    if (state == PairState.Eliminated) Assert.NotEqual(woman, hidden[man]);
                }
            }
        }
    }

    [Fact]
    public void Swaps_ChangeExactlyTwoPairsBetweenConsecutiveMatchUps()
    {
        var hidden = SeasonSimulator.HiddenMatching(10, 7);
        var run = SeasonSimulator.Run(new HumanStrategy(10, Options("B"), new Random(7)), hidden);
        var matchUps = run.Events.Where(e => e.MatchUp != null).Select(e => e.MatchUp!).ToList();

        Assert.True(matchUps.Count > 2);
        var swaps = matchUps.Zip(matchUps.Skip(1))
            .Count(p => p.First.Matching.Zip(p.Second.Matching).Count(x => x.First.B != x.Second.B) == 2);
        Assert.True(swaps >= matchUps.Count / 2, $"Only {swaps} of {matchUps.Count - 1} transitions were single swaps");
    }

    [Fact]
    public void Padding_TestsTPairsAndFillsTheRestWithKnownZeros()
    {
        var grid = new PointsGrid(4);
        foreach (var (a, b) in new[] { (0, 1), (1, 0), (2, 3), (3, 2) })
        {
            grid.ApplyTruthBooth(new TruthBooth { A = a, B = b, Result = false });
        }

        var builder = new LineupBuilder(4, grid, new Random(1));
        builder.PlaceConfirmed();
        builder.PlaceGreedy(0, _ => 0);
        builder.PadWithKnownPairs();
        var lineup = builder.Complete();

        Assert.Equal(4, lineup.Select(p => p.B).Distinct().Count());
        Assert.All(lineup, p => Assert.Equal(PairState.Eliminated, grid.State(p.A, p.B)));
    }

    [Fact]
    public void Padding_FallsBackToTestPairsWhenNoKnownZerosFit()
    {
        var grid = new PointsGrid(4);
        var strategy = new HumanStrategy(4, new HumanStrategyOptions { TestPairs = 1, UseEndgame = false }, new Random(1));

        var lineup = strategy.NextMatchUp();

        Assert.Equal(4, lineup.Select(p => p.B).Distinct().Count());
        Assert.All(lineup, p => Assert.Equal(PairState.Unknown, grid.State(p.A, p.B)));
    }

    [Fact]
    public void Padding_WithPartialKnownZeros_UsesThemAndTestsTheRest()
    {
        var strategy = new HumanStrategy(6, new HumanStrategyOptions { TestPairs = 2, UseEndgame = false }, new Random(3));
        // Blackout on the identity lineup: 6 known zeros on the diagonal
        strategy.Observe(new MatchUp { Matching = Enumerable.Range(0, 6).Select(i => new Pair { A = i, B = i }).ToArray(), Score = 0 });

        var lineup = strategy.NextMatchUp();

        Assert.Equal(2, lineup.Count(p => strategy.Grid.IsUnknown(p.A, p.B)));
        Assert.Equal(4, lineup.Count(p => strategy.Grid.State(p.A, p.B) == PairState.Eliminated));
    }

    [Fact]
    public void SameSeed_GivesIdenticalRuns()
    {
        var hidden = SeasonSimulator.HiddenMatching(10, 11);
        var first = SeasonSimulator.Run(new HumanStrategy(10, Options("BC3"), new Random(11)), hidden);
        var second = SeasonSimulator.Run(new HumanStrategy(10, Options("BC3"), new Random(11)), hidden);

        Assert.Equal(first.RemainingCounts, second.RemainingCounts);
    }
}
