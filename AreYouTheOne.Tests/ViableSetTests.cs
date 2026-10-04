using AreYouTheOne.Entities;
using AreYouTheOne.Simulation;
using Microsoft.Extensions.Logging.Abstractions;

namespace AreYouTheOne.Tests;

public class ViableSetTests
{
    [Fact]
    public void StartsWithEveryPerfectMatching()
    {
        Assert.Equal(3_628_800, new ViableSet(10).Count);
    }

    [Fact]
    public void AgreesWithSolverCounts()
    {
        const int pairs = 6;
        var seasonData = new SeasonData(pairs)
        {
            Contestants = new Contestants
            {
                Men = Enumerable.Range(0, pairs).Select(i => new Contestant { Id = i, Name = "Geoff" }).ToArray(),
                Women = Enumerable.Range(0, pairs).Select(i => new Contestant { Id = i, Name = "Sally" }).ToArray()
            }
        };
        var events = new List<Event>
        {
            new() { TruthBooths = [new TruthBooth { A = 0, B = 3, Result = false }] },
            new() { MatchUp = new MatchUp { Matching = Lineup(0, 1, 2, 3, 4, 5), Score = 2 } },
            new() { TruthBooths = [new TruthBooth { A = 1, B = 1, Result = false }] },
            new() { MatchUp = new MatchUp { Matching = Lineup(1, 0, 3, 2, 5, 4), Score = 1 } }
        };

        var viable = new ViableSet(pairs);
        foreach (var @event in events)
        {
            seasonData.Events.Add(@event);
            foreach (var tb in @event.TruthBooths) viable.ApplyTruthBooth(tb);
            if (@event.MatchUp != null) viable.ApplyMatchUp(@event.MatchUp);

            var solver = new Solver(seasonData, NullLogger.Instance, seasonData.Events);
            var expected = solver.SolveAllConstraints(solver.BuildConstraints(seasonData)).Count;
            Assert.Equal(expected, viable.Count);
        }
    }

    [Fact]
    public void MarginalsSumToOnePerRow()
    {
        var viable = new ViableSet(5);
        viable.ApplyMatchUp(new MatchUp { Matching = Lineup(0, 1, 2, 3, 4), Score = 1 });
        var marginals = viable.MarginalProbabilities();
        for (var man = 0; man < 5; man++)
        {
            Assert.Equal(1d, Enumerable.Range(0, 5).Sum(w => marginals[man, w]), 9);
        }
    }

    [Fact]
    public void MeanBitsPerEvent_IgnoresEventsAfterTheMatchingIsDetermined()
    {
        Assert.Equal(1.5, InformationMetrics.MeanBitsPerEvent([8, 4, 1, 1]));
    }

    private static Pair[] Lineup(params int[] women) => women.Select((w, m) => new Pair { A = m, B = w }).ToArray();
}
