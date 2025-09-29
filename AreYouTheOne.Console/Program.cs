using AreYouTheOne.Console.Data;
using AreYouTheOne.Entities;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Critical); // Change this to set the minimum log level
});
var logger = loggerFactory.CreateLogger<Program>();

var i = 0;
var scoreChanges = new List<ScoreChange>();
var averageInformationScores = new List<double>();
while (i < 1000)
{ 
    // Load the JSON file from embedded resource
    var result = new ManualInformationTheory(logger).Generate();
    
    // for each event, get the score of the match up, followed by the number of changes made in the following match up 
    scoreChanges.AddRange(CalculateScoreChanges(result.Last().Events));

    var infoGained = CalculateInformationGained(result);
    averageInformationScores.Add(infoGained);

    logger.LogCritical("Information gained: {InformationGained}. Running average: {RunningAverage}", infoGained, averageInformationScores.Average());
    logger.LogCritical($"{string.Join(", ", result.Select(n => n.Count))}");
    i++;
}

logger.LogCritical("Average information gained: {InformationGained}", averageInformationScores.Average());

List<ScoreChange> CalculateScoreChanges(List<Event> list)
{
    var scoreChanges = new List<ScoreChange>();
    MatchUp? previousMatchUp = null;
    foreach (var matchUp in list.Where(e => e.MatchUp != null).Select(e => e.MatchUp!))
    {
        if (previousMatchUp != null)
        {
            var changes = 0;
            foreach (var pair in matchUp.Matching)
            {
                var previousPairForMan = previousMatchUp.Matching.Single(p => p.A == pair.A);
                if (previousPairForMan.B != pair.B)
                {
                    changes++;
                }
            }
            scoreChanges.Add(new ScoreChange { Score = previousMatchUp.Score, ResultingChanges = changes });
        }
        
        previousMatchUp = matchUp;
    }

    return scoreChanges;
}

double CalculateInformationGained(List<AytoResult> results)
{
    if (results.Count <= 1)
    {
        return 0;
    }
    var informations = new List<double>();
    AytoResult? previous = null;
    foreach (var result in results)
    {
        if (previous != null && previous.Count > 1)
        {
            var probability = (double)result.Count / previous.Count;
            var informationGained = -Math.Log2(probability);
            informations.Add(informationGained);
        }

        previous = result;
    }

    return informations.Average();
}