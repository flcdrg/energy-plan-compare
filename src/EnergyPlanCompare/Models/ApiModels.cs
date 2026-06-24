using System.Text.Json.Serialization;

namespace EnergyPlanCompare.Models;

public sealed record PlanListResponse(
    [property: JsonPropertyName("data")] PlanListData Data);

public sealed record PlanListData(
    [property: JsonPropertyName("plans")] List<PlanListItem> Plans);

public sealed record PlanListItem(
    [property: JsonPropertyName("planId")] string PlanId,
    [property: JsonPropertyName("planData")] PlanData PlanData);

public sealed record PlanDetailResponse(
    [property: JsonPropertyName("data")] PlanDetailData Data);

public sealed record PlanDetailData(
    [property: JsonPropertyName("planId")] string PlanId,
    [property: JsonPropertyName("planData")] PlanData PlanData);

public sealed record PlanData(
    [property: JsonPropertyName("planId")] string PlanId,
    [property: JsonPropertyName("planName")] string PlanName,
    [property: JsonPropertyName("retailerName")] string RetailerName,
    [property: JsonPropertyName("tariffType")] string TariffType,
    [property: JsonPropertyName("contract")] List<Contract> Contract);

public sealed record Contract(
    [property: JsonPropertyName("pricingModel")] string? PricingModel,
    [property: JsonPropertyName("tariffPeriod")] List<TariffPeriod>? TariffPeriod,
    [property: JsonPropertyName("solarFit")] List<SolarFit>? SolarFit,
    [property: JsonPropertyName("eligibilityRestriction")] List<EligibilityRestriction>? EligibilityRestriction);

public sealed record TariffPeriod(
    [property: JsonPropertyName("blockRate")] List<BlockRate>? BlockRate,
    [property: JsonPropertyName("touBlock")] List<TouBlock>? TouBlock,
    [property: JsonPropertyName("dailySupplyCharge")] decimal? DailySupplyCharge,
    [property: JsonPropertyName("startDate")] string? StartDate,
    [property: JsonPropertyName("endDate")] string? EndDate,
    [property: JsonPropertyName("blockPeriod")] string? BlockPeriod);

public sealed record BlockRate(
    [property: JsonPropertyName("unitPrice")] decimal UnitPrice,
    [property: JsonPropertyName("volume")] decimal? Volume,
    [property: JsonPropertyName("measureUnit")] string? MeasureUnit);

public sealed record TouBlock(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("timeOfUsePeriod")] string? TimeOfUsePeriod,
    [property: JsonPropertyName("blockRate")] List<BlockRate>? BlockRate,
    [property: JsonPropertyName("timeOfUse")] List<TouTimeOfUse>? TimeOfUse);

public sealed record TouTimeOfUse(
    [property: JsonPropertyName("days")] string? Days,
    [property: JsonPropertyName("startTime")] string StartTime,
    [property: JsonPropertyName("endTime")] string EndTime);

public sealed record SolarFit(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("singleTariffRates")] List<SolarFitRate>? SingleTariffRates);

public sealed record SolarFitRate(
    [property: JsonPropertyName("unitPrice")] decimal UnitPrice,
    [property: JsonPropertyName("volume")] decimal? Volume);

public sealed record EligibilityRestriction(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("information")] string? Information);

public sealed record StoredPlans(
    [property: JsonPropertyName("fetchedAtUtc")] DateTime FetchedAtUtc,
    [property: JsonPropertyName("postcode")] string Postcode,
    [property: JsonPropertyName("plans")] List<PlanData> Plans);
