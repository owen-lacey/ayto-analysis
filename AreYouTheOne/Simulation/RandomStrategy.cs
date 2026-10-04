using AreYouTheOne.Entities;
using AreYouTheOne.Human;

namespace AreYouTheOne.Simulation;

/// <summary>
/// Harness control mirroring <c>RandomlyGenerated</c>: random truth booths and lineups that only avoid pairs already
/// known to be wrong (and keep known matches). Used to check that this harness reproduces the quoted Random baseline.
/// </summary>
public class RandomStrategy(int pairs, Random random) : IStrategy
{
    private readonly PointsGrid _known = new(pairs);

    public Pair NextTruthBooth()
    {
        var candidates = Enumerable.Range(0, pairs * pairs)
            .Select(i => new Pair { A = i / pairs, B = i % pairs })
            .Where(p => _known.IsUnknown(p.A, p.B))
            .ToArray();
        return candidates.Length == 0 ? new Pair { A = 0, B = 0 } : candidates[random.Next(candidates.Length)];
    }

    public Pair[] NextMatchUp()
    {
        var builder = new LineupBuilder(pairs, _known, random);
        builder.PlaceConfirmed();
        builder.PlaceGreedy(int.MaxValue, _ => 0);
        return builder.Complete();
    }

    public void Observe(TruthBooth truthBooth) => _known.ApplyTruthBooth(truthBooth);

    public void Observe(MatchUp matchUp) => _known.ApplyMatchUp(matchUp);
}
