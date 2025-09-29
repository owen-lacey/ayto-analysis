using AreYouTheOne.Console.Helpers;
using AreYouTheOne.Entities;
using Microsoft.Extensions.Logging;

namespace AreYouTheOne.Console.Data;

public class InformationTheory(ILogger logger)
{
    private readonly ILogger _logger = logger;

    private const int Pairs = 6;
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
            _logger.LogWarning($"Computing next match count. Remaining matches: {matchCount.Count}. Events: {seasonData.Events.Count}");
            AddNextTb(matchCount, correctAnswer, seasonData);
            matchCount = GetNextMatchCount(seasonData)!;
            matchCounts.Add(matchCount);

            _logger.LogWarning($"Computing next match count. Remaining matches: {matchCount.Count}. Events: {seasonData.Events.Count}");
            AddNextMu(matchCount, correctAnswer, seasonData);
            matchCount = GetNextMatchCount(seasonData)!;
            matchCounts.Add(matchCount);
        }

        return matchCounts;
    }

    private void AddNextMu(AytoResult matchCount, List<int> correctAnswer, SeasonData seasonData)
    {
        var potentialMus = new List<PotentialMu>();
        var index = 0;
        
        // Pick 10 random MUs to evaluate, because there are too many combinations
        var remainingMatches = matchCount.RemainingMatches
            .OrderBy(_ => Rnd.Next())
            // .Take(50)
            .ToList();
        foreach (var remainingMatch in remainingMatches)
        {
            _logger.LogInformation($"Evaluating potential MU {index + 1} of {remainingMatches.Count}. Match: {string.Join(", ", remainingMatch)}");
            var scores = new List<PotentialMuScore>();
            var pairs = remainingMatch.Select((woman, man) => new Pair { A = man, B = woman}).ToArray();
            for (var score = 0; score <= Pairs; score++)
            {
                var matchup = new MatchUp
                {
                    Matching = pairs,
                    Score = score
                };
                var result = new Event { MatchUp = matchup };
                seasonData.AddTempEvent(result);
                var successMatchCount = GetNextMatchCount(seasonData);
                if (successMatchCount == null)
                {
                    _logger.LogDebug($"Skipping score {score} for matchup {string.Join(", ", matchup.Matching.Select(p => $"{p.B}"))} because it is infeasible.");
                }
                else
                {
                    _logger.LogDebug($"Matching {string.Join(", ", matchup.Matching.Select(p => $"{p.B}"))} with score {score} would lead to {successMatchCount.Count} remaining couples.");
                    scores.Add(new PotentialMuScore{ Score = score, RemainingMatches = successMatchCount.Count});
                }
                seasonData.RemoveTempEvent(result);
            }
            _logger.LogDebug($"There are {scores.Count} scores for matching {string.Join(", ", remainingMatch)}: {string.Join(", ", scores)}");
            var potentialMu = new PotentialMu(matchCount.RemainingMatches.Count, remainingMatch, scores);
            _logger.LogDebug($"E(Information) for matching {string.Join(", ", remainingMatch)} is {potentialMu.ExpectedInformation}");
            potentialMus.Add(potentialMu);
            index++;
        }
        
        // Pick the truth booth with the lowest average remaining couples
        var sortedPicks = potentialMus
            .OrderByDescending(tb => tb.ExpectedInformation)
            .ToList();
        var matchingToPick = sortedPicks
            .First();

        var matching = matchingToPick.MatchUp.Select((woman, man) => new Pair { A = man, B = woman }).ToArray();
        var matchUp = new MatchUp
        {
            Matching = matching,
            // this is the number of pairs that are correct based on correctAnswer
            Score = matchingToPick.MatchUp.Where((x, index) => matching[index].B == correctAnswer[index]).Count()
        };
        seasonData.AddEvent(new Event { MatchUp = matchUp });
    }

    private void AddNextTb(AytoResult matchCount, List<int> correctAnswer, SeasonData seasonData)
    {
        var viableTbs = new List<Pair>();
        for (var man = 0; man < Pairs; man++)
        {
            var viableMatches = matchCount.RemainingMatches.Select(m => m[man]).Distinct().ToList();
            _logger.LogDebug($"Man {man} has viable matches: {string.Join(", ", viableMatches)}");
            viableMatches.ForEach(woman => viableTbs.Add(new Pair { A = man, B = woman }));
        }

        var index = 0;
        var potentialTbs = new List<PotentialTb>();
        // Pick 10 random TBs to evaluate, because there are too many combinations
        viableTbs = viableTbs
            .OrderBy(_ => Rnd.Next())
            // .Take(10)
            .ToList();
        foreach (var viableTb in viableTbs)
        {
            _logger.LogInformation($"Evaluating potential TB {index + 1} of {viableTbs.Count}. TB: {viableTb.A} - {viableTb.B}");
            var successResult = new Event
                { TruthBooths = [new TruthBooth { A = viableTb.A, B = viableTb.B, Result = true }] };
            seasonData.AddTempEvent(successResult);
            var successMatchCount = GetNextMatchCount(seasonData);
            seasonData.RemoveTempEvent(successResult);
            var failureResult = new Event
                { TruthBooths = [new TruthBooth { A = viableTb.A, B = viableTb.B, Result = true }] };
            seasonData.AddTempEvent(failureResult);
            var failureMatchCount = GetNextMatchCount(seasonData);
            seasonData.RemoveTempEvent(failureResult);
            potentialTbs.Add(new PotentialTb(matchCount.Count, viableTb.A, viableTb.B, successMatchCount.Count, failureMatchCount.Count));
            index++;
        }
        
        // Pick the truth booth with the lowest average remaining couples
        var sortedTbs = potentialTbs
            .OrderByDescending(tb => tb.ExpectedInformation)
            .ToList();
        var tbToPick = sortedTbs
            .First();
        
        seasonData.AddEvent(new Event { TruthBooths = [new TruthBooth { A = tbToPick.A, B = tbToPick.B, Result = correctAnswer[tbToPick.A] == tbToPick.B}]});
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