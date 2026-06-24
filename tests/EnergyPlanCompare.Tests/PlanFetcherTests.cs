using System.Net;
using System.Text;
using System.Text.Json;
using EnergyPlanCompare.Models;
using EnergyPlanCompare.Services;

namespace EnergyPlanCompare.Tests;

public class PlanFetcherTests
{
    [Fact]
    public void PlanDetailFixture_ContainsTouTimeWindows()
    {
        var json = File.ReadAllText(TestPaths.Fixture("plan-tou.json"));
        var response = JsonSerializer.Deserialize<PlanDetailResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(response);
        var contract = response!.Data.PlanData.Contract.First();
        var touPeriod = contract.TariffPeriod!.First(tp => tp.TouBlock is not null && tp.TouBlock.Count > 0);
        var block = touPeriod.TouBlock!.First();
        Assert.NotEmpty(block.TimeOfUse!);
    }

    [Fact]
    public async Task FetchPlansAsync_UsesListForSrAndDetailForTou()
    {
        var listJson = """
{
  "data": {
    "plans": [
      {
        "planId": "SR1",
        "planData": {
          "planId": "SR1",
          "planName": "Single",
          "retailerName": "Retailer",
          "tariffType": "SR",
          "contract": [
            {
              "pricingModel": "SR",
              "tariffPeriod": [{ "blockRate": [{ "unitPrice": 22 }], "dailySupplyCharge": 95 }]
            }
          ]
        }
      },
      {
        "planId": "TOU1",
        "planData": {
          "planId": "TOU1",
          "planName": "Tou",
          "retailerName": "Retailer",
          "tariffType": "TOU",
          "contract": [
            {
              "pricingModel": "TOU",
              "tariffPeriod": [{ "touBlock": [{ "blockRate": [{ "unitPrice": 44 }] }] }]
            }
          ]
        }
      }
    ]
  }
}
""";

        var detailJson = """
{
  "data": {
    "planId": "TOU1",
    "planData": {
      "planId": "TOU1",
      "planName": "Tou",
      "retailerName": "Retailer",
      "tariffType": "TOU",
      "contract": [
        {
          "pricingModel": "TOU",
          "tariffPeriod": [
            {
              "touBlock": [
                {
                  "blockRate": [{ "unitPrice": 44 }],
                  "timeOfUse": [{ "days": "MON|TUE|WED|THU|FRI|SAT|SUN", "startTime": "0600", "endTime": "0959" }]
                }
              ]
            }
          ]
        }
      ]
    }
  }
}
""";

        var handler = new StubHandler(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("/consumerplan/plans?", StringComparison.Ordinal))
            {
                return JsonResponse(listJson);
            }

            if (url.Contains("/consumerplan/plan/TOU1", StringComparison.Ordinal))
            {
                return JsonResponse(detailJson);
            }

            throw new InvalidOperationException($"Unexpected URL: {url}");
        });

        using var client = new HttpClient(handler);
        var fetcher = new PlanFetcher(client);
        var stored = await fetcher.FetchPlansAsync("https://api.energymadeeasy.gov.au/consumerplan/plans?postcode=YOUR_POSTCODE", "YOUR_POSTCODE", fetchAll: false, concurrency: 2, CancellationToken.None);

        Assert.Equal(2, stored.Plans.Count);
        var sr = stored.Plans.Single(p => p.PlanId == "SR1");
        var tou = stored.Plans.Single(p => p.PlanId == "TOU1");

        Assert.Equal("SR", sr.TariffType);
        Assert.NotNull(tou.Contract.First().TariffPeriod!.First().TouBlock!.First().TimeOfUse);
        Assert.NotEmpty(tou.Contract.First().TariffPeriod!.First().TouBlock!.First().TimeOfUse!);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}
