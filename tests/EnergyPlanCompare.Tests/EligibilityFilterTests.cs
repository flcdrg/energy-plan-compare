using EnergyPlanCompare.Models;
using EnergyPlanCompare.Services;

namespace EnergyPlanCompare.Tests;

public class EligibilityFilterTests
{
    [Fact]
    public void IsEligible_RejectsSmartMeterPlanWhenNotSet()
    {
        var plan = PlanWithRestriction(new EligibilityRestriction("SM", "You must have a remotely read interval meter (smart meter).", null));
        var filter = new EligibilityFilter();

        var eligible = filter.IsEligible(plan, new EligibilityRequirements(false, false, false, false), out _);

        Assert.False(eligible);
    }

    [Fact]
    public void IsEligible_AllowsSmartMeterPlanWhenRetailerWillInstall()
    {
        // "will install" means it's not a requirement — the retailer installs one for you
        var plan = PlanWithRestriction(new EligibilityRestriction("SM", "If you do not have a smart meter we will install one for you", null));
        var filter = new EligibilityFilter();

        var eligible = filter.IsEligible(plan, new EligibilityRequirements(false, false, false, false), out _);

        Assert.True(eligible);
    }

    [Fact]
    public void IsEligible_RejectsCbPlanWhenNoBattery()
    {
        // CB type means the plan is designed for battery owners — always requires --battery,
        // even when the description only describes the pricing methodology ("estimate based on...")
        var plan = PlanWithRestriction(new EligibilityRestriction("CB", "Usage price estimate based on a typical household battery system of 12.5kWh.", null));
        var filter = new EligibilityFilter();

        var eligible = filter.IsEligible(plan, new EligibilityRequirements(false, false, false, false), out _);

        Assert.False(eligible);
    }

    [Fact]
    public void IsEligible_AllowsCbPlanWithBattery()
    {
        var plan = PlanWithRestriction(new EligibilityRestriction("CB", "You must have an eligible battery installed at your premises.", null));
        var filter = new EligibilityFilter();

        var eligible = filter.IsEligible(plan, new EligibilityRequirements(false, false, true, false), out _);

        Assert.True(eligible);
    }

    [Fact]
    public void IsEligible_RejectsSolarPlanWhenSpRequirementAndNoSolar()
    {
        var plan = PlanWithRestriction(new EligibilityRestriction("SP", "Solar Max is only available to eligible residential solar customers.", null));
        var filter = new EligibilityFilter();

        var eligible = filter.IsEligible(plan, new EligibilityRequirements(false, false, false, false), out _);

        Assert.False(eligible);
    }

    [Fact]
    public void IsEligible_AllowsSolarPricingNotePlanWithoutSolar()
    {
        // SP with "estimate" description is a pricing methodology note, not a hardware requirement
        var plan = PlanWithRestriction(new EligibilityRestriction("SP", "Usage price estimate based on a typical household solar system of 5kW.", null));
        var filter = new EligibilityFilter();

        var eligible = filter.IsEligible(plan, new EligibilityRequirements(false, false, false, false), out _);

        Assert.True(eligible);
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
    public void IsEligible_AllowsBatteryMentionedInOcDescription()
    {
        // OC descriptions sometimes mention "battery" as part of a plan name or T&C reference,
        // not as a hardware requirement — these should not be filtered out
        var plan = PlanWithRestriction(new EligibilityRestriction("OC",
            "To be eligible you must accept the Battery Maximiser Terms and Conditions.", null));
        var filter = new EligibilityFilter();

        var eligible = filter.IsEligible(plan, new EligibilityRequirements(false, false, false, false), out _);

        Assert.True(eligible);
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
