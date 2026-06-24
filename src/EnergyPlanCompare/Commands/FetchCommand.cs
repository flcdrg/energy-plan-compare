using System.CommandLine;
using System.Text.Json;
using EnergyPlanCompare.Services;

namespace EnergyPlanCompare.Commands;

public static class FetchCommand
{
    private const string DefaultUrl = "https://api.energymadeeasy.gov.au/consumerplan/plans?usageDataSource=noUsageFrontier&customerType=R&distE=&distG=&fuelType=E&journey=E&postcode=YOUR_POSTCODE";

    public static Command Build()
    {
        var urlOption = new Option<string>("--url")
        {
            Description = "Energy Made Easy list URL",
            DefaultValueFactory = _ => DefaultUrl
        };

        var outputOption = new Option<FileInfo>("--output")
        {
            Description = "Path to output plans JSON",
            DefaultValueFactory = _ => new FileInfo("plans.json")
        };

        var postcodeOption = new Option<string>("--postcode")
        {
            Description = "Postcode used for plan details",
            DefaultValueFactory = _ => "YOUR_POSTCODE"
        };

        var fetchAllOption = new Option<bool>("--fetch-all")
        {
            Description = "Fetch every plan by ID, including SR plans"
        };

        var concurrencyOption = new Option<int>("--concurrency")
        {
            Description = "Maximum concurrent plan-detail requests",
            DefaultValueFactory = _ => 5
        };

        var command = new Command("fetch", "Fetch plan data and store to JSON");
        command.Options.Add(urlOption);
        command.Options.Add(outputOption);
        command.Options.Add(postcodeOption);
        command.Options.Add(fetchAllOption);
        command.Options.Add(concurrencyOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var url = parseResult.GetValue(urlOption) ?? DefaultUrl;
            var output = parseResult.GetValue(outputOption) ?? new FileInfo("plans.json");
            var postcode = parseResult.GetValue(postcodeOption) ?? "YOUR_POSTCODE";
            var fetchAll = parseResult.GetValue(fetchAllOption);
            var concurrency = parseResult.GetValue(concurrencyOption);

            using var httpClient = new HttpClient();
            var fetcher = new PlanFetcher(httpClient);
            var plans = await fetcher.FetchPlansAsync(url, postcode, fetchAll, concurrency, cancellationToken);

            output.Directory?.Create();
            await using var stream = output.Open(FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, plans, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);

            var srCount = plans.Plans.Count(x => x.TariffType.Equals("SR", StringComparison.OrdinalIgnoreCase));
            var touCount = plans.Plans.Count(x => x.TariffType.Equals("TOU", StringComparison.OrdinalIgnoreCase));
            Console.WriteLine($"Fetched {plans.Plans.Count} plans (SR: {srCount}, TOU: {touCount}) -> {output.FullName}");
            return 0;
        });

        return command;
    }
}
