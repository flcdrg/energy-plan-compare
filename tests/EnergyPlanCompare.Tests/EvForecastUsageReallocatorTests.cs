using EnergyPlanCompare.Models;
using EnergyPlanCompare.Services;

namespace EnergyPlanCompare.Tests;

public class EvForecastUsageReallocatorTests
{
    [Fact]
    public void Reallocate_PreservesDailyTotalAndAppliesWindowPercentage()
    {
        var day = new decimal[288];
        for (var i = 0; i < day.Length; i++)
        {
            day[i] = 1m;
        }

        var input = new IntervalData(
            new Dictionary<DateOnly, decimal[]> { [new DateOnly(2026, 7, 1)] = new decimal[288] },
            new Dictionary<DateOnly, decimal[]> { [new DateOnly(2026, 7, 1)] = day });

        var reallocator = new EvForecastUsageReallocator();
        var output = reallocator.Reallocate(input, new EvForecastSettings(0, 6, 50m));
        var remapped = output.ConsumptionByDate[new DateOnly(2026, 7, 1)];

        var total = remapped.Sum();
        var inWindow = remapped.Take(72).Sum(); // 00:00-05:55

        Assert.Equal(288m, total, 6);
        Assert.Equal(144m, inWindow, 6);
    }

    [Fact]
    public void Reallocate_HandlesOvernightWindow()
    {
        var day = new decimal[288];
        day[0] = 10m;    // 00:00
        day[120] = 10m;  // 10:00
        day[240] = 10m;  // 20:00

        var input = new IntervalData(
            new Dictionary<DateOnly, decimal[]> { [new DateOnly(2026, 7, 2)] = new decimal[288] },
            new Dictionary<DateOnly, decimal[]> { [new DateOnly(2026, 7, 2)] = day });

        var reallocator = new EvForecastUsageReallocator();
        var output = reallocator.Reallocate(input, new EvForecastSettings(22, 6, 80m));
        var remapped = output.ConsumptionByDate[new DateOnly(2026, 7, 2)];

        var total = remapped.Sum();
        var inWindow = remapped.Take(72).Sum() + remapped.Skip(264).Sum(); // 22:00-23:55 plus 00:00-05:55

        Assert.Equal(30m, total, 6);
        Assert.Equal(24m, inWindow, 6);
    }

    [Fact]
    public void Reallocate_WhenWindowHasNoHistoricalUsage_DistributesWindowUniformly()
    {
        var day = new decimal[288];
        day[144] = 12m; // noon only (outside 00:00-06:00 window)

        var input = new IntervalData(
            new Dictionary<DateOnly, decimal[]> { [new DateOnly(2026, 7, 3)] = new decimal[288] },
            new Dictionary<DateOnly, decimal[]> { [new DateOnly(2026, 7, 3)] = day });

        var reallocator = new EvForecastUsageReallocator();
        var output = reallocator.Reallocate(input, new EvForecastSettings(0, 6, 25m));
        var remapped = output.ConsumptionByDate[new DateOnly(2026, 7, 3)];

        var inWindow = remapped.Take(72).ToArray();
        var perWindowSlot = 3m / 72m;

        Assert.All(inWindow, value => Assert.Equal(perWindowSlot, value, 6));
        Assert.Equal(12m, remapped.Sum(), 6);
    }
}