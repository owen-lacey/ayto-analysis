using AreYouTheOne.Entities;
using AreYouTheOne.Human;

namespace AreYouTheOne.Tests;

public class PointsGridTests
{
    private static MatchUp Identity(int pairs, int score) => new()
    {
        Matching = Enumerable.Range(0, pairs).Select(i => new Pair { A = i, B = i }).ToArray(),
        Score = score
    };

    [Theory]
    [InlineData(1, 10, 0)]
    // 10*log10((2/8) * 9) = 3.52, so +4 (the brief's rule-of-thumb table said +3)
    [InlineData(2, 10, 4)]
    [InlineData(3, 10, 6)]
    [InlineData(4, 10, 8)]
    [InlineData(2, 5, 4)]
    [InlineData(1, 2, 0)]
    public void Increment_MatchesLogOddsFormula(int kPrime, int nPrime, int expected)
    {
        Assert.Equal(expected, PointsGrid.Increment(kPrime, nPrime));
    }

    [Fact]
    public void Blackout_AmongUnknownPairs_EliminatesThemAndLeavesConfirmedAlone()
    {
        var grid = new PointsGrid(10);
        grid.ApplyTruthBooth(new TruthBooth { A = 0, B = 0, Result = true });

        // Scores 1, but that beam is the confirmed couple: k' = 0
        grid.ApplyMatchUp(Identity(10, 1));

        Assert.Equal(PairState.Confirmed, grid.State(0, 0));
        for (var i = 1; i < 10; i++)
        {
            Assert.Equal(PairState.Eliminated, grid.State(i, i));
        }
    }

    [Fact]
    public void MatchUp_SubtractsConfirmedFromKAndSkipsKnownZerosInN()
    {
        var grid = new PointsGrid(10);
        grid.ApplyTruthBooth(new TruthBooth { A = 0, B = 0, Result = true });
        grid.ApplyTruthBooth(new TruthBooth { A = 1, B = 1, Result = true });
        grid.ApplyTruthBooth(new TruthBooth { A = 2, B = 2, Result = false });
        grid.ApplyTruthBooth(new TruthBooth { A = 3, B = 3, Result = false });
        grid.ApplyTruthBooth(new TruthBooth { A = 4, B = 4, Result = false });

        // k = 4 with 2 confirmed -> k' = 2; 10 - 2 confirmed - 3 known zeros -> n' = 5
        grid.ApplyMatchUp(Identity(10, 4));

        var expected = (int)Math.Round(10 * Math.Log10(2d / 3 * 4));
        Assert.Equal(4, expected);
        for (var i = 5; i < 10; i++)
        {
            Assert.Equal(expected, grid.Scores[i, i]);
        }

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(0, grid.Scores[i, i]);
        }
    }

    [Fact]
    public void MatchUp_WhereEveryUnknownPairMustScore_ConfirmsThem()
    {
        var grid = new PointsGrid(4);
        grid.ApplyTruthBooth(new TruthBooth { A = 0, B = 0, Result = false });
        grid.ApplyTruthBooth(new TruthBooth { A = 1, B = 1, Result = false });

        grid.ApplyMatchUp(Identity(4, 2));

        Assert.Equal(PairState.Confirmed, grid.State(2, 2));
        Assert.Equal(PairState.Confirmed, grid.State(3, 3));
    }

    [Fact]
    public void TruthBoothYes_EliminatesRowAndColumn()
    {
        var grid = new PointsGrid(10);
        grid.ApplyTruthBooth(new TruthBooth { A = 3, B = 7, Result = true });

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(i == 7 ? PairState.Confirmed : PairState.Eliminated, grid.State(3, i));
            Assert.Equal(i == 3 ? PairState.Confirmed : PairState.Eliminated, grid.State(i, 7));
        }
    }

    [Fact]
    public void AbsoluteLogOdds_UsesRowCandidatesPlusScore()
    {
        var grid = new PointsGrid(10);
        // r = 10: round(10 * log10(1/9)) = -10
        Assert.Equal(-10, grid.AbsoluteLogOdds(0, 0));

        grid.ApplyTruthBooth(new TruthBooth { A = 0, B = 9, Result = false });
        // r = 9: round(10 * log10(1/8)) = -9
        Assert.Equal(9, grid.RowCandidates(0));
        Assert.Equal(-9, grid.AbsoluteLogOdds(0, 0));

        grid.Scores[0, 0] += 4;
        Assert.Equal(-5, grid.AbsoluteLogOdds(0, 0));
        // Two candidates left is evens before any evidence
        Assert.Equal(0, PointsGrid.RowPrior(2));
    }

    [Fact]
    public void RowWithOneCandidateLeft_IsConfirmed()
    {
        var grid = new PointsGrid(3);
        grid.ApplyTruthBooth(new TruthBooth { A = 0, B = 1, Result = false });
        grid.ApplyTruthBooth(new TruthBooth { A = 0, B = 2, Result = false });

        Assert.Equal(PairState.Confirmed, grid.State(0, 0));
        Assert.Equal(PairState.Eliminated, grid.State(1, 0));
    }
}
