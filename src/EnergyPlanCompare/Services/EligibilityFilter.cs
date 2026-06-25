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
                    // CB is used both for genuine battery requirements ("you must have a battery") and as
                    // pricing methodology notes ("estimate based on typical battery system"). Only filter
                    // if the description contains explicit requirement language.
                    if (IsHardwareRequirement(descUpper) && !requirements.HasBattery)
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
                    // Free-text "other conditions". Only filter for clear pensioner exclusion.
                    // Do not keyword-scan for EV/battery — OC descriptions often mention these as pricing
                    // methodology notes or in plan names (e.g. "Battery Maximiser Terms"), not as hardware
                    // requirements.
                    if (descUpper.Contains("NON-PENSIONER") && requirements.IsPensioner)
                    {
                        notes.Add("Excludes pensioners");
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

