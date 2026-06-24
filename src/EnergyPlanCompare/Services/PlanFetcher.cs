using System.Net.Http.Json;
using System.Text.Json;
using System.Globalization;
using EnergyPlanCompare.Models;

namespace EnergyPlanCompare.Services;

public sealed class PlanFetcher
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public PlanFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<StoredPlans> FetchPlansAsync(
        string listUrl,
        string postcode,
        bool fetchAll,
        bool currentOnly,
        int concurrency,
        Action<int, int>? onProgress,
        CancellationToken cancellationToken)
    {
        var listResponse = await _httpClient.GetFromJsonAsync<PlanListResponse>(listUrl, _jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Unable to deserialize plans list response.");

        var baseUri = new Uri("https://api.energymadeeasy.gov.au/");
        var semaphore = new SemaphoreSlim(Math.Max(1, concurrency));
        var tasks = new List<Task<PlanData>>(listResponse.Data.Plans.Count);
        var total = listResponse.Data.Plans.Count;
        var completed = 0;

        foreach (var listItem in listResponse.Data.Plans)
        {
            var mustFetchDetail = fetchAll || currentOnly || listItem.PlanData.TariffType.StartsWith("TOU", StringComparison.OrdinalIgnoreCase);
            if (!mustFetchDetail)
            {
                tasks.Add(Task.FromResult(listItem.PlanData));
                var done = Interlocked.Increment(ref completed);
                onProgress?.Invoke(done, total);
                continue;
            }

            tasks.Add(FetchPlanDetailWithGateAsync(listItem.PlanId, postcode, semaphore, baseUri, () =>
            {
                var done = Interlocked.Increment(ref completed);
                onProgress?.Invoke(done, total);
            }, cancellationToken));
        }

        var plans = (await Task.WhenAll(tasks)).ToList();
        if (currentOnly)
        {
            plans = plans.Where(IsCurrentlyAvailable).ToList();
        }

        return new StoredPlans(DateTime.UtcNow, postcode, plans);
    }

    private async Task<PlanData> FetchPlanDetailWithGateAsync(
        string planId,
        string postcode,
        SemaphoreSlim semaphore,
        Uri baseUri,
        Action onCompleted,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var detailPath = $"consumerplan/plan/{Uri.EscapeDataString(planId)}?postcode={Uri.EscapeDataString(postcode)}&withPrices=true";
            var detailUri = new Uri(baseUri, detailPath);
            var detail = await _httpClient.GetFromJsonAsync<PlanDetailResponse>(detailUri, _jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException($"Unable to deserialize plan detail for {planId}.");

            return detail.Data.PlanData;
        }
        finally
        {
            semaphore.Release();
            onCompleted();
        }
    }

    private static bool IsCurrentlyAvailable(PlanData plan)
    {
        if (!string.IsNullOrWhiteSpace(plan.PlanStatus) &&
            !plan.PlanStatus.Equals("PUBLISHED", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var effectiveDate = ParseDate(plan.EffectiveDate);
        if (effectiveDate is not null && effectiveDate > today)
        {
            return false;
        }

        var periods = plan.Contract
            .SelectMany(c => c.TariffPeriod ?? [])
            .ToList();
        if (periods.Count == 0)
        {
            return true;
        }

        foreach (var period in periods)
        {
            var start = ParseDate(period.StartDate);
            var end = ParseDate(period.EndDate);
            var startsOk = start is null || start <= today;
            var endsOk = end is null || end >= today;
            if (startsOk && endsOk)
            {
                return true;
            }
        }

        return false;
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }
}
