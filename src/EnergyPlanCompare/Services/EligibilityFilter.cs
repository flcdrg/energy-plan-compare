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
            var descUpper = (restriction.Description ?? string.Empty).Trim().ToUpperInvariant();

            switch (type)
            {
                case "SM":
                    // "will install" means the retailer installs a smart meter for you — not a requirement
                    if (!descUpper.Contains("WILL INSTALL") && !requirements.HasSmartMeter)
                    {
                        notes.Add("Requires smart meter");
                        return false;
                    }
                    break;

                case "CB":
                    // CB means the plan is designed for customers with a home battery.
                    // The description may say "estimate based on..." (pricing methodology) or
                    // "you must have a battery" (explicit requirement), but either way the plan
                    // requires battery hardware — it is never shown to customers without one.
                    if (!requirements.HasBattery)
                    {
                        notes.Add("Requires battery");
                        return false;
                    }
                    break;

                case "SP":
                    // SP is used both for genuine solar requirements ("only available to solar customers")
                    // and pricing methodology notes ("estimate based on typical solar system"). Only filter
                    // if the description contains explicit requirement language.
                    if (IsHardwareRequirement(descUpper) && !requirements.HasSolar)
                    {
                        notes.Add("Requires solar panels");
                        return false;
                    }
                    break;

                case "OC":
                    // Free-text "other conditions". Only filter for unambiguous exclusions.
                    if (descUpper.Contains("NON-PENSIONER") && requirements.IsPensioner)
                    {
                        notes.Add("Excludes pensioners");
                        return false;
                    }
                    // Plans that are explicitly for households WITHOUT solar or battery
                    // (e.g. Amber "No Feed-In" plans: "pricing estimate is for households
                    // without a solar or battery system")
                    if (descUpper.Contains("WITHOUT") && descUpper.Contains("SOLAR") && requirements.HasSolar)
                    {
                        notes.Add("For households without solar");
                        return false;
                    }
                    if (descUpper.Contains("WITHOUT") && descUpper.Contains("BATTERY") && requirements.HasBattery)
                    {
                        notes.Add("For households without battery");
                        return false;
                    }
                    break;
            }
        }

        return true;
    }

    // Detects language that indicates a genuine eligibility requirement rather than a pricing note.
    private static bool IsHardwareRequirement(string descUpper) =>
        descUpper.Contains("YOU MUST") ||
        descUpper.Contains("MUST HAVE") ||
        descUpper.Contains("MUST BE") ||
        descUpper.Contains("ONLY AVAILABLE TO");
}

