using HttpHammer.Diagnostics;
using Spectre.Console;

namespace HttpHammer.Console.Renderers;

internal static class ResultRenderer
{
    public static void DisplayResults(this IAnsiConsole console, IReadOnlyDictionary<string, Measurements> measurements)
    {
        if (measurements.Count == 0)
        {
            return;
        }

        // console.Clear();
        // console.ShowSplashScreen();
        var table = new Table { Expand = true };
        table.AddColumns("Request", "Min", "P50", "P75", "P95", "Max", "Duration", "# Requests", "# Errors");

        foreach (var (request, measurement) in measurements)
        {
            var hasErrors = measurement.Errors.Count > 0;
            table.AddRow(
                hasErrors ? $"{request} :warning:" : request,
                measurement.Min.ToDisplayString(),
                measurement.P50.ToDisplayString(),
                measurement.P75.ToDisplayString(),
                measurement.P95.ToDisplayString(),
                measurement.Max.ToDisplayString(),
                measurement.Total.ToDisplayString(),
                measurement.Count.ToString("N0"),
                hasErrors ? $"[red]{measurement.Errors.Count:N0}[/]" : "0");
        }

        console.WriteLine();
        console.Write(new Rule("Hammering Result") { Justification = Justify.Center, Style = new Style(decoration: Decoration.Bold) });
        console.Write(table);
        console.WriteLine();
        console.DisplayErrors(measurements);
    }

    private static void DisplayErrors(this IAnsiConsole console, IReadOnlyDictionary<string, Measurements> measurements)
    {
        var errorGroups = measurements
            .SelectMany(m => m.Value.Errors.Select(error => (Request: m.Key, Error: error)))
            .GroupBy(x => x.Error)
            .Select(group => new
            {
                Error = group.Key,
                Count = group.Count(),
                Request = group.First().Request
            })
            .ToList();

        if (errorGroups.Count == 0)
        {
            return;
        }

        var table = new Table
        {
            Expand = true,
            Title = new TableTitle("Failed Requests", new Style(foreground: Color.Red, decoration: Decoration.Bold))
        };

        table.AddColumns("[bold]Request[/]", "# Errors", "Message");
        foreach (var group in errorGroups)
        {
            table.AddRow(group.Request, group.Count.ToString("N0"), group.Error);
        }

        console.Write(table);
    }
}