using System.Runtime.CompilerServices;

namespace AreYouTheOne.Entities;

public record PotentialTb
{
    public PotentialTb(int startingNumberOfMatches, int a, int b, int successRemainingMatches, int failureRemainingMatches)
    {
        A = a;
        B = b;
        var scoreBasedInformation = new List<double>();
        foreach (var potentialMuScore in new List<int> { successRemainingMatches, failureRemainingMatches})
        {
            var probability = (double)potentialMuScore / startingNumberOfMatches;
            var information = - Math.Log2(probability);
            var expectedInformation = information * probability;
            scoreBasedInformation.Add(expectedInformation);
        }
        ExpectedInformation = scoreBasedInformation.Sum();
    }
    public int A { get; }
    public int B { get; }

    public double ExpectedInformation { get; }
}

public record PotentialMu
{
    public PotentialMu(int startingNumberOfMatches, List<int> matchUp, List<PotentialMuScore> scores)
    {
        MatchUp = matchUp;
        Scores = scores;
        var scoreBasedInformation = new List<double>();
        foreach (var potentialMuScore in scores)
        {
            var probability = (double)potentialMuScore.RemainingMatches / startingNumberOfMatches;
            var information = - Math.Log2(probability);
            var expectedInformation = information * probability;
            scoreBasedInformation.Add(expectedInformation);
        }
        ExpectedInformation = scoreBasedInformation.Sum();
        
    }
    public List<int> MatchUp { get; }

    public List<PotentialMuScore> Scores { get; }

    public double ExpectedInformation { get; }
}

public record PotentialMuScore
{
    public int Score { get; set; }

    public int RemainingMatches { get; set; }
}