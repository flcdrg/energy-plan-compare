using EnergyPlanCompare.Models;
using EnergyPlanCompare.Services;

namespace EnergyPlanCompare.Tests;

public class PlanFilterTests
{
    [Fact]
    public void FilterControlledLoad_WhenDisabled_ExcludesControlledLoadPlans()
    {
        var filter = new PlanFilter();
        var plans = new List<PlanData>
        {
            BuildPlan("P1", "SR", "SR"),
            BuildPlan("P2", "TOUCL", "TOUCL")
        };

        var result = filter.FilterControlledLoad(plans, includeControlledLoad: false);

        var only = Assert.Single(result);
        Assert.Equal("P1", only.PlanId);
    }

    [Fact]
    public void FilterControlledLoad_WhenEnabled_ReturnsAllPlans()
    {
        var filter = new PlanFilter();
        var plans = new List<PlanData>
        {
            BuildPlan("P1", "SR", "SR"),
            BuildPlan("P2", "TOUCL", "TOUCL"),
            BuildPlan("P3", "TOU", "SRCL")
        };

        var result = filter.FilterControlledLoad(plans, includeControlledLoad: true);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, p => p.PlanId == "P1");
        Assert.Contains(result, p => p.PlanId == "P2");
        Assert.Contains(result, p => p.PlanId == "P3");
    }

    private static PlanData BuildPlan(string planId, string tariffType, string pricingModel) =>
        new(
            planId,
            $"Plan {planId}",
            "Retailer",
            tariffType,
            null,
            null,
            [new Contract(pricingModel, null, null, null)]);
}
