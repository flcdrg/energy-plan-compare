using EnergyPlanCompare.Models;
using EnergyPlanCompare.Services;

namespace EnergyPlanCompare.Tests;

public class IntervalDataFilterTests
{
    [Fact]
    public void ForDate_ReturnsOnlyRequestedDay()
    {
        var day1 = new DateOnly(2026, 7, 1);
        var day2 = new DateOnly(2026, 7, 2);

        var intervalData = new IntervalData(
            new Dictionary<DateOnly, decimal[]>
            {
                [day1] = new decimal[288],
                [day2] = new decimal[288]
            },
            new Dictionary<DateOnly, decimal[]>
            {
                [day1] = new decimal[288],
                [day2] = new decimal[288]
            });

        var filter = new IntervalDataFilter();
        var filtered = filter.ForDate(intervalData, day2);

        Assert.Single(filtered.ConsumptionByDate);
        Assert.Single(filtered.SolarByDate);
        Assert.True(filtered.ConsumptionByDate.ContainsKey(day2));
        Assert.True(filtered.SolarByDate.ContainsKey(day2));
    }

    [Fact]
    public void ForDate_WhenSolarMissing_StillReturnsConsumptionDay()
    {
        var day = new DateOnly(2026, 7, 3);
        var intervalData = new IntervalData(
            new Dictionary<DateOnly, decimal[]>(),
            new Dictionary<DateOnly, decimal[]> { [day] = new decimal[288] });

        var filter = new IntervalDataFilter();
        var filtered = filter.ForDate(intervalData, day);

        Assert.Single(filtered.ConsumptionByDate);
        Assert.Empty(filtered.SolarByDate);
    }

    [Fact]
    public void ForDate_WhenConsumptionMissing_Throws()
    {
        var day = new DateOnly(2026, 7, 4);
        var intervalData = new IntervalData(
            new Dictionary<DateOnly, decimal[]>(),
            new Dictionary<DateOnly, decimal[]>());

        var filter = new IntervalDataFilter();

        var ex = Assert.Throws<InvalidOperationException>(() => filter.ForDate(intervalData, day));
        Assert.Contains("2026-07-04", ex.Message);
    }
}