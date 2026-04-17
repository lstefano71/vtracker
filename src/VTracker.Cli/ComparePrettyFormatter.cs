using Spectre.Console;
using VTracker.Core;

namespace VTracker.Cli;

/// <summary>
/// Renders a <see cref="CompareResult"/> using Spectre.Console markup for colour-rich output.
/// Accepts an <see cref="IAnsiConsole"/> so the output can be redirected in tests.
/// </summary>
public static class ComparePrettyFormatter
{
    public static void Write(IAnsiConsole console, CompareResult result, int hiddenCount = 0)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(result);

        WriteSummary(console, result.Summary);

        var hasDetails = result.Added.Length > 0 ||
            result.Removed.Length > 0 ||
            result.Updated.Length > 0 ||
            result.ProvenanceDifferences.Length > 0;

        if (hasDetails)
        {
            console.WriteLine();
            WriteDetails(console, result);
        }

        if (hiddenCount > 0)
        {
            console.WriteLine();
            console.MarkupLine($"[grey]({hiddenCount} file {(hiddenCount == 1 ? "row" : "rows")} hidden by --include filter)[/]");
        }
    }

    private static void WriteSummary(IAnsiConsole console, CompareSummary summary)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]Category[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Count[/]").RightAligned());

        table.AddRow("[green]Added[/]", summary.Added.ToString());
        table.AddRow("[red]Removed[/]", summary.Removed.ToString());
        table.AddRow("[yellow]Updated[/]", summary.Updated.ToString());
        table.AddRow("[grey]Provenance differences[/]", summary.ProvenanceDifferences.ToString());

        console.Write(table);
    }

    private static void WriteDetails(IAnsiConsole console, CompareResult result)
    {
        foreach (var path in result.Added)
        {
            console.MarkupLine($"[green]+[/] {Markup.Escape(path)}");
        }

        foreach (var path in result.Removed)
        {
            console.MarkupLine($"[red]-[/] {Markup.Escape(path)}");
        }

        foreach (var update in result.Updated)
        {
            console.MarkupLine($"[yellow]~[/] {Markup.Escape(update.Path)}");
            WriteUpdatedDetail(console, update);
        }

        foreach (var difference in result.ProvenanceDifferences)
        {
            console.MarkupLine($"[grey]![/] {Markup.Escape(difference)}");
        }
    }

    private static void WriteUpdatedDetail(IAnsiConsole console, CompareUpdatedFile update)
    {
        var leftSize = FormatSize(update.Left.Size);
        var rightSize = FormatSize(update.Right.Size);
        var sizeChange = update.Left.Size == update.Right.Size
            ? $"  size: {leftSize}"
            : $"  size: {leftSize} → {rightSize}";
        console.MarkupLine($"[grey]{Markup.Escape(sizeChange)}[/]");

        var leftVer = update.Left.FileVersion ?? update.Left.ProductVersion;
        var rightVer = update.Right.FileVersion ?? update.Right.ProductVersion;
        if (leftVer is not null || rightVer is not null)
        {
            var verDisplay = leftVer == rightVer
                ? $"  version: {leftVer ?? "(none)"}"
                : $"  version: {leftVer ?? "(none)"} → {rightVer ?? "(none)"}";
            console.MarkupLine($"[grey]{Markup.Escape(verDisplay)}[/]");
        }
    }

    private static string FormatSize(long bytes) =>
        bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / (1024.0 * 1024):F1} MB",
        };
}
