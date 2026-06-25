using System.Globalization;
using EnergyPlanCompare.Models;

namespace EnergyPlanCompare.Services;

public sealed class IntervalParser
{
    // NEM12 uses 5-minute intervals; all internal storage uses this slot count.
    private const int SlotsPerDay = 288;
    private const int MinutesPerSlot = 5;

    public IntervalData Parse(string csvPath)
    {
        var firstLine = File.ReadLines(csvPath).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
        if (firstLine is not null && IsNem12Line(firstLine))
            return ParseNem12(csvPath);
        return ParseGlobird(csvPath);
    }

    // -------------------------------------------------------------------------
    // NEM12 format (AEMO MDFF Specification NEM12/NEM13)
    // -------------------------------------------------------------------------
    // Record layout:
    //   100  Header (optional in SAPN Detailed export; skip)
    //   200  NMI Data Details: col[0]=200, col[1]=NMI, col[2]=NMIConfig,
    //                          col[3]=RegisterID, col[4]=NMISuffix (E1/B1),
    //                          col[7]=UOM, col[8]=IntervalLength (5|15|30)
    //   300  Interval Data:    col[0]=300, col[1]=IntervalDate (yyyyMMdd),
    //                          col[2..N+1]=N interval values (N=288/96/48),
    //                          col[N+2]=QualityMethod, col[N+3]=ReasonCode, ...
    //   400  Interval Events:  quality overrides for specific slots; not needed
    //   900  End of data
    //
    // Multiple 200+300 blocks can appear in one file (one per stream/channel).
    // Values are energy in UOM per interval period (typically kWh).
    // Non-5-minute data is normalised to 288 slots by dividing and repeating.

    private static bool IsNem12Line(string line)
    {
        var comma = line.IndexOf(',');
        var prefix = comma > 0 ? line[..comma] : line.Trim();
        return prefix is "100" or "200" or "300" or "400" or "500" or "900";
    }

    private static IntervalData ParseNem12(string csvPath)
    {
        var solar = new Dictionary<DateOnly, decimal[]>();
        var consumption = new Dictionary<DateOnly, decimal[]>();

        string? streamKind = null;
        int slotsInFile = SlotsPerDay;   // slots per day as stored in the file
        int expansion = 1;               // how many 5-min output slots each file slot fills

        foreach (var rawLine in File.ReadLines(csvPath))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;

            var cols = rawLine.Split(',');
            if (cols.Length == 0 || !int.TryParse(cols[0], out var recordType))
                continue;

            switch (recordType)
            {
                case 200:
                    // col[4] = NMISuffix (E1=consumption, B1=generation); col[8] = IntervalLength
                    streamKind = cols.ElementAtOrDefault(4)?.Trim();
                    if (int.TryParse(cols.ElementAtOrDefault(8)?.Trim(), out var intervalMinutes)
                        && intervalMinutes > 0 && intervalMinutes <= 30)
                    {
                        slotsInFile = 24 * 60 / intervalMinutes;
                        expansion = MinutesPerSlot * slotsInFile / (24 * 60 / SlotsPerDay * slotsInFile);
                        // Simplified: expansion = intervalMinutes / MinutesPerSlot
                        expansion = intervalMinutes / MinutesPerSlot;
                    }
                    break;

                case 300:
                    if (streamKind is null)
                        break;
                    var dateField = cols.ElementAtOrDefault(1);
                    if (!IsNem12DateField(dateField))
                        break;
                    var date = DateOnly.ParseExact(dateField!, "yyyyMMdd", CultureInfo.InvariantCulture);
                    var values = ParseNem12DayValues(cols, slotsInFile, expansion);
                    if (streamKind == "B1")
                        solar[date] = values;
                    else if (streamKind == "E1")
                        consumption[date] = values;
                    break;

                // 100, 400, 500, 900 — skip
            }
        }

