namespace AreYouTheOne.Entities;

public record Event
{
    public TruthBooth[] TruthBooths { get; set; } = [];

    public MatchUp? MatchUp { get; set; }

    public override string ToString() => MatchUp != null ? $"{MatchUp}" : $"{TruthBooths[0]}";
}

public record ScoreChange
{
    public int Score { get; set; }

    public int ResultingChanges { get; set; }
}