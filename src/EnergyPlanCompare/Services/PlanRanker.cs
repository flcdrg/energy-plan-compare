using EnergyPlanCompare.Models;
using Spectre.Console;

namespace EnergyPlanCompare.Services;

public sealed class PlanRanker
{
    public void Print(IReadOnlyList<PlanCostResult> results, int top, bool showUrls, string postcode, string? currentPlanId = null)
    {
        var take = top > 0 ? Math.Min(top, results.Count) : results.Count;
        var table = new Table().RoundedBorder();
        table.AddColumn("Rank");
        table.AddColumn("Plan ID");
        table.AddColumn("Retailer");
        table.AddColumn("Type");
        table.AddColumn("Est. Annual ($)");
        table.AddColumn("$/day");
        table.AddColumn("Days");
        if (showUrls)
        {
            table.AddColumn("URL");
        }

        var currentIndex = currentPlanId is not null
            ? results.ToList().FindIndex(r => r.PlanId.Equals(currentPlanId, StringComparison.OrdinalIgnoreCase))
            : -1;

        // If the current plan falls outside the top N, show it first as a reference row.
        if (currentPlanId is not null && currentIndex >= take)
        {
            table.AddRow(BuildRow("current", results[currentIndex], showUrls, postcode, grey: true));
        }
        else if (currentPlanId is not null && currentIndex < 0)
        {
            var notFoundRow = new[] { "[grey]current[/]", $"[grey]{Markup.Escape(currentPlanId)}[/]",
                "[grey](not in eligible results)[/]", "", "", "", "" };
            if (showUrls) notFoundRow = [.. notFoundRow, $"[grey]{Markup.Escape(BuildPlanUrl(currentPlanId, postcode))}[/]"];
            table.AddRow(notFoundRow);
        }

        // Render the top N ranked results; highlight the current plan at its natural position.
        for (var i = 0; i < take; i++)
        {
            var item = results[i];
            var isCurrent = currentIndex == i;
            table.AddRow(BuildRow((i + 1).ToString(), item, showUrls, postcode, grey: isCurrent));
        }

        AnsiConsole.Write(table);
        if (take < results.Count)
        {
            AnsiConsole.MarkupLine($"[grey]Showing top {take} of {results.Count} results. Use --top to change.[/]");
        }
    }

    private static string[] BuildRow(string rank, PlanCostResult item, bool showUrls, string postcode, bool grey)
    {
        string W(string v) => grey ? $"[grey]{Markup.Escape(v)}[/]" : Markup.Escape(v);

        var row = new List<string>
        {
            W(rank),
            W(item.PlanId),
            W(Trim(item.RetailerName, 28)),
            W(item.TariffType),
            W(item.TotalCostDollars.ToString("F2")),
            W(item.DailyAverageDollars.ToString("F2")),
            W(item.DayCount.ToString())
        };
        if (showUrls)
        {
            row.Add(W(BuildPlanUrl(item.PlanId, postcode)));
        }

        return [.. row];
    }

    private static string Trim(string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        return value.Length <= max ? value : value[..(max - 1)] + "…";
    }

    private static string BuildPlanUrl(string planId, string postcode) =>
        $"https://www.energymadeeasy.gov.au/plan?id={Uri.EscapeDataString(planId)}&postcode={Uri.EscapeDataString(postcode)}";
}
