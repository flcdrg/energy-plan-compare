using EnergyPlanCompare.Models;

namespace EnergyPlanCompare.Services;

public sealed class PlanFilter
{
    public List<PlanData> FilterControlledLoad(IEnumerable<PlanData> plans, bool includeControlledLoad)
    {
        if (includeControlledLoad)
        {
            return plans.ToList();
        }

        return plans.Where(plan => !IsControlledLoad(plan)).ToList();
    }

    public bool IsControlledLoad(PlanData plan)
    {
        if ((plan.TariffType ?? string.Empty).Contains("CL", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var contract = plan.Contract.FirstOrDefault();
        var pricingModel = contract?.PricingModel ?? string.Empty;
        return pricingModel.Contains("CL", StringComparison.OrdinalIgnoreCase);
    }
}
