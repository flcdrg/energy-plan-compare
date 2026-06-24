using EnergyPlanCompare.Models;

namespace EnergyPlanCompare.Services;

public sealed class PlanRanker
{
    public void Print(IReadOnlyList<PlanCostResult> results)
    {
        Console.WriteLine("Rank Plan ID         Retailer                     Type  Total($)  $/day  Days");
        for (var i = 0; i < results.Count; i++)
        {
            var item = results[i];
            Console.WriteLine(
                $"{i + 1,4} {item.PlanId,-15} {Trim(item.RetailerName, 28),-28} {item.TariffType,-4} {item.TotalCostDollars,8:F2} {item.DailyAverageDollars,6:F2} {item.DayCount,5}");
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
}

