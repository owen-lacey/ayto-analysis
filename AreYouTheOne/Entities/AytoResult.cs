using MathNet.Numerics;

namespace AreYouTheOne.Entities;

public record AytoResult
{
    public List<Event> Events { get; } = [];

    public AytoResult(List<Event> events)
    {
        Events = events;
    }

    public AytoResult(int pairs, TruthBooth truthBooth)
    {
        int remainingMatches;
        if (truthBooth.Result)
        {
            remainingMatches = (int)SpecialFunctions.Factorial(pairs - 1);
        }
        else
        {
            remainingMatches = (int)(SpecialFunctions.Factorial(pairs) - (int)SpecialFunctions.Factorial(pairs - 1));
        }

        RemainingMatches = Enumerable.Range(0, remainingMatches).Select(_ => new List<int>()).ToList();
    }
    
    public List<List<int>> RemainingMatches { get; init; } = [];

    public int Count => RemainingMatches.Count;
}