using AreYouTheOne.Entities;

namespace AreYouTheOne.Simulation;

/// <summary>
/// A strategy only ever sees the events it chose and their results. It never sees the hidden matching or the
/// <see cref="ViableSet"/>.
/// </summary>
public interface IStrategy
{
    Pair NextTruthBooth();

    /// <summary>A perfect matching, indexed by man: element i has A == i.</summary>
    Pair[] NextMatchUp();

    void Observe(TruthBooth truthBooth);

    void Observe(MatchUp matchUp);
}