        return new IntervalData(solar, consumption);
    }

    private static bool IsNem12DateField(string? value) =>
        value is { Length: 8 } && value.All(char.IsAsciiDigit);

    private static decimal[] ParseNem12DayValues(string[] cols, int slotsInFile, int expansion)
    {
        // cols[0]=300, cols[1]=date, cols[2..slotsInFile+1]=interval values
        if (cols.Length < slotsInFile + 2)
            throw new InvalidDataException(
                $"300 record has {cols.Length} columns; expected at least {slotsInFile + 2}.");

        var values = new decimal[SlotsPerDay];
        for (var i = 0; i < slotsInFile; i++)
        {
            var raw = cols[i + 2];
            var slotValue = string.IsNullOrWhiteSpace(raw)
                ? 0m
                : decimal.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);

            // For sub-5-minute data this would be >1, but NEM12 only supports 5/15/30.
            // For 15-min (expansion=3) or 30-min (expansion=6) data, distribute energy evenly.
            var valuePerOutputSlot = expansion > 1 ? slotValue / expansion : slotValue;
            for (var j = 0; j < expansion; j++)
                values[i * expansion + j] = valuePerOutputSlot;
        }

        return values;
    }

    // -------------------------------------------------------------------------
    // Globird / meter-report CSV format
    // -------------------------------------------------------------------------
    // Layout: rows with "Stream ID" header set context (B1=solar, E1=consumption),
    // followed by date rows (yyyyMMdd) with 288 values, until "Total for Period".
    // Header lines may contain leading garbage before "Stream ID".

    private static IntervalData ParseGlobird(string csvPath)
    {
        var solar = new Dictionary<DateOnly, decimal[]>();
        var consumption = new Dictionary<DateOnly, decimal[]>();

        string? streamKind = null;
        foreach (var rawLine in File.ReadLines(csvPath))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;

            var line = NormalizeHeaderLine(rawLine);
            var cols = ParseCsvLine(line);
            if (cols.Count == 0)
                continue;

            if (cols[0].Equals("Stream ID", StringComparison.OrdinalIgnoreCase))
            {
                streamKind = DetectGlobirdStreamKind(cols);
                continue;
            }

            if (cols[0].Equals("Total for Period", StringComparison.OrdinalIgnoreCase))
            {
                streamKind = null;
                continue;
            }

            if (streamKind is null || !IsDateRow(cols[0]))
                continue;

            var date = DateOnly.ParseExact(cols[0], "yyyyMMdd", CultureInfo.InvariantCulture);
            var values = ParseGlobirdDayValues(cols);

            if (streamKind == "B1")
                solar[date] = values;
            else if (streamKind == "E1")
                consumption[date] = values;
        }

        return new IntervalData(solar, consumption);
    }

    private static string? DetectGlobirdStreamKind(IReadOnlyList<string> cols)
    {
        if (cols.Count > 5)
        {
            var third = cols.ElementAtOrDefault(2) ?? string.Empty;
            var fourth = cols.ElementAtOrDefault(3) ?? string.Empty;
            var label = cols.ElementAtOrDefault(5) ?? string.Empty;

            if (third.Equals("B1", StringComparison.OrdinalIgnoreCase) ||
                fourth.Equals("B1", StringComparison.OrdinalIgnoreCase) ||
                label.Contains("Solar", StringComparison.OrdinalIgnoreCase))
                return "B1";

            if (third.Equals("E1", StringComparison.OrdinalIgnoreCase) ||
                fourth.Equals("E1", StringComparison.OrdinalIgnoreCase) ||
                label.Contains("CONSUMPTION", StringComparison.OrdinalIgnoreCase))
                return "E1";
        }

        return null;
    }

    private static bool IsDateRow(string value) =>
        value.Length == 8 && value.All(char.IsAsciiDigit);

    private static decimal[] ParseGlobirdDayValues(IReadOnlyList<string> cols)
    {
        if (cols.Count < SlotsPerDay + 1)
            throw new InvalidDataException(
                $"Row has {cols.Count} columns; expected at least {SlotsPerDay + 1}.");

        var values = new decimal[SlotsPerDay];
        for (var i = 0; i < SlotsPerDay; i++)
        {
            var raw = cols[i + 1];
            values[i] = string.IsNullOrWhiteSpace(raw)
                ? 0m
                : decimal.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        return values;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result;
    }

    private static string NormalizeHeaderLine(string line)
    {
        var streamIndex = line.IndexOf("Stream ID", StringComparison.OrdinalIgnoreCase);
        if (streamIndex > 0)
            return line[streamIndex..];
        return line;
    }
}
