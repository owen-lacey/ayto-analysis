using AreYouTheOne.Human;

namespace AreYouTheOne.Tests;

public class SwapDeductionTests
{
    private static IEnumerable<int[]> Permutations(int[] items)
    {
        if (items.Length <= 1)
        {
            yield return items;
            yield break;
        }

        for (var i = 0; i < items.Length; i++)
        {
            var rest = items.Where((_, j) => j != i).ToArray();
            foreach (var tail in Permutations(rest))
            {
                yield return [items[i], .. tail];
            }
        }
    }

    [Fact]
    public void Deductions_AreSoundForEveryHiddenMatchingAndSwap()
    {
        const int pairs = 5;
        var previous = Enumerable.Range(0, pairs).ToArray();
        var deltasSeen = new HashSet<int>();

        foreach (var hidden in Permutations(Enumerable.Range(0, pairs).ToArray()))
        {
            for (var manA = 0; manA < pairs; manA++)
            {
                for (var manB = manA + 1; manB < pairs; manB++)
                {
                    var next = (int[])previous.Clone();
                    (next[manA], next[manB]) = (next[manB], next[manA]);
                    var delta = next.Where((w, m) => hidden[m] == w).Count()
                                - previous.Where((w, m) => hidden[m] == w).Count();
                    deltasSeen.Add(delta);

                    var deduction = SwapDeduction.Deduce(manA, previous[manA], manB, previous[manB], delta);

                    Assert.All(deduction.Confirmed, p => Assert.Equal(p.B, hidden[p.A]));
                    Assert.All(deduction.Eliminated, p => Assert.NotEqual(p.B, hidden[p.A]));
                    // ±1: exactly one of two pairs is a match, and the queued booth tests one of them
                    if (delta == -1)
                    {
                        Assert.Equal((manA, previous[manA]), (deduction.NextTruthBooth!.A, deduction.NextTruthBooth.B));
                        Assert.True(hidden[manA] == previous[manA] ^ hidden[manB] == previous[manB]);
                    }
                    else if (delta == 1)
                    {
                        Assert.Equal((manA, previous[manB]), (deduction.NextTruthBooth!.A, deduction.NextTruthBooth.B));
                        Assert.True(hidden[manA] == previous[manB] ^ hidden[manB] == previous[manA]);
                    }
                }
            }
        }

        Assert.Equal([-2, -1, 0, 1, 2], deltasSeen.Order());
    }

    [Theory]
    [InlineData(-2, 2, 0)]
    [InlineData(-1, 0, 2)]
    [InlineData(0, 0, 4)]
    [InlineData(1, 0, 2)]
    [InlineData(2, 2, 0)]
    public void Deductions_HaveExpectedShape(int delta, int confirmed, int eliminated)
    {
        var deduction = SwapDeduction.Deduce(0, 0, 1, 1, delta);
        Assert.Equal(confirmed, deduction.Confirmed.Length);
        Assert.Equal(eliminated, deduction.Eliminated.Length);
        Assert.Equal(Math.Abs(delta) == 1, deduction.NextTruthBooth != null);
    }

    [Fact]
    public void MinusTwo_ConfirmsTheOriginalPairs_PlusTwo_ConfirmsTheSwappedPairs()
    {
        var minus = SwapDeduction.Deduce(0, 5, 1, 6, -2);
        Assert.Contains(minus.Confirmed, p => p.A == 0 && p.B == 5);
        Assert.Contains(minus.Confirmed, p => p.A == 1 && p.B == 6);

        var plus = SwapDeduction.Deduce(0, 5, 1, 6, 2);
        Assert.Contains(plus.Confirmed, p => p.A == 0 && p.B == 6);
        Assert.Contains(plus.Confirmed, p => p.A == 1 && p.B == 5);
    }
}
