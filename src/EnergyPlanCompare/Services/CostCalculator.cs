using System.Globalization;
using EnergyPlanCompare.Models;

namespace EnergyPlanCompare.Services;

public sealed class CostCalculator
{
    private const int DaysInYear = 365;

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

        var tariffPeriods = contract.TariffPeriod;
        var defaultTariffPeriod = SelectTariffPeriod(tariffPeriods);
        var fitRateCents = SelectRetailerFitRate(contract.SolarFit);
        var isTou = plan.TariffType.Equals("TOU", StringComparison.OrdinalIgnoreCase);

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

            // Seasonal plans (e.g. separate Summer/Winter tariffPeriod entries) must be matched
            // to each usage date individually; falling back to a single globally-selected period
            // would apply one season's rates to the whole year.
            var tariffPeriod = SelectTariffPeriodForDate(tariffPeriods, date) ?? defaultTariffPeriod;
            var dailySupplyChargeCents = tariffPeriod.DailySupplyCharge ?? 0m;

            var dayCumulativeKwh = 0m;
            var touBlockCumulativeKwh = isTou ? new Dictionary<string, decimal>(StringComparer.Ordinal) : null;

            for (var slot = 0; slot < consumptionValues.Length; slot++)
            {
                var usage = Math.Max(0m, consumptionValues[slot]);
                var solar = Math.Max(0m, solarValues[slot]);

                if (isTou)
                {
                    consumptionCostCents += ResolveTouCostCents(tariffPeriod, date, slot, usage, touBlockCumulativeKwh!);
                }
                else
                {
                    consumptionCostCents += ResolveSingleRateCostCents(tariffPeriod, usage, ref dayCumulativeKwh);
                }

                feedInCreditCents += solar * fitRateCents;
            }

            supplyCostCents += dailySupplyChargeCents;
        }

        var sampleTotalCents = consumptionCostCents - feedInCreditCents + supplyCostCents;
        var dayCount = intervalData.DayCount;
        var dailyAverageCents = dayCount == 0 ? 0m : sampleTotalCents / dayCount;
        var annualEstimateCents = dailyAverageCents * DaysInYear;
        var annualEstimateDollars = Math.Round(annualEstimateCents / 100m, 2, MidpointRounding.AwayFromZero);
        var dailyAverageDollars = Math.Round(dailyAverageCents / 100m, 2, MidpointRounding.AwayFromZero);

        return new PlanCostResult(
            plan.PlanId,
            plan.PlanName,
            plan.RetailerName,
            plan.TariffType,
            annualEstimateDollars,
            dailyAverageDollars,
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

    /// <summary>
    /// Matches a usage date to the tariff period whose start/end date range covers it, by
    /// month-day (ignoring year) so recurring seasonal periods (e.g. Summer Oct-Apr, Winter May-Sep)
    /// are applied correctly regardless of which year the usage data falls in. Returns null when
    /// there is only one period, or when no period's dates cover the given date (caller should fall
    /// back to <see cref="SelectTariffPeriod"/> in that case).
    /// </summary>
    private static TariffPeriod? SelectTariffPeriodForDate(List<TariffPeriod> tariffPeriods, DateOnly date)
    {
        if (tariffPeriods.Count <= 1)
        {
            return null;
        }

        foreach (var tariffPeriod in tariffPeriods)
        {
            var start = ParseDate(tariffPeriod.StartDate);
            var end = ParseDate(tariffPeriod.EndDate);
            if (start is null || end is null)
            {
                continue;
            }

            if (IsDateInSeasonalRange(date, start.Value, end.Value))
            {
                return tariffPeriod;
            }
        }

        return null;
    }

    private static bool IsDateInSeasonalRange(DateOnly date, DateOnly start, DateOnly end)
    {
        var dateMonthDay = (date.Month * 100) + date.Day;
        var startMonthDay = (start.Month * 100) + start.Day;
        var endMonthDay = (end.Month * 100) + end.Day;

        return startMonthDay <= endMonthDay
            ? dateMonthDay >= startMonthDay && dateMonthDay <= endMonthDay
            : dateMonthDay >= startMonthDay || dateMonthDay <= endMonthDay; // range wraps across year-end
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

        return ResolveTieredCostCents(rates, usageKwh, ref dayCumulativeKwh);
    }

    /// <summary>
    /// Applies volume-tiered block rates (e.g. "first 24kWh free, then 17.9c/kWh") against cumulative
    /// usage tracked by the caller. Shared by single-rate plans (one running total per day) and TOU
    /// plans (one running total per TOU block per day, since each TOU window can carry its own tiers).
    /// </summary>
    private static decimal ResolveTieredCostCents(List<BlockRate> rates, decimal usageKwh, ref decimal cumulativeKwh)
    {
        var remaining = usageKwh;
        var cost = 0m;
        var running = cumulativeKwh;

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

        cumulativeKwh = running;
        return cost;
    }

    private static decimal ResolveTouCostCents(
        TariffPeriod tariffPeriod,
        DateOnly date,
        int slot,
        decimal usageKwh,
        Dictionary<string, decimal> touBlockCumulativeKwh)
    {
        var touBlocks = tariffPeriod.TouBlock;
        if (touBlocks is null || touBlocks.Count == 0)
        {
            return usageKwh * (tariffPeriod.BlockRate?.FirstOrDefault()?.UnitPrice ?? 0m);
        }

        var dayCode = DayMap[date.DayOfWeek];
        var slotMinutes = slot * 5;
        var hours = slotMinutes / 60;
        var minutes = slotMinutes % 60;
        var slotTime = (hours * 100) + minutes;

        for (var i = 0; i < touBlocks.Count; i++)
        {
            var block = touBlocks[i];
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

                // Some retailers express overnight windows as e.g. 1500-0059, i.e. start > end,
                // meaning the window wraps past midnight. Treat that as "at or after start, OR at
                // or before end" instead of the impossible "between start and end".
                var matches = start <= end
                    ? slotTime >= start && slotTime <= end
                    : slotTime >= start || slotTime <= end;

                if (matches)
                {
                    return ResolveTouBlockCostCents(block, i, usageKwh, touBlockCumulativeKwh);
                }
            }
        }

        return ResolveTouBlockCostCents(touBlocks[0], 0, usageKwh, touBlockCumulativeKwh);
    }

    private static decimal ResolveTouBlockCostCents(
        TouBlock block,
        int blockIndex,
        decimal usageKwh,
        Dictionary<string, decimal> touBlockCumulativeKwh)
    {
        var rates = block.BlockRate;
        if (rates is null || rates.Count == 0 || usageKwh <= 0m)
        {
            return 0m;
        }

        var blockKey = block.Name ?? $"block-{blockIndex}";
        touBlockCumulativeKwh.TryGetValue(blockKey, out var cumulative);
        var cost = ResolveTieredCostCents(rates, usageKwh, ref cumulative);
        touBlockCumulativeKwh[blockKey] = cumulative;
        return cost;
    }
}
