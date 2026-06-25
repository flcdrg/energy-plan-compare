using EnergyPlanCompare.Models;
using EnergyPlanCompare.Services;

namespace EnergyPlanCompare.Tests;

public class CostCalculatorTests
{
    [Fact]
    public void RankPlans_SingleRateIncludesFitAndSupplyCharge()
    {
        var plan = new PlanData(
            "PLAN-SR",
            "Single Rate Plan",
            "Test Retailer",
            "SR",
            null,
            null,
            [
                new Contract(
                    "SR",
                    [new TariffPeriod([new BlockRate(20m, null, "KWH")], null, 100m, null, null, "P1D", null)],
                    [new SolarFit("R", "FiT", [new SolarFitRate(10m, null)])],
                    null)
            ]);

        var consumption = EmptyDay();
        var solar = EmptyDay();
        consumption[0] = 2m;
        consumption[1] = 3m;
        solar[0] = 1m;

        var interval = new IntervalData(
            new Dictionary<DateOnly, decimal[]> { [new DateOnly(2026, 6, 20)] = solar },
            new Dictionary<DateOnly, decimal[]> { [new DateOnly(2026, 6, 20)] = consumption });

        var results = new CostCalculator(new EligibilityFilter()).RankPlans([plan], interval, new EligibilityRequirements(false, false, false, false));
        var result = Assert.Single(results);

        Assert.Equal(1.90m, result.DailyAverageDollars);
        Assert.Equal(693.50m, result.TotalCostDollars);
    }

    [Fact]
    public void RankPlans_AppliesTieredSingleRate()
    {
        var plan = new PlanData(
            "PLAN-TIER",
            "Tier Plan",
            "Test Retailer",
            "SR",
            null,
            null,
            [
                new Contract(
                    "SR",
                    [new TariffPeriod([new BlockRate(10m, 10m, "KWH"), new BlockRate(20m, null, "KWH")], null, 0m, null, null, "P1D", null)],
                    null,
                    null)
            ]);

        var consumption = EmptyDay();
        consumption[0] = 12m;

        var interval = new IntervalData(
            new Dictionary<DateOnly, decimal[]> { [new DateOnly(2026, 6, 20)] = EmptyDay() },
            new Dictionary<DateOnly, decimal[]> { [new DateOnly(2026, 6, 20)] = consumption });

        var result = Assert.Single(new CostCalculator(new EligibilityFilter())
            .RankPlans([plan], interval, new EligibilityRequirements(false, false, false, false)));

        Assert.Equal(1.40m, result.DailyAverageDollars);
        Assert.Equal(511.00m, result.TotalCostDollars);
    }

    [Fact]
    public void RankPlans_TouUsesTimeBoundaries()
    {
        var peakWindow = new TouTimeOfUse("SUN|MON|TUE|WED|THU|FRI|SAT", "0600", "0959");
        var shoulderWindow = new TouTimeOfUse("SUN|MON|TUE|WED|THU|FRI|SAT", "1000", "1459");
        var plan = new PlanData(
            "PLAN-TOU",
            "TOU Plan",
            "Test Retailer",
            "TOU",
            null,
            null,
            [
                new Contract(
                    "TOU",
                    [
                        new TariffPeriod(
                            null,
                            [
                                new TouBlock("Peak", "P", [new BlockRate(50m, null, "KWH")], [peakWindow]),
                                new TouBlock("Shoulder", "S", [new BlockRate(30m, null, "KWH")], [shoulderWindow])
                            ],
                            0m,
                            null,
                            null,
                            "P1D",
                            null)
                    ],
                    null,
                    null)
            ]);

        var consumption = EmptyDay();
        consumption[119] = 1m; // 09:55 peak
        consumption[120] = 1m; // 10:00 shoulder

        var interval = new IntervalData(
            new Dictionary<DateOnly, decimal[]> { [new DateOnly(2026, 6, 20)] = EmptyDay() },
            new Dictionary<DateOnly, decimal[]> { [new DateOnly(2026, 6, 20)] = consumption });

        var result = Assert.Single(new CostCalculator(new EligibilityFilter())
            .RankPlans([plan], interval, new EligibilityRequirements(false, false, false, false)));

        Assert.Equal(0.80m, result.DailyAverageDollars);
        Assert.Equal(292.00m, result.TotalCostDollars);
    }

    private static decimal[] EmptyDay() => new decimal[288];
}
