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

    [Fact]
    public void FilterDemandPlans_ExcludesPlansWithDemandCharge()
    {
        var filter = new PlanFilter();
        var plans = new List<PlanData>
        {
            BuildPlan("P1", "SR", "SR", hasDemandCharge: false),
            BuildPlan("P2", "SR", "SR", hasDemandCharge: true),
            BuildPlan("P3", "TOU", "TOU", hasDemandCharge: false)
        };

        var result = filter.FilterDemandPlans(plans);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.PlanId == "P1");
        Assert.Contains(result, p => p.PlanId == "P3");
    }

    [Fact]
    public void HasDemandCharge_ReturnsTrueWhenDemandChargePresent()
    {
        var filter = new PlanFilter();
        var plan = BuildPlan("P1", "SR", "SR", hasDemandCharge: true);

        Assert.True(filter.HasDemandCharge(plan));
    }

    [Fact]
    public void HasDemandCharge_ReturnsFalseWhenNoDemandCharge()
    {
        var filter = new PlanFilter();
        var plan = BuildPlan("P1", "SR", "SR", hasDemandCharge: false);

        Assert.False(filter.HasDemandCharge(plan));
    }

    private static PlanData BuildPlan(string planId, string tariffType, string pricingModel, bool hasDemandCharge = false)
    {
        var demandCharges = hasDemandCharge
            ? new List<DemandCharge> { new(42.0m, "KWH") }
            : null;
        var tariffPeriod = new List<TariffPeriod>
        {
            new(BlockRate: [new(22m, null, "KWH")], TouBlock: null,
                DailySupplyCharge: 95m, StartDate: null, EndDate: null,
                BlockPeriod: null, DemandCharge: demandCharges)
        };
        return new(
            planId,
            $"Plan {planId}",
            "Retailer",
            tariffType,
            null,
            null,
            [new Contract(pricingModel, tariffPeriod, null, null)]);
    }
}
