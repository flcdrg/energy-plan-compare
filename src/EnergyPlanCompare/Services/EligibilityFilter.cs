using EnergyPlanCompare.Models;

namespace EnergyPlanCompare.Services;

public sealed class EligibilityFilter
{
    public bool IsEligible(PlanData plan, EligibilityRequirements requirements, out List<string> notes)
    {
        notes = [];
        var restrictions = plan.Contract.FirstOrDefault()?.EligibilityRestriction;
        if (restrictions is null || restrictions.Count == 0)
        {
            return true;
        }

        foreach (var restriction in restrictions)
        {
            var type = (restriction.Type ?? string.Empty).Trim().ToUpperInvariant();
            var description = (restriction.Description ?? string.Empty).Trim();
            var combined = $"{type} {description}".ToUpperInvariant();

            if (type == "SM" && !requirements.HasSmartMeter)
            {
                notes.Add("Requires smart meter");
                return false;
            }

            if ((combined.Contains("EV") || combined.Contains("ELECTRIC VEHICLE")) && !requirements.HasEv)
            {
                notes.Add("Requires EV");
                return false;
            }

            if (combined.Contains("BATTERY") && !requirements.HasBattery)
            {
                notes.Add("Requires battery");
                return false;
            }

            if (combined.Contains("NON-PENSIONER") && requirements.IsPensioner)
            {
                notes.Add("Excludes pensioners");
                return false;
            }
        }

        return true;
    }
}

