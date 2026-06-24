namespace EnergyPlanCompare.Models;

public sealed record PlanCostResult(
    string PlanId,
    string PlanName,
    string RetailerName,
    string TariffType,
    decimal TotalCostDollars,
    decimal DailyAverageDollars,
    int DayCount,
    IReadOnlyList<string> Notes);

