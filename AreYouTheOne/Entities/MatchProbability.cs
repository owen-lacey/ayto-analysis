namespace AreYouTheOne.Entities;

public record MatchProbability(int Man, int Woman)
{
    /// <summary>
    /// Number between 0 and 1 representing the probability of this couple being a match.
    /// </summary>
    public double? Probability =>
        Probabilities.Count > 0 ? Probabilities.Average() : null;
    
    public List<double> Probabilities { get; set; } = [];

    /// <summary>
    /// Has this couple been paired in the past?
    /// If TB pairing, it'll be 1 or 0 depending on the result.
    /// If a MU, it'll be score / pairs
    /// </summary>
    public bool HaveBeenPaired { get; set; }

    public bool IsKnown => Probability == 0d || Probability > 0.99d;
}