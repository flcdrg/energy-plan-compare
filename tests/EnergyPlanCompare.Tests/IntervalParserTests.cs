using EnergyPlanCompare.Services;

namespace EnergyPlanCompare.Tests;

public class IntervalParserTests
{
    [Fact]
    public void Parse_ReadsSolarAndConsumptionSections()
    {
        var parser = new IntervalParser();
        var data = parser.Parse(TestPaths.Fixture("sample-interval-anonymised.csv"));

        Assert.Equal(2, data.SolarByDate.Count);
        Assert.Equal(2, data.ConsumptionByDate.Count);
        Assert.All(data.SolarByDate.Values, day => Assert.Equal(288, day.Length));
        Assert.All(data.ConsumptionByDate.Values, day => Assert.Equal(288, day.Length));

        var day1 = new DateOnly(2026, 6, 20);
        Assert.Equal(0.03m, data.SolarByDate[day1][96]);   // 08:00
        Assert.Equal(0.09m, data.SolarByDate[day1][144]);  // 12:00
        Assert.Equal(0.11m, data.ConsumptionByDate[day1][72]);  // 06:00
        Assert.Equal(0.15m, data.ConsumptionByDate[day1][204]); // 17:00
    }

    [Fact]
    public void Parse_MatchesKnownTotals()
    {
        var parser = new IntervalParser();
        var data = parser.Parse(TestPaths.Fixture("sample-interval-anonymised.csv"));

        var solarTotal = data.SolarByDate.Values.SelectMany(x => x).Sum();
        var consumptionTotal = data.ConsumptionByDate.Values.SelectMany(x => x).Sum();

        Assert.Equal(11.552m, solarTotal, 3);
        Assert.Equal(38.496m, consumptionTotal, 3);
    }

    [Fact]
    public void Parse_ThrowsOnMalformedDataRow()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, """
Nmi,0000000000,NETWORK,UMPLP
Stream ID,"Meter ANON000001,ANON000001",E1,E1,KWH,CONSUMPTION
LOCAL TIME,AEST
Date/Time,0:00,0:05,Quality,Total
20260620,0.1,0.2,A,0.3
Total for Period,,,,0.3
""");
            var parser = new IntervalParser();
            Assert.Throws<InvalidDataException>(() => parser.Parse(tmp));
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}

