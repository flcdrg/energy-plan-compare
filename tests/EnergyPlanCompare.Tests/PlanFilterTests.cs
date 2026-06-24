using EnergyPlanCompare.Models;
using EnergyPlanCompare.Services;

namespace EnergyPlanCompare.Tests;

public class PlanFilterTests
{
    [Fact]
    public void FilterControlledLoad_WhenDisabled_ReturnsAllPlans()
    {
        var filter = new PlanFilter();
        var plans = new List<PlanData>
        {
            BuildPlan("P1", "SR", "SR"),
            BuildPlan("P2", "TOUCL", "TOUCL")
        };

        var result = filter.FilterControlledLoad(plans, controlledLoadOnly: false);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FilterControlledLoad_WhenEnabled_ReturnsOnlyControlledLoadPlans()
    {
        var filter = new PlanFilter();
        var plans = new List<PlanData>
        {
            BuildPlan("P1", "SR", "SR"),
            BuildPlan("P2", "TOUCL", "TOUCL"),
            BuildPlan("P3", "TOU", "SRCL")
        };

        var result = filter.FilterControlledLoad(plans, controlledLoadOnly: true);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.PlanId == "P2");
        Assert.Contains(result, p => p.PlanId == "P3");
    }

    private static PlanData BuildPlan(string planId, string tariffType, string pricingModel) =>
        new(
            planId,
            $"Plan {planId}",
            "Retailer",
            tariffType,
            [new Contract(pricingModel, null, null, null)]);
}

