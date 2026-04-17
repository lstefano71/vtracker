using Humanizer;
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

        if (summary.CategoryBreakdown is { Length: > 0 } breakdown)
        {
            console.WriteLine();

            var catTable = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold]Per-category breakdown[/]")
                .AddColumn(new TableColumn("[bold]Category[/]").LeftAligned())
                .AddColumn(new TableColumn("[bold green]+[/]").RightAligned())
                .AddColumn(new TableColumn("[bold red]-[/]").RightAligned())
                .AddColumn(new TableColumn("[bold yellow]~[/]").RightAligned());

            foreach (var cat in breakdown)
            {
                catTable.AddRow(
                    Markup.Escape(cat.Category),
                    cat.Added.ToString(),
                    cat.Removed.ToString(),
                    cat.Updated.ToString());
            }

            console.Write(catTable);
        }
    }

    private static void WriteDetails(IAnsiConsole console, CompareResult result)
    {
        foreach (var added in result.Added)
        {
            var catLabel = added.Category is not null ? $"  [dim][[{Markup.Escape(added.Category)}]][/]" : "";
            console.MarkupLine($"[green]+[/] {Markup.Escape(added.Path)}{catLabel}");
        }

        foreach (var removed in result.Removed)
        {
            var catLabel = removed.Category is not null ? $"  [dim][[{Markup.Escape(removed.Category)}]][/]" : "";
            console.MarkupLine($"[red]-[/] {Markup.Escape(removed.Path)}{catLabel}");
        }

        WriteUpdatedFiles(console, result.Updated);

        foreach (var difference in result.ProvenanceDifferences)
        {
            console.MarkupLine($"[orange3]![/] {Markup.Escape(difference)}");
        }
    }

    private static void WriteUpdatedFiles(IAnsiConsole console, CompareUpdatedFile[] updated)
    {
        if (updated.Length == 0) return;

        // Pre-compute plain-text size strings so we can measure the widest one
        // for column alignment before emitting any markup.
        var leftSizes = Array.ConvertAll(updated, u => HumanizeBytes(u.Left.Size));
        var rightSizes = Array.ConvertAll(updated, u => HumanizeBytes(u.Right.Size));

        // Visual width of the size-change field: "X.X MB → Y.Y MB" or just "X.X MB"
        var sizeFieldWidths = new int[updated.Length];
        for (var i = 0; i < updated.Length; i++)
        {
            sizeFieldWidths[i] = updated[i].Left.Size == updated[i].Right.Size
                ? leftSizes[i].Length
                : leftSizes[i].Length + 3 + rightSizes[i].Length; // " → " = 3 visible chars
        }
        var maxSizeFieldWidth = sizeFieldWidths.Max();

        for (var i = 0; i < updated.Length; i++)
        {
            var update = updated[i];

            console.MarkupLine($"[yellow]~[/] {Markup.Escape(update.Path)}");

            // ── size part (markup) ──────────────────────────────────────────
            string sizePart;
            if (update.Left.Size == update.Right.Size)
            {
                sizePart = $"[dim]{Markup.Escape(leftSizes[i])}[/]";
            }
            else
            {
                sizePart = $"[yellow]{Markup.Escape(leftSizes[i])}[/] [dim]→[/] [green]{Markup.Escape(rightSizes[i])}[/]";
            }

            // ── version part (markup, null when no version on either side) ──
            var versionPart = BuildVersionPart(update);

            if (versionPart is not null)
            {
                var padding = new string(' ', maxSizeFieldWidth - sizeFieldWidths[i] + 2);
                console.MarkupLine($"  {sizePart}{padding}{versionPart}");
            }
            else
            {
                console.MarkupLine($"  {sizePart}");
            }
        }
    }

    private static string? BuildVersionPart(CompareUpdatedFile update)
    {
        var leftVer = update.Left.FileVersion ?? update.Left.ProductVersion;
        var rightVer = update.Right.FileVersion ?? update.Right.ProductVersion;

        if (leftVer is null && rightVer is null)
            return null;

        if (leftVer == rightVer)
            return $"[dim]{Markup.Escape(leftVer!)}[/]";

        return $"[yellow]{Markup.Escape(leftVer ?? "(none)")}[/] [dim]→[/] [green]{Markup.Escape(rightVer ?? "(none)")}[/]";
    }

    private static string HumanizeBytes(long bytes) =>
        ByteSize.FromBytes(bytes).Humanize();
}
