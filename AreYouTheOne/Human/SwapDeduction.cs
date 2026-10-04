using AreYouTheOne.Entities;

namespace AreYouTheOne.Human;

/// <summary>
/// What a contestant can deduce when consecutive match-ups differ by exactly one swap, A-a, B-b → A-b, B-a.
/// Only those four pairs change, so delta = ([A-b] + [B-a]) - ([A-a] + [B-b]). If A-a were a match, A can't match b
/// and a can't match B, so both new pairs would be 0 (likewise for B-b). That makes every score change decisive:
/// <list type="bullet">
/// <item>-2: A-a and B-b are both matches.</item>
/// <item>-1: exactly one of A-a, B-b is a match, so A-b and B-a are out. Truth booth A-a next.</item>
/// <item> 0: none of the four pairs is a match.</item>
/// <item>+1: exactly one of A-b, B-a is a match, so A-a and B-b are out. Truth booth A-b next.</item>
/// <item>+2: A-b and B-a are both matches.</item>
/// </list>
/// </summary>
public record SwapDeduction(Pair[] Confirmed, Pair[] Eliminated, Pair? NextTruthBooth)
{
    public static SwapDeduction Deduce(int manA, int womanA, int manB, int womanB, int delta)
    {
        var aa = new Pair { A = manA, B = womanA };
        var bb = new Pair { A = manB, B = womanB };
        var ab = new Pair { A = manA, B = womanB };
        var ba = new Pair { A = manB, B = womanA };

        return delta switch
        {
            -2 => new SwapDeduction([aa, bb], [], null),
            -1 => new SwapDeduction([], [ab, ba], aa),
            0 => new SwapDeduction([], [aa, bb, ab, ba], null),
            1 => new SwapDeduction([], [aa, bb], ab),
            2 => new SwapDeduction([ab, ba], [], null),
            _ => throw new ArgumentOutOfRangeException(nameof(delta), delta, "A single swap changes the score by at most 2")
        };
    }
}
