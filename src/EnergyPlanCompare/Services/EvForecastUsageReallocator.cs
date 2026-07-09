using EnergyPlanCompare.Models;

namespace EnergyPlanCompare.Services;

public sealed class EvForecastUsageReallocator
{
    private const int SlotsPerDay = 288;

    public IntervalData Reallocate(IntervalData intervalData, EvForecastSettings settings)
    {
        var inWindowSlots = BuildInWindowSlotMask(settings.StartHour, settings.EndHour);
        var inWindowCount = inWindowSlots.Count(x => x);
        var outWindowCount = SlotsPerDay - inWindowCount;

        var remappedConsumption = new Dictionary<DateOnly, decimal[]>(intervalData.ConsumptionByDate.Count);
        foreach (var (date, originalDay) in intervalData.ConsumptionByDate)
        {
            remappedConsumption[date] = ReallocateDay(originalDay, inWindowSlots, inWindowCount, outWindowCount, settings.WindowPercentage);
        }

        return new IntervalData(intervalData.SolarByDate, remappedConsumption);
    }

    private static decimal[] ReallocateDay(
        decimal[] dayValues,
        IReadOnlyList<bool> inWindowSlots,
        int inWindowCount,
        int outWindowCount,
        decimal windowPercentage)
    {
        if (dayValues.Length != SlotsPerDay)
        {
            throw new InvalidDataException($"Expected {SlotsPerDay} slots, got {dayValues.Length}.");
        }

        var cleaned = new decimal[SlotsPerDay];
        for (var slot = 0; slot < SlotsPerDay; slot++)
        {
            cleaned[slot] = Math.Max(0m, dayValues[slot]);
        }

        var dayTotal = cleaned.Sum();
        if (dayTotal <= 0m)
        {
            return cleaned;
        }

        var targetWindow = dayTotal * (windowPercentage / 100m);
        var targetNonWindow = dayTotal - targetWindow;

        var sourceWindowTotal = 0m;
        var sourceNonWindowTotal = 0m;
        for (var slot = 0; slot < SlotsPerDay; slot++)
        {
            if (inWindowSlots[slot])
            {
                sourceWindowTotal += cleaned[slot];
            }
            else
            {
                sourceNonWindowTotal += cleaned[slot];
            }
        }

        var remapped = new decimal[SlotsPerDay];
        ApplyAllocation(remapped, cleaned, inWindowSlots, isWindow: true, targetWindow, sourceWindowTotal, inWindowCount);
        ApplyAllocation(remapped, cleaned, inWindowSlots, isWindow: false, targetNonWindow, sourceNonWindowTotal, outWindowCount);
        return remapped;
    }

    private static void ApplyAllocation(
        decimal[] target,
        decimal[] source,
        IReadOnlyList<bool> inWindowSlots,
        bool isWindow,
        decimal targetTotal,
        decimal sourceTotal,
        int slotCount)
    {
        if (slotCount == 0 || targetTotal <= 0m)
        {
            return;
        }

        if (sourceTotal > 0m)
        {
            for (var slot = 0; slot < SlotsPerDay; slot++)
            {
                if (inWindowSlots[slot] == isWindow)
                {
                    target[slot] = targetTotal * (source[slot] / sourceTotal);
                }
            }

            return;
        }

        var perSlot = targetTotal / slotCount;
        for (var slot = 0; slot < SlotsPerDay; slot++)
        {
            if (inWindowSlots[slot] == isWindow)
            {
                target[slot] = perSlot;
            }
        }
    }

    private static bool[] BuildInWindowSlotMask(int startHour, int endHour)
    {
        var result = new bool[SlotsPerDay];

        for (var slot = 0; slot < SlotsPerDay; slot++)
        {
            var hour = slot / 12;
            result[slot] = startHour < endHour
                ? hour >= startHour && hour < endHour
                : hour >= startHour || hour < endHour;
        }

        return result;
    }
}