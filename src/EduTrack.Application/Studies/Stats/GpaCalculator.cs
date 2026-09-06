namespace EduTrack.Application.Studies.Stats;

public static class GpaCalculator
{
    public static double? WeightedAverage(IEnumerable<(int Value, decimal Weight)> grades)
    {
        double weightedSum = 0;
        double totalWeight = 0;

        foreach (var (value, weight) in grades)
        {
            var w = weight > 0 ? (double)weight : 1.0;
            weightedSum += value * w;
            totalWeight += w;
        }

        return totalWeight == 0 ? null : Math.Round(weightedSum / totalWeight, 2);
    }
}
