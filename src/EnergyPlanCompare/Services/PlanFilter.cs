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

    public List<PlanData> FilterDemandPlans(IEnumerable<PlanData> plans)
    {
        return plans.Where(plan => !HasDemandCharge(plan)).ToList();
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

    public bool HasDemandCharge(PlanData plan) =>
        plan.Contract
            .SelectMany(c => c.TariffPeriod ?? [])
            .Any(tp => tp.DemandCharge is { Count: > 0 });
}
