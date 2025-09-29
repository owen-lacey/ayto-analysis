using System.Runtime.ExceptionServices;
using AreYouTheOne.Console.Helpers;
using AreYouTheOne.Entities;
using Microsoft.Extensions.Logging;

namespace AreYouTheOne.Console.Data;

public class ManualInformationTheory(ILogger logger)
{
    private readonly ILogger _logger = logger;

    private const int Pairs = 10;
    private static readonly Random Rnd = new();

    public List<AytoResult> Generate()
    {
        var matchCounts = new List<AytoResult>();
        var seasonData = new SeasonData(Pairs)
        {
            Contestants = new Contestants
            {
                Men = Enumerable.Range(0, Pairs).Select(i => new Contestant { Id = i, Name = "Geoff" }).ToArray(),
                Women = Enumerable.Range(0, Pairs).Select(i => new Contestant { Id = i, Name = "Sally" }).ToArray()
            }
        };

        // List of indexes where the ith element is the correct answer for male i
        var correctAnswer = Rnd.ListOfNumbers(Pairs);

        // Randomly select the first TB and MU because we don't know anything yet
        var initialTb = GetInitialTb(Pairs, correctAnswer);
        seasonData.AddEvent(new Event { TruthBooths = [initialTb] });
        var matchCount = new AytoResult(Pairs, initialTb);
        matchCounts.Add(matchCount);
        _logger.LogWarning("Doing initial random MU");
        GetRandomMu(correctAnswer, seasonData);
        matchCount = GetNextMatchCount(seasonData)!;

        while (matchCount.Count > 1)
        {
            _logger.LogWarning(
                $"Computing next match count. Remaining matches: {matchCount.Count}. Events: {seasonData.Events.Count}");
            AddNextTb(matchCount, correctAnswer, seasonData);
            var nextMatchCount = GetNextMatchCount(seasonData)!;
            if (nextMatchCount.Count == matchCount.Count && nextMatchCount.Count > 1)
            {
            }

            matchCount = nextMatchCount;
            matchCounts.Add(matchCount);

            _logger.LogWarning(
                $"Computing next match count. Remaining matches: {matchCount.Count}. Events: {seasonData.Events.Count}");
            AddNextMu(matchCount, correctAnswer, seasonData);
            nextMatchCount = GetNextMatchCount(seasonData)!;
            if (nextMatchCount.Count == matchCount.Count && nextMatchCount.Count > 1)
            {
            }

            matchCount = nextMatchCount;
            matchCounts.Add(matchCount);
        }

        return matchCounts;
    }

    private void AddNextMu(AytoResult matchCount, List<int> correctAnswer, SeasonData seasonData)
    {
        var previousMu = GetPreviousMu(matchCount, seasonData);

        var couplesToKeep = previousMu.Score;
        var sortedCouples = previousMu.Matching
            .ToArray()
            .Shuffle(Rnd)
            .OrderByDescending(pair => seasonData.MatchProbabilities[pair.A, pair.B].Probability > 0.9d)
            .ToList();

        // If any confirmed couples weren't in the matching, we must add them
        var matching = new Pair[Pairs];

        foreach (var pair in sortedCouples.Take(couplesToKeep))
        {
            matching[pair.A] = new Pair { A = pair.A, B = pair.B };
        }

        var couplesToShuffle = sortedCouples.Skip(couplesToKeep)
            .ToList();
        for (var index = 0; index < couplesToShuffle.Count; index++)
        {
            var coupleToShuffle = couplesToShuffle[index];
            matching[coupleToShuffle.A] = coupleToShuffle with
            {
                B = couplesToShuffle[(index + 1) % couplesToShuffle.Count].B
            };
        }

        var matchUp = new MatchUp
        {
            Matching = matching,
            // this is the number of pairs that are correct based on correctAnswer
            Score = matching.Where((x, index) => matching[index].B == correctAnswer[index]).Count()
        };
        seasonData.AddEvent(new Event { MatchUp = matchUp });
    }

    private static MatchUp GetPreviousMu(AytoResult matchCount, SeasonData seasonData)
    {
        var previousMu = matchCount.Events.Last(e => e.MatchUp != null).MatchUp!;
        var clone = (Pair[])previousMu.Matching.Clone();

        var updatedMu = new MatchUp
        {
            Matching = clone,
            Score = previousMu.Score
        };

        // check for any TBs outside the group
        var successfulTbs = seasonData.Events
            .SelectMany(e => e.TruthBooths)
            .Where(tb => tb.Result)
            .ToList();
        foreach (var successfulTb in successfulTbs)
        {
            var previousPair = previousMu.Matching[successfulTb.A];
            if (previousPair.B != successfulTb.B)
            {
                var (first, second) = (
                    first: updatedMu.Matching.Single(m => m.A == successfulTb.A),
                    second: updatedMu.Matching.Single(m => m.B == successfulTb.B));
                updatedMu.Matching[first.A] = new Pair { A = first.A, B = second.B };
                updatedMu.Matching[second.A] = new Pair { A = second.A, B = first.B };
                updatedMu.Score++;
            }
        }

        return updatedMu;
    }

