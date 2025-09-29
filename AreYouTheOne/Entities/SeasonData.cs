using System.Text;

namespace AreYouTheOne.Entities;

public class SeasonData
{
    private readonly int _pairs;

    public SeasonData(int pairs)
    {
        _pairs = pairs;

        MatchProbabilities = new MatchProbability[pairs, pairs];
        for (int man = 0; man < pairs; man++)
        {
            for (int woman = 0; woman < pairs; woman++)
            {
                MatchProbabilities[man, woman] = new MatchProbability(man, woman);
            }
        }
    }

    public string MatchProbabilitiesDisplay
    {
        get
        {
            // format the 2D array as a table-like string for console inspection
            var sb = new StringBuilder();
            sb.Append($"    ");
            for (int i = 0; i < _pairs; i++)
            {
                sb.Append($" W{i,0 - 3} ");
            }

            sb.AppendLine();
            for (int i = 0; i < _pairs; i++)
            {
                sb.Append($"M{i,2} ");
                for (int j = 0; j < _pairs; j++)
                {
                    sb.Append(MatchProbabilities[i, j].Probability != null
                        ? $"{MatchProbabilities[i, j].Probability,5:F2} "
                        : "      ");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    public MatchProbability[,] MatchProbabilities { get; }
    public Contestants Contestants { get; set; } = null!;
    public List<Event> Events { get; set; } = [];

    public void AddEvent(Event eventToAdd)
    {
        Events.Add(eventToAdd);
        UpdateMatchProbabilities(eventToAdd);
    }

    private void UpdateMatchProbabilities(Event eventToAdd)
    {
        FillInMissingProbabilities();
        
        if (eventToAdd.MatchUp != null)
        {
            HandleMatchUp(eventToAdd.MatchUp);
        }

        foreach (var truthBooth in eventToAdd.TruthBooths)
        {
            HandleTruthBooth(truthBooth);
        }
    }

    private void FillInMissingProbabilities()
    {
        for (var man = 0; man < _pairs; man++)
        {
            var knownMatchings = Enumerable.Range(0, _pairs)
                .Where(woman => MatchProbabilities[man, woman].IsKnown)
                .ToList();
            if (knownMatchings.Any(woman => MatchProbabilities[man, woman].Probability > 0.99d))
            {
                continue;
            }

            var remainingProbability = 1d / (_pairs - knownMatchings.Count);
            for (var woman = 0; woman < _pairs; woman++)
            {
                if (!MatchProbabilities[man, woman].IsKnown)
                {
                    MatchProbabilities[man, woman].Probabilities = [remainingProbability];
                }
            }
        }
    }

    private void HandleTruthBooth(TruthBooth truthBooth)
    {
        MatchProbabilities[truthBooth.A, truthBooth.B].HaveBeenPaired = true;
        MatchProbabilities[truthBooth.A, truthBooth.B].Probabilities = (truthBooth.Result ? [1d] : [0d]);

        if (truthBooth.Result)
        {
            for (int i = 0; i < _pairs; i++)
            {
                if (i != truthBooth.B)
                {
                    MatchProbabilities[truthBooth.A, i].Probabilities = [0d];
                    MatchProbabilities[truthBooth.A, i].HaveBeenPaired = true;
                }

                if (i != truthBooth.A)
                {
                    MatchProbabilities[i, truthBooth.B].Probabilities = [0d];
                    MatchProbabilities[truthBooth.A, i].HaveBeenPaired = true;
                }
            }
        }

        var previousOccurrencesOfPair = Events.Where(e =>
                e.MatchUp != null && e.MatchUp.Matching.Any(m => m.A == truthBooth.A && m.B == truthBooth.B))
            .Select(e => e.MatchUp!)
            .ToList();
        foreach (var matchUp in previousOccurrencesOfPair)
        {
            UpdateProbabilitiesFromMatchUp(matchUp);
        }
    }

    private void HandleMatchUp(MatchUp matchUp)
    {
        UpdateProbabilitiesFromMatchUp(matchUp);
    }

    private void UpdateProbabilitiesFromMatchUp(MatchUp matchUp)
    {
        double confirmedPerfectMatchesInMu = matchUp.Matching
            .Count(pair => MatchProbabilities[pair.A, pair.B].HaveBeenPaired &&
                           MatchProbabilities[pair.A, pair.B].Probability > 0.9);
        double confirmedImperfectMatchesInMu = matchUp.Matching
            .Count(pair => MatchProbabilities[pair.A, pair.B].HaveBeenPaired &&
                           MatchProbabilities[pair.A, pair.B].Probability == 0d);

        var probabilityOfRemainingPairs = (matchUp.Score - confirmedPerfectMatchesInMu) /
                                          (_pairs - confirmedPerfectMatchesInMu - confirmedImperfectMatchesInMu);
        foreach (var pair in matchUp.Matching)
        {
            if (MatchProbabilities[pair.A, pair.B].Probability > 0.9 ||
                MatchProbabilities[pair.A, pair.B].Probability == 0d)
            {
                // If this pair has already been confirmed, we don't need to change its probability
                continue;
            }

            MatchProbabilities[pair.A, pair.B].HaveBeenPaired = true;
            MatchProbabilities[pair.A, pair.B].Probabilities = [probabilityOfRemainingPairs];
        }
    }

    public void AddTempEvent(Event eventToAdd)
    {
        Events.Add(eventToAdd);
    }

    public void RemoveTempEvent(Event eventToAdd)
    {
        Events.Remove(eventToAdd);
    }
}