using EnergyPlanCompare.Models;
using Spectre.Console;

namespace EnergyPlanCompare.Services;

public sealed class PlanRanker
{
    public void Print(IReadOnlyList<PlanCostResult> results, int top, bool showUrls, string postcode)
    {
        var take = top > 0 ? Math.Min(top, results.Count) : results.Count;
        var table = new Table().RoundedBorder();
        table.AddColumn("Rank");
        table.AddColumn("Plan ID");
        table.AddColumn("Retailer");
        table.AddColumn("Type");
        table.AddColumn("Total ($)");
        table.AddColumn("$/day");
        table.AddColumn("Days");
        if (showUrls)
        {
            table.AddColumn("URL");
        }

        for (var i = 0; i < take; i++)
        {
            var item = results[i];
            var row = new List<string>
            {
                (i + 1).ToString(),
                item.PlanId,
                Trim(item.RetailerName, 28),
                item.TariffType,
                item.TotalCostDollars.ToString("F2"),
                item.DailyAverageDollars.ToString("F2"),
                item.DayCount.ToString()
            };
            if (showUrls)
            {
                row.Add(BuildPlanUrl(item.PlanId, postcode));
            }

            table.AddRow(row.ToArray());
        }

        AnsiConsole.Write(table);
        if (take < results.Count)
        {
            AnsiConsole.MarkupLine($"[grey]Showing top {take} of {results.Count} results. Use --top to change.[/]");
        }
    }

    private static string Trim(string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= max ? value : value[..(max - 1)] + "…";
    }

    private static string BuildPlanUrl(string planId, string postcode) =>
        $"https://www.energymadeeasy.gov.au/plan?id={Uri.EscapeDataString(planId)}&postcode={Uri.EscapeDataString(postcode)}";
}
