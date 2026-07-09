namespace EnergyPlanCompare.Models;

public sealed record EvForecastSettings(
    int StartHour,
    int EndHour,
    decimal WindowPercentage);