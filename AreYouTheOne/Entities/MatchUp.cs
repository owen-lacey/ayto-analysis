namespace AreYouTheOne.Entities;

public record MatchUp
{
    public Pair[] Matching { get; set; } = [];
    public int Score { get; set; }

    public override string ToString()
    {
        return $"MatchUp: {string.Join(", ", Matching.Select(p => $"{p.A}-{p.B}"))} (Score: {Score})";
    }
}