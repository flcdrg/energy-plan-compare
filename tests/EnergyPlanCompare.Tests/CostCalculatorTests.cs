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

    [Fact]
    public void RankPlans_TouHandlesMidnightWraparoundWindow()
    {
        // "Peak" wraps past midnight (1500-0059); listed after "Shoulder" so a naive first-block
        // fallback (pre-fix behaviour) would incorrectly charge the Shoulder rate for these hours.
        var shoulderWindow = new TouTimeOfUse("SUN|MON|TUE|WED|THU|FRI|SAT", "0600", "1459");
        var peakWindow = new TouTimeOfUse("SUN|MON|TUE|WED|THU|FRI|SAT", "1500", "0059");
        var plan = new PlanData(
            "PLAN-TOU-WRAP",
            "TOU Wraparound Plan",
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
                                new TouBlock("Shoulder", "S", [new BlockRate(20m, null, "KWH")], [shoulderWindow]),
                                new TouBlock("Peak", "P", [new BlockRate(50m, null, "KWH")], [peakWindow])
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
        consumption[276] = 1m; // 23:00 -> should match wraparound Peak window, not fallback
        consumption[6] = 1m;   // 00:30 -> also within the wraparound Peak window

        var interval = new IntervalData(
            new Dictionary<DateOnly, decimal[]> { [new DateOnly(2026, 6, 20)] = EmptyDay() },
            new Dictionary<DateOnly, decimal[]> { [new DateOnly(2026, 6, 20)] = consumption });

        var result = Assert.Single(new CostCalculator(new EligibilityFilter())
            .RankPlans([plan], interval, new EligibilityRequirements(false, false, false, false)));

        // 2 kWh at the 50c/kWh Peak rate = 100 cents = $1.00/day
        Assert.Equal(1.00m, result.DailyAverageDollars);
        Assert.Equal(365.00m, result.TotalCostDollars);
    }

    [Fact]
    public void RankPlans_SelectsSeasonalTariffPeriodByUsageDateNotLatestStartDate()
    {
        // Summer's startDate string ("2026-10-01") sorts later than Winter's ("2026-05-01"), so
        // picking a single globally "latest" period (pre-fix behaviour) would apply Summer's rate
        // to winter usage too. Each usage date must be matched to its own season.
        var summerPeriod = new TariffPeriod(
            [new BlockRate(50m, null, "KWH")], null, 0m, "2026-10-01", "2026-04-30", "P1Y", null);
        var winterPeriod = new TariffPeriod(
            [new BlockRate(10m, null, "KWH")], null, 0m, "2026-05-01", "2026-09-30", "P1Y", null);

        var plan = new PlanData(
            "PLAN-SEASONAL",
            "Seasonal Plan",
            "Test Retailer",
            "SR",
            null,
            null,
            [new Contract("SR", [summerPeriod, winterPeriod], null, null)]);

        var winterUsage = EmptyDay();
        winterUsage[0] = 1m; // 15 Jul 2026 -> Winter season, rate 10c
        var summerUsage = EmptyDay();
        summerUsage[0] = 1m; // 15 Nov 2026 -> Summer season, rate 50c

        var interval = new IntervalData(
            new Dictionary<DateOnly, decimal[]>
            {
                [new DateOnly(2026, 7, 15)] = EmptyDay(),
                [new DateOnly(2026, 11, 15)] = EmptyDay()
            },
            new Dictionary<DateOnly, decimal[]>
            {
                [new DateOnly(2026, 7, 15)] = winterUsage,
                [new DateOnly(2026, 11, 15)] = summerUsage
            });

        var result = Assert.Single(new CostCalculator(new EligibilityFilter())
            .RankPlans([plan], interval, new EligibilityRequirements(false, false, false, false)));

        // (1kWh * 10c) + (1kWh * 50c) = 60 cents over 2 days -> $0.30/day average
        Assert.Equal(0.30m, result.DailyAverageDollars);
        Assert.Equal(109.50m, result.TotalCostDollars);
    }

    [Fact]
    public void RankPlans_AppliesTieredBlockRateWithinTouWindow()
    {
        // The TOU block's own blockRate list carries volume tiers (first 24kWh free, then 20c/kWh),
        // mirroring real "Solar Sharer" / "Free Usage" TOU plans. Pre-fix, only BlockRate[0]
        // (0c/kWh) was ever used, so the second tier's usage was priced at zero.
        var window = new TouTimeOfUse("SUN|MON|TUE|WED|THU|FRI|SAT", "0000", "2359");
        var plan = new PlanData(
            "PLAN-TOU-TIERED",
            "TOU Tiered Plan",
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
                                new TouBlock(
                                    "Shoulder",
                                    "S",
                                    [new BlockRate(0m, 24m, "KWH"), new BlockRate(20m, null, "KWH")],
                                    [window])
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
        consumption[0] = 24m; // consumes the whole free tier
        consumption[1] = 6m;  // spills into the 20c/kWh tier

        var interval = new IntervalData(
            new Dictionary<DateOnly, decimal[]> { [new DateOnly(2026, 6, 20)] = EmptyDay() },
            new Dictionary<DateOnly, decimal[]> { [new DateOnly(2026, 6, 20)] = consumption });

        var result = Assert.Single(new CostCalculator(new EligibilityFilter())
            .RankPlans([plan], interval, new EligibilityRequirements(false, false, false, false)));

        // 24kWh free + 6kWh * 20c = 120 cents = $1.20/day
        Assert.Equal(1.20m, result.DailyAverageDollars);
        Assert.Equal(438.00m, result.TotalCostDollars);
    }

    private static decimal[] EmptyDay() => new decimal[288];
}