    private void AddNextTb(AytoResult matchCount, List<int> correctAnswer, SeasonData seasonData)
    {
        var flattened = FlattenProbabilities(seasonData)
            .Where(p => p.Item1.Probability < 1 && p.Item1.Probability > 0d)
            .ToList();

        var mostLikelyPairings = flattened
            .GroupBy(p => p.Item1.Probability)
            .OrderByDescending(p => p.Key)
            .First();
        // Only pick a TB based on probability if 
        var probabilityThreshold = 1d / (Pairs - seasonData.Events.Sum(e => e.TruthBooths.Count(tb => tb.Result)));
        MatchProbability probabilityToPick;
        if (mostLikelyPairings.Key > probabilityThreshold)
        {
            probabilityToPick = mostLikelyPairings.OrderBy(p => p.Item2).First().Item1;
        }
        else
        {
            probabilityToPick = flattened
                .OrderBy(f => f.Item2)
                .ThenBy(f => f.Item1.HaveBeenPaired)
                .First().Item1;
        }

        var tbToPick = new TruthBooth
        {
            A = probabilityToPick.Man,
            B = probabilityToPick.Woman,
            Result = correctAnswer[probabilityToPick.Man] == probabilityToPick.Woman
        };

        seasonData.AddEvent(new Event
        {
            TruthBooths =
                [new TruthBooth { A = tbToPick.A, B = tbToPick.B, Result = correctAnswer[tbToPick.A] == tbToPick.B }]
        });
    }

    private Tuple<MatchProbability, int>[] FlattenProbabilities(SeasonData seasonData)
    {
        var flattened = new Tuple<MatchProbability, int>[Pairs * Pairs];

        for (var man = 0; man < Pairs; man++)
        {
            for (var woman = 0; woman < Pairs; woman++)
            {
                var truthBoothCount = seasonData.Events.SelectMany(e => e.TruthBooths)
                    .Count(tb => tb.A == man || tb.B == woman);
                var muCount = seasonData.Events
                    .Select(e => e.MatchUp)
                    .Count(mu => mu != null && (mu.Matching[man].B == woman || mu.Matching[man].B == man));
                flattened[man * Pairs + woman] = new(seasonData.MatchProbabilities[man, woman], truthBoothCount+muCount);
            }
        }

        return flattened;
    }

    private static int GetViableWomanForMan(List<int> correctAnswer, SeasonData seasonData, int man,
        List<int> remainingWomen)
    {
        var truthBooths = seasonData.Events.SelectMany(e => e.TruthBooths).ToList();
        var successfulTbs = truthBooths
            .Where(tb => tb.Result)
            .ToList();
        // if a woman has been in a successful truth with this man, return her
        if (successfulTbs.Any(tb => tb.A == man))
        {
            return successfulTbs.First(tb => tb.A == man).B;
        }

        var blackOutWomen = seasonData.Events
            .Where(e => e.MatchUp != null && e.MatchUp.Score == 0)
            .Select(e => e.MatchUp!.Matching[man].B);

        // Now, find a woman who:
        // - has not been in an unsuccessful TB with this man
        // - has been paired with them in a blackout
        // - has not been in a successful TB with any other man
        var viableWomen = remainingWomen
            .Where(i => !blackOutWomen.Contains(i))
            .Where(i => !truthBooths.Any(tb => tb.A == man && tb.B == i))
            .Where(i => !truthBooths.Any(tb => tb.A != man && tb.B == i && tb.Result))
            .ToList();

        // return a random element from the list of viable women
        return viableWomen.Any()
            ? viableWomen[Rnd.Next(0, viableWomen.Count)]
            : remainingWomen[Rnd.Next(0, remainingWomen.Count)];
    }

    private static void GetRandomMu(List<int> correctAnswer, SeasonData seasonData)
    {
        var matching = new Pair[Pairs];
        // Pick the men who've been in the most truth booths first, because they will have the smaller number of choices
        var shuffledMen = Rnd.ListOfNumbers(Pairs)
            .OrderByDescending(m => seasonData.Events
                .SelectMany(e => e.TruthBooths)
                .Count(tb => tb.A == m))
            .ToList();
        var allWomen = Enumerable.Range(0, Pairs).ToList();

        foreach (var man in shuffledMen)
        {
            var woman = GetViableWomanForMan(correctAnswer, seasonData, man, allWomen);
            allWomen.Remove(woman);
            matching[man] = new Pair { A = man, B = woman };
        }

        var matchUp = new MatchUp
        {
            Matching = matching,
            // this is the number of pairs that are correct based on correctAnswer
            Score = matching.Where((x, index) => matching[index].B == correctAnswer[index]).Count()
        };
        seasonData.AddEvent(new Event { MatchUp = matchUp });
    }

    private AytoResult? GetNextMatchCount(SeasonData seasonData)
    {
        var solver = new Solver(seasonData, _logger, seasonData.Events);
        var constraints = solver.BuildConstraints(seasonData);
        try
        {
            var matchCount = solver.SolveAllConstraints(constraints);
            return matchCount;
        }
        catch
        {
            _logger.LogDebug("Combination of events is infeasible");
            return null;
        }
    }

    private static TruthBooth GetInitialTb(int pairs, List<int> correctAnswer)
    {
        var man = Rnd.Next(0, pairs);
        var woman = Rnd.Next(0, pairs);
        var initialTb = new TruthBooth
        {
            A = man,
            B = woman,
            Result = correctAnswer[man] == woman
        };
        return initialTb;
    }
}