namespace EnergyPlanCompare.Models;

public sealed record IntervalData(
    IReadOnlyDictionary<DateOnly, decimal[]> SolarByDate,
    IReadOnlyDictionary<DateOnly, decimal[]> ConsumptionByDate)
{
    public int DayCount => ConsumptionByDate.Count;
}

public sealed record IntervalReading(DateOnly Date, int Slot, decimal SolarKwh, decimal ConsumptionKwh);

