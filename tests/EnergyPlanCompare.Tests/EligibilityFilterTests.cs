using EnergyPlanCompare.Models;
using EnergyPlanCompare.Services;

namespace EnergyPlanCompare.Tests;

public class EligibilityFilterTests
{
    [Fact]
    public void IsEligible_RejectsSmartMeterPlanWhenNotSet()
    {
        var plan = PlanWithRestriction(new EligibilityRestriction("SM", "Smart meter required", null));
        var filter = new EligibilityFilter();

        var eligible = filter.IsEligible(plan, new EligibilityRequirements(false, false, false, false), out _);

        Assert.False(eligible);
    }

    [Fact]
    public void IsEligible_RejectsNonPensionerPlanForPensioner()
    {
        var plan = PlanWithRestriction(new EligibilityRestriction("OC", "Must be non-pensioner", "Additional Eligibility"));
        var filter = new EligibilityFilter();

        var eligible = filter.IsEligible(plan, new EligibilityRequirements(true, false, false, true), out _);

        Assert.False(eligible);
    }

    [Fact]
    public void IsEligible_AllowsPlanWithoutRestrictions()
    {
        var plan = new PlanData("P0", "Plan", "Retailer", "SR", null, null, [new Contract("SR", null, null, null)]);
        var filter = new EligibilityFilter();

        var eligible = filter.IsEligible(plan, new EligibilityRequirements(false, false, false, false), out _);

        Assert.True(eligible);
    }

    private static PlanData PlanWithRestriction(EligibilityRestriction restriction) =>
        new("P1", "Restricted", "Retailer", "SR", null, null, [new Contract("SR", null, null, [restriction])]);
}
