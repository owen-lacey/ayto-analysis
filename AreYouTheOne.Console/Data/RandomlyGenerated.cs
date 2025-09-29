using AreYouTheOne.Console.Helpers;
using AreYouTheOne.Entities;
using Microsoft.Extensions.Logging;

namespace AreYouTheOne.Console.Data;

public class RandomlyGenerated(ILogger logger)
{
    private ILogger _logger = logger;

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

        var initialTb = GetInitialTb(Pairs, correctAnswer);
        seasonData.Events.Add(new Event { TruthBooths = [initialTb] });

        var matchCount = new AytoResult(Pairs, initialTb);
        matchCounts.Add(matchCount);

        SimulateNextMatchUp(correctAnswer, seasonData);
        matchCount = GetNextMatchCount(seasonData);

        while (matchCount.Count > 1)
        {
            SimulateNextTb(matchCount, correctAnswer, seasonData);
            matchCount = GetNextMatchCount(seasonData);
            matchCounts.Add(matchCount);

            SimulateNextMatchUp(correctAnswer, seasonData);
            matchCount = GetNextMatchCount(seasonData);
            matchCounts.Add(matchCount);
        }

        return matchCounts;
    }

    private static void SimulateNextTb(AytoResult matchCount, List<int> correctAnswer, SeasonData seasonData)
    {
        // Pick a man & a woman who we don't know are a perfect match yet, based on previous TBs
        var successfulTbs = seasonData.Events.SelectMany(e => e.TruthBooths)
            .Where(tb => tb.Result)
            .ToList();

        // get a random man who hasn't been in a successful truth booth yet
        var man = Enumerable.Range(0, correctAnswer.Count)
            .Where(i => successfulTbs.All(tb => tb.A != i))
            .OrderBy(_ => Rnd.Next())
            .First();
        var woman = GetViableWomanForMan(correctAnswer, seasonData, man, Enumerable.Range(0, Pairs).ToList());

        var truthBooth = new TruthBooth
        {
            A = man,
            B = woman,
            Result = correctAnswer[man] == woman
        };
        seasonData.Events.Add(new Event { TruthBooths = [truthBooth] });
    }

    private static int GetViableWomanForMan(List<int> correctAnswer, SeasonData seasonData, int man, List<int> remainingWomen)
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

    private static void SimulateNextMatchUp(List<int> correctAnswer, SeasonData seasonData)
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
        seasonData.Events.Add(new Event { MatchUp = matchUp });
    }

    private AytoResult GetNextMatchCount(SeasonData seasonData)
    {
        var solver = new Solver(seasonData, _logger, seasonData.Events);
        var constraints = solver.BuildConstraints(seasonData);
        var matchCount = solver.SolveAllConstraints(constraints);
        return matchCount;
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