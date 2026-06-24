using System.Globalization;
using EnergyPlanCompare.Models;

namespace EnergyPlanCompare.Services;

public sealed class CostCalculator
{
    private static readonly Dictionary<DayOfWeek, string> DayMap = new()
    {
        [DayOfWeek.Monday] = "MON",
        [DayOfWeek.Tuesday] = "TUE",
        [DayOfWeek.Wednesday] = "WED",
        [DayOfWeek.Thursday] = "THU",
        [DayOfWeek.Friday] = "FRI",
        [DayOfWeek.Saturday] = "SAT",
        [DayOfWeek.Sunday] = "SUN"
    };

    private readonly EligibilityFilter _eligibilityFilter;

    public CostCalculator(EligibilityFilter eligibilityFilter)
    {
        _eligibilityFilter = eligibilityFilter;
    }

    public List<PlanCostResult> RankPlans(
        IEnumerable<PlanData> plans,
        IntervalData intervalData,
        EligibilityRequirements requirements)
    {
        var results = new List<PlanCostResult>();

        foreach (var plan in plans)
        {
            if (!_eligibilityFilter.IsEligible(plan, requirements, out var notes))
            {
                continue;
            }

            var result = CalculatePlan(plan, intervalData, notes);
            if (result is not null)
            {
                results.Add(result);
            }
        }

        return results
            .OrderBy(r => r.TotalCostDollars)
            .ThenBy(r => r.PlanId, StringComparer.Ordinal)
            .ToList();
    }

    private static PlanCostResult? CalculatePlan(PlanData plan, IntervalData intervalData, IReadOnlyList<string> notes)
    {
        var contract = plan.Contract.FirstOrDefault();
        if (contract?.TariffPeriod is null || contract.TariffPeriod.Count == 0)
        {
            return null;
        }

        var tariffPeriod = SelectTariffPeriod(contract.TariffPeriod);
        var dailySupplyChargeCents = tariffPeriod.DailySupplyCharge ?? 0m;
        var fitRateCents = SelectRetailerFitRate(contract.SolarFit);

        decimal consumptionCostCents = 0m;
        decimal feedInCreditCents = 0m;
        decimal supplyCostCents = 0m;

        foreach (var (date, consumptionValues) in intervalData.ConsumptionByDate.OrderBy(x => x.Key))
        {
            intervalData.SolarByDate.TryGetValue(date, out var solarValues);
            solarValues ??= new decimal[consumptionValues.Length];

            if (consumptionValues.Length != solarValues.Length)
            {
                throw new InvalidDataException($"Solar/consumption slot mismatch on {date:yyyy-MM-dd}.");
            }

            var dayCumulativeKwh = 0m;
            for (var slot = 0; slot < consumptionValues.Length; slot++)
            {
                var usage = Math.Max(0m, consumptionValues[slot]);
                var solar = Math.Max(0m, solarValues[slot]);

                if (plan.TariffType.Equals("TOU", StringComparison.OrdinalIgnoreCase))
                {
                    var rate = ResolveTouRateCents(tariffPeriod, date, slot);
                    consumptionCostCents += usage * rate;
                    dayCumulativeKwh += usage;
                }
                else
                {
                    consumptionCostCents += ResolveSingleRateCostCents(tariffPeriod, usage, ref dayCumulativeKwh);
                }

                feedInCreditCents += solar * fitRateCents;
            }

            supplyCostCents += dailySupplyChargeCents;
        }

        var totalCents = consumptionCostCents - feedInCreditCents + supplyCostCents;
        var dayCount = intervalData.DayCount;
        var totalDollars = Math.Round(totalCents / 100m, 2, MidpointRounding.AwayFromZero);
        var dailyAverage = dayCount == 0
            ? 0m
            : Math.Round(totalDollars / dayCount, 2, MidpointRounding.AwayFromZero);

        return new PlanCostResult(
            plan.PlanId,
            plan.PlanName,
            plan.RetailerName,
            plan.TariffType,
            totalDollars,
            dailyAverage,
            dayCount,
            notes);
    }

    private static TariffPeriod SelectTariffPeriod(List<TariffPeriod> tariffPeriods)
    {
        TariffPeriod? best = null;
        DateOnly? bestStart = null;

        foreach (var tariffPeriod in tariffPeriods)
        {
            var start = ParseDate(tariffPeriod.StartDate);
            if (best is null)
            {
                best = tariffPeriod;
                bestStart = start;
                continue;
            }

            if (start is not null && (bestStart is null || start > bestStart))
            {
                best = tariffPeriod;
                bestStart = start;
            }
        }

        return best ?? tariffPeriods[0];
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static decimal SelectRetailerFitRate(List<SolarFit>? solarFits)
    {
        if (solarFits is null || solarFits.Count == 0)
        {
            return 0m;
        }

        var retailerFit = solarFits.FirstOrDefault(x => string.Equals(x.Type, "R", StringComparison.OrdinalIgnoreCase))
            ?? solarFits[0];

        return retailerFit.SingleTariffRates?.FirstOrDefault()?.UnitPrice ?? 0m;
    }

    private static decimal ResolveSingleRateCostCents(TariffPeriod tariffPeriod, decimal usageKwh, ref decimal dayCumulativeKwh)
    {
        var rates = tariffPeriod.BlockRate;
        if (rates is null || rates.Count == 0 || usageKwh <= 0m)
        {
            dayCumulativeKwh += usageKwh;
            return 0m;
        }

        var remaining = usageKwh;
        var cost = 0m;
        var running = dayCumulativeKwh;

        for (var i = 0; i < rates.Count; i++)
        {
            var rate = rates[i];
            var upperBound = rate.Volume ?? decimal.MaxValue;
            if (running >= upperBound)
            {
                continue;
            }

            var availableInTier = upperBound - running;
            var billed = Math.Min(remaining, availableInTier);
            if (billed <= 0m)
            {
                continue;
            }

            cost += billed * rate.UnitPrice;
            remaining -= billed;
            running += billed;

            if (remaining <= 0m)
            {
                break;
            }
        }

        if (remaining > 0m)
        {
            var fallbackRate = rates[^1].UnitPrice;
            cost += remaining * fallbackRate;
            running += remaining;
        }

        dayCumulativeKwh = running;
        return cost;
    }

    private static decimal ResolveTouRateCents(TariffPeriod tariffPeriod, DateOnly date, int slot)
    {
        var touBlocks = tariffPeriod.TouBlock;
        if (touBlocks is null || touBlocks.Count == 0)
        {
            return tariffPeriod.BlockRate?.FirstOrDefault()?.UnitPrice ?? 0m;
        }

        var dayCode = DayMap[date.DayOfWeek];
        var slotMinutes = slot * 5;
        var hours = slotMinutes / 60;
        var minutes = slotMinutes % 60;
        var slotTime = (hours * 100) + minutes;

        foreach (var block in touBlocks)
        {
            var rate = block.BlockRate?.FirstOrDefault()?.UnitPrice ?? 0m;
            var windows = block.TimeOfUse;
            if (windows is null || windows.Count == 0)
            {
                continue;
            }

            foreach (var window in windows)
            {
                if (string.IsNullOrWhiteSpace(window.Days) ||
                    !window.Days.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Contains(dayCode, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var start = int.Parse(window.StartTime, CultureInfo.InvariantCulture);
                var end = int.Parse(window.EndTime, CultureInfo.InvariantCulture);
                if (slotTime >= start && slotTime <= end)
                {
                    return rate;
                }
            }
        }

        return touBlocks.FirstOrDefault()?.BlockRate?.FirstOrDefault()?.UnitPrice ?? 0m;
    }
}

