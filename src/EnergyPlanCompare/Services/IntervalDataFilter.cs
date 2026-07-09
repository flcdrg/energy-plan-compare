using EnergyPlanCompare.Models;

namespace EnergyPlanCompare.Services;

public sealed class IntervalDataFilter
{
    public IntervalData ForDate(IntervalData intervalData, DateOnly date)
    {
        if (!intervalData.ConsumptionByDate.TryGetValue(date, out var consumption))
        {
            throw new InvalidOperationException($"No consumption interval data found for --typical-day {date:yyyy-MM-dd}.");
        }

        var filteredConsumption = new Dictionary<DateOnly, decimal[]> { [date] = consumption };
        var filteredSolar = new Dictionary<DateOnly, decimal[]>();

        if (intervalData.SolarByDate.TryGetValue(date, out var solar))
        {
            filteredSolar[date] = solar;
        }

        return new IntervalData(filteredSolar, filteredConsumption);
    }
}