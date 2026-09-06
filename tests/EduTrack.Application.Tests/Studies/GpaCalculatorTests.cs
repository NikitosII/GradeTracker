using EduTrack.Application.Studies.Stats;
using FluentAssertions;

namespace EduTrack.Application.Tests.Studies;

public class GpaCalculatorTests
{
    [Fact]
    public void Empty_returns_null()
    {
        GpaCalculator.WeightedAverage(Array.Empty<(int, decimal)>()).Should().BeNull();
    }

    [Fact]
    public void Unweighted_grades_average_normally()
    {
        var grades = new (int, decimal)[] { (5, 1m), (4, 1m), (3, 1m) };

        GpaCalculator.WeightedAverage(grades).Should().Be(4.0);
    }

    [Fact]
    public void Weights_shift_the_average()
    {
        // (5*3 + 3*1) / (3+1) = 18/4 = 4.5
        var grades = new (int, decimal)[] { (5, 3m), (3, 1m) };

        GpaCalculator.WeightedAverage(grades).Should().Be(4.5);
    }

    [Fact]
    public void Non_positive_weight_counts_as_one()
    {
        // both treated as weight 1 -> (5 + 3) / 2 = 4
        var grades = new (int, decimal)[] { (5, 0m), (3, -2m) };

        GpaCalculator.WeightedAverage(grades).Should().Be(4.0);
    }

    [Fact]
    public void Rounds_to_two_decimals()
    {
        // (5 + 4 + 4) / 3 = 4.333...
        var grades = new (int, decimal)[] { (5, 1m), (4, 1m), (4, 1m) };

        GpaCalculator.WeightedAverage(grades).Should().Be(4.33);
    }
}
