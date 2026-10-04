namespace AreYouTheOne.Simulation;

public static class InformationMetrics
{
    /// <summary>
    /// Mean bits gained per event, -log2(count_t / count_{t-1}), over events whose preceding count was still above 1.
    /// This is the definition the console app has always used for "information gained".
    /// </summary>
    public static double MeanBitsPerEvent(IEnumerable<long> remainingCounts)
    {
        var informations = new List<double>();
        long? previous = null;
        foreach (var count in remainingCounts)
        {
            if (previous > 1)
            {
                informations.Add(-Math.Log2((double)count / previous.Value));
            }

            previous = count;
        }

        return informations.Count == 0 ? 0 : informations.Average();
    }
}
