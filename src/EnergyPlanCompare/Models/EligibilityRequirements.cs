namespace EnergyPlanCompare.Models;

public sealed record EligibilityRequirements(
    bool HasSmartMeter,
    bool HasEv,
    bool HasBattery,
    bool IsPensioner);

