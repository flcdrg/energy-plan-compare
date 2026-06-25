using EnergyPlanCompare.Services;

namespace EnergyPlanCompare.Tests;

public class IntervalParserTests
{
    // -------------------------------------------------------------------------
    // Globird format
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_Globird_ReadsSolarAndConsumptionSections()
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
    public void Parse_Globird_MatchesKnownTotals()
    {
        var parser = new IntervalParser();
        var data = parser.Parse(TestPaths.Fixture("sample-interval-anonymised.csv"));

        var solarTotal = data.SolarByDate.Values.SelectMany(x => x).Sum();
        var consumptionTotal = data.ConsumptionByDate.Values.SelectMany(x => x).Sum();

        Assert.Equal(11.552m, solarTotal, 3);
        Assert.Equal(38.496m, consumptionTotal, 3);
    }

    [Fact]
    public void Parse_Globird_ThrowsOnMalformedDataRow()
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

    // -------------------------------------------------------------------------
    // NEM12 format (AEMO MDFF Specification NEM12/NEM13)
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_Nem12_ReadsSolarAndConsumptionStreams()
    {
        var parser = new IntervalParser();
        var data = parser.Parse(TestPaths.Fixture("sample-interval-nem12.csv"));

        Assert.Equal(2, data.SolarByDate.Count);
        Assert.Equal(2, data.ConsumptionByDate.Count);
        Assert.All(data.SolarByDate.Values, day => Assert.Equal(288, day.Length));
        Assert.All(data.ConsumptionByDate.Values, day => Assert.Equal(288, day.Length));
    }

    [Fact]
    public void Parse_Nem12_MatchesGlobirdTotals()
    {
        // NEM12 fixture contains identical data to the Globird fixture
        var parser = new IntervalParser();
        var nem12 = parser.Parse(TestPaths.Fixture("sample-interval-nem12.csv"));
        var globird = parser.Parse(TestPaths.Fixture("sample-interval-anonymised.csv"));

        Assert.Equal(
            globird.SolarByDate.Values.SelectMany(x => x).Sum(),
            nem12.SolarByDate.Values.SelectMany(x => x).Sum(), 3);
        Assert.Equal(
            globird.ConsumptionByDate.Values.SelectMany(x => x).Sum(),
            nem12.ConsumptionByDate.Values.SelectMany(x => x).Sum(), 3);
    }

    [Fact]
    public void Parse_Nem12_MatchesSlotValuesForKnownDay()
    {
        var parser = new IntervalParser();
        var data = parser.Parse(TestPaths.Fixture("sample-interval-nem12.csv"));

        var day1 = new DateOnly(2026, 6, 20);
        Assert.Equal(0.03m, data.SolarByDate[day1][96]);   // 08:00
        Assert.Equal(0.09m, data.SolarByDate[day1][144]);  // 12:00
        Assert.Equal(0.11m, data.ConsumptionByDate[day1][72]);  // 06:00
        Assert.Equal(0.15m, data.ConsumptionByDate[day1][204]); // 17:00
    }

    [Fact]
    public void Parse_Nem12_Normalises30MinIntervalsTo288Slots()
    {
        // A 30-minute NEM12 file has 48 slots/day; each slot expands to 6 output slots,
        // with energy divided equally so totals are preserved.
        var tmp = Path.GetTempFileName();
        try
        {
            // Build a 30-min NEM12 with known values: 48 E1 slots all = 0.3 kWh
            var slots = string.Join(",", Enumerable.Repeat("0.3", 48));
            File.WriteAllText(tmp,
                $"200,0000000000,E1,E1,E1,,ANON,KWH,30,\n" +
                $"300,20260620,{slots},A,,\n" +
                "900\n");

            var parser = new IntervalParser();
            var data = parser.Parse(tmp);

            var day = new DateOnly(2026, 6, 20);
            Assert.True(data.ConsumptionByDate.ContainsKey(day));
            Assert.Equal(288, data.ConsumptionByDate[day].Length);

            // Each 30-min slot (0.3 kWh) expands to 6 × 0.05 kWh output slots
            Assert.All(data.ConsumptionByDate[day], v => Assert.Equal(0.05m, v));

            // Total energy preserved: 48 × 0.3 = 14.4 kWh
            Assert.Equal(14.4m, data.ConsumptionByDate[day].Sum(), 3);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void Parse_Nem12_Normalises15MinIntervalsTo288Slots()
    {
        // A 15-minute NEM12 file has 96 slots/day; each slot expands to 3 output slots.
        var tmp = Path.GetTempFileName();
        try
        {
            var slots = string.Join(",", Enumerable.Repeat("0.6", 96));
            File.WriteAllText(tmp,
                $"200,0000000000,E1,E1,E1,,ANON,KWH,15,\n" +
                $"300,20260620,{slots},A,,\n" +
                "900\n");

            var parser = new IntervalParser();
            var data = parser.Parse(tmp);

            var day = new DateOnly(2026, 6, 20);
            Assert.True(data.ConsumptionByDate.ContainsKey(day));
            Assert.Equal(288, data.ConsumptionByDate[day].Length);

            // Each 15-min slot (0.6 kWh) expands to 3 × 0.2 kWh output slots
            Assert.All(data.ConsumptionByDate[day], v => Assert.Equal(0.2m, v));

            // Total energy preserved: 96 × 0.6 = 57.6 kWh
            Assert.Equal(57.6m, data.ConsumptionByDate[day].Sum(), 3);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void Parse_Nem12_IgnoresNonE1B1Streams()
    {
        // Streams other than E1 (consumption) and B1 (generation) should be ignored
        var tmp = Path.GetTempFileName();
        try
        {
            var slots = string.Join(",", Enumerable.Repeat("0.1", 288));
            File.WriteAllText(tmp,
                $"200,0000000000,E1Q1,Q1,Q1,,ANON,KWH,05,\n" +
                $"300,20260620,{slots},A,,\n" +
                "900\n");

            var parser = new IntervalParser();
            var data = parser.Parse(tmp);

            Assert.Empty(data.ConsumptionByDate);
            Assert.Empty(data.SolarByDate);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    // Rename these to match the old test names so existing usages keep working
    [Fact]
    public void Parse_ReadsSolarAndConsumptionSections() =>
        Parse_Globird_ReadsSolarAndConsumptionSections();

    [Fact]
    public void Parse_MatchesKnownTotals() =>
        Parse_Globird_MatchesKnownTotals();
}

