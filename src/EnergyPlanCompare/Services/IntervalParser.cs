using System.Globalization;
using EnergyPlanCompare.Models;

namespace EnergyPlanCompare.Services;

public sealed class IntervalParser
{
    private const int SlotsPerDay = 288;

    public IntervalData Parse(string csvPath)
    {
        var solar = new Dictionary<DateOnly, decimal[]>();
        var consumption = new Dictionary<DateOnly, decimal[]>();

        string? streamKind = null; // B1 or E1
        foreach (var rawLine in File.ReadLines(csvPath))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var line = NormalizeHeaderLine(rawLine);
            var cols = ParseCsvLine(line);
            if (cols.Count == 0)
            {
                continue;
            }

            if (cols[0].Equals("Stream ID", StringComparison.OrdinalIgnoreCase))
            {
                streamKind = DetectStreamKind(cols);
                continue;
            }

            if (cols[0].Equals("Total for Period", StringComparison.OrdinalIgnoreCase))
            {
                streamKind = null;
                continue;
            }

            if (streamKind is null || !IsDateRow(cols[0]))
            {
                continue;
            }

            var date = DateOnly.ParseExact(cols[0], "yyyyMMdd", CultureInfo.InvariantCulture);
            var values = ParseDayValues(cols);

            if (streamKind == "B1")
            {
                solar[date] = values;
            }
            else if (streamKind == "E1")
            {
                consumption[date] = values;
            }
        }

        return new IntervalData(solar, consumption);
    }

    private static string? DetectStreamKind(IReadOnlyList<string> cols)
    {
        if (cols.Count > 5)
        {
            var third = cols.ElementAtOrDefault(2) ?? string.Empty;
            var fourth = cols.ElementAtOrDefault(3) ?? string.Empty;
            var label = cols.ElementAtOrDefault(5) ?? string.Empty;

            if (third.Equals("B1", StringComparison.OrdinalIgnoreCase) ||
                fourth.Equals("B1", StringComparison.OrdinalIgnoreCase) ||
                label.Contains("Solar", StringComparison.OrdinalIgnoreCase))
            {
                return "B1";
            }

            if (third.Equals("E1", StringComparison.OrdinalIgnoreCase) ||
                fourth.Equals("E1", StringComparison.OrdinalIgnoreCase) ||
                label.Contains("CONSUMPTION", StringComparison.OrdinalIgnoreCase))
            {
                return "E1";
            }
        }

        return null;
    }

    private static bool IsDateRow(string value) =>
        value.Length == 8 && value.All(char.IsDigit);

    private static decimal[] ParseDayValues(IReadOnlyList<string> cols)
    {
        if (cols.Count < SlotsPerDay + 1)
        {
            throw new InvalidDataException($"Row has {cols.Count} columns; expected at least {SlotsPerDay + 1}.");
        }

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
        {
            return line[streamIndex..];
        }

        return line;
    }
}
