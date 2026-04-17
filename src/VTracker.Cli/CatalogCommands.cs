using ConsoleAppFramework;
using Spectre.Console;
using VTracker.Core;

namespace VTracker.Cli;

/// <summary>
/// Catalog management subcommands for inspecting, initialising, and maintaining
/// catalog CSV files used by VTracker's classification system.
/// </summary>
[RegisterCommands("catalog")]
public sealed class CatalogCommands(
    ManifestRepository manifestRepository,
    CatalogParser catalogParser,
    CatalogWriter catalogWriter,
    CatalogInitService catalogInitService,
    CatalogCheckService catalogCheckService)
{
    /// <summary>
    /// Initialise a new catalog CSV from a manifest. Creates one exact-path glob
    /// row per file, all assigned to the "Unclassified" category.
    /// </summary>
    /// <param name="manifest">Path to a manifest (.json) or archive (.zip) to seed the catalog from.</param>
    /// <param name="out">Output path for the generated catalog CSV file.</param>
    /// <param name="cancellationToken">Cancellation token provided by the framework.</param>
    [Command("init")]
    public async Task<int> Init(
        string manifest,
        string @out,
        CancellationToken cancellationToken = default)
    {
        var manifestDoc = await manifestRepository.LoadFromPathAsync(manifest, cancellationToken);
        catalogInitService.Init(manifestDoc, @out);

        Console.Out.WriteLine($"Catalog initialised: {@out} ({manifestDoc.Files.Length} entries)");
        return 0;
    }

    /// <summary>
    /// Check a catalog for dead patterns that match zero files in the given manifest.
    /// Reports entries that can be safely removed or need updating.
    /// </summary>
    /// <param name="catalog">Path to the catalog CSV file to check.</param>
    /// <param name="manifest">Path to a manifest (.json) or archive (.zip) to check against.</param>
    /// <param name="cancellationToken">Cancellation token provided by the framework.</param>
    [Command("check")]
    public async Task<int> Check(
        string catalog,
        string manifest,
        CancellationToken cancellationToken = default)
    {
        var manifestDoc = await manifestRepository.LoadFromPathAsync(manifest, cancellationToken);
        var result = catalogCheckService.Check(catalog, manifestDoc);

        if (result.DeadEntries.Count == 0)
        {
            Console.Out.WriteLine("All catalog patterns match at least one file.");
            return 0;
        }

        Console.Out.WriteLine($"{result.DeadEntries.Count} dead pattern(s) found:");
        Console.Out.WriteLine();

        if (!Console.IsOutputRedirected)
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn(new TableColumn("[bold]Row[/]").RightAligned())
                .AddColumn(new TableColumn("[bold]Type[/]").Centered())
                .AddColumn(new TableColumn("[bold]Pattern[/]").LeftAligned())
                .AddColumn(new TableColumn("[bold]Category[/]").LeftAligned());

            foreach (var entry in result.DeadEntries)
            {
                table.AddRow(
                    entry.RowNumber.ToString(),
                    entry.Type == CatalogRowType.Glob ? "G" : "R",
                    Markup.Escape(entry.Pattern),
                    Markup.Escape(entry.Category));
            }

            AnsiConsole.Write(table);
        }
        else
        {
            foreach (var entry in result.DeadEntries)
            {
                var typeChar = entry.Type == CatalogRowType.Glob ? "G" : "R";
                Console.Out.WriteLine($"  Row {entry.RowNumber}: [{typeChar}] {entry.Pattern} -> {entry.Category}");
            }
        }

        return 0;
    }

    /// <summary>
    /// Export a catalog file to a clean RFC 4180-compliant CSV. Re-serialises the
    /// catalog through the writer to normalise quoting and escaping.
    /// </summary>
    /// <param name="catalog">Path to the source catalog CSV file.</param>
    /// <param name="out">Output path for the exported CSV file.</param>
    /// <param name="cancellationToken">Cancellation token provided by the framework.</param>
    [Command("export")]
    public Task<int> Export(
        string catalog,
        string @out,
        CancellationToken cancellationToken = default)
    {
        var rows = catalogParser.ParseRows(catalog);
        catalogWriter.Write(@out, rows);

        Console.Out.WriteLine($"Catalog exported: {@out} ({rows.Count} entries)");
        return Task.FromResult(0);
    }

    /// <summary>
    /// Display catalog entries as a formatted table. Optionally filter by category.
    /// </summary>
    /// <param name="catalog">Path to the catalog CSV file to display.</param>
    /// <param name="category">Optional category name to filter displayed entries.</param>
    /// <param name="cancellationToken">Cancellation token provided by the framework.</param>
    [Command("show")]
    public Task<int> Show(
        string catalog,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        var rows = catalogParser.ParseRows(catalog);

        IReadOnlyList<CatalogRow> filtered = category is not null
            ? rows.Where(r => r.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList()
            : rows;

        if (!Console.IsOutputRedirected)
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title(category is not null
                    ? $"[bold]Catalog entries — {Markup.Escape(category)}[/]"
                    : "[bold]Catalog entries[/]")
                .AddColumn(new TableColumn("[bold]#[/]").RightAligned())
                .AddColumn(new TableColumn("[bold]Type[/]").Centered())
                .AddColumn(new TableColumn("[bold]Pattern[/]").LeftAligned())
                .AddColumn(new TableColumn("[bold]Category[/]").LeftAligned());

            for (var i = 0; i < filtered.Count; i++)
            {
                var row = filtered[i];
                table.AddRow(
                    (i + 1).ToString(),
                    row.Type == CatalogRowType.Glob ? "G" : "R",
                    Markup.Escape(row.Pattern),
                    Markup.Escape(row.Category));
            }

            AnsiConsole.Write(table);
        }
        else
        {
            foreach (var row in filtered)
            {
                var typeChar = row.Type == CatalogRowType.Glob ? "G" : "R";
                Console.Out.WriteLine($"{typeChar},{row.Pattern},{row.Category}");
            }
        }

        Console.Out.WriteLine($"{filtered.Count} of {rows.Count} entries shown.");
        return Task.FromResult(0);
    }

    /// <summary>
    /// Interactively compact exact-path glob entries into broader patterns.
    /// Groups entries by category and prompts for a replacement pattern per group.
    /// Requires an interactive terminal (TTY).
    /// </summary>
    /// <param name="catalog">Path to the catalog CSV file to compact.</param>
    /// <param name="manifest">Optional manifest path to validate replacement pattern match counts against real files.</param>
    /// <param name="cancellationToken">Cancellation token provided by the framework.</param>
    [Command("compact")]
    public async Task<int> Compact(
        string catalog,
        string? manifest = null,
        CancellationToken cancellationToken = default)
    {
        if (Console.IsOutputRedirected)
        {
            Console.Error.WriteLine("The compact command requires an interactive terminal.");
            return 1;
        }

        var rows = catalogParser.ParseRows(catalog).ToList();

        ManifestDocument? manifestDoc = null;
        if (manifest is not null)
        {
            manifestDoc = await manifestRepository.LoadFromPathAsync(manifest, cancellationToken);
        }

        // Group exact-path G entries by category
        var exactGroups = rows
            .Select((row, index) => (Row: row, Index: index))
            .Where(x => x.Row.Type == CatalogRowType.Glob && !ContainsGlobWildcard(x.Row.Pattern))
            .GroupBy(x => x.Row.Category, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (exactGroups.Count == 0)
        {
            Console.Out.WriteLine("No groups of exact-path entries found to compact.");
            return 0;
        }

        // Track which original indices should be replaced. We collect all
        // decisions first, then rebuild the list once at the end to avoid
        // stale-index corruption from in-place mutations.
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var removedIndices = new HashSet<int>();
        var insertionPoints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var totalReplaced = 0;

        foreach (var group in exactGroups)
        {
            AnsiConsole.MarkupLine($"\n[bold]{Markup.Escape(group.Key)}[/]: {group.Count()} exact-path entries");

            foreach (var entry in group.Take(5))
            {
                AnsiConsole.MarkupLine($"  [dim]{Markup.Escape(entry.Row.Pattern)}[/]");
            }

            if (group.Count() > 5)
            {
                AnsiConsole.MarkupLine($"  [dim]... and {group.Count() - 5} more[/]");
            }

            var pattern = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter replacement glob pattern (or [grey]empty[/] to skip):")
                    .AllowEmpty());

            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            // Compute match count
            int matchCount;
            if (manifestDoc is not null)
            {
                matchCount = manifestDoc.Files.Count(f =>
                    GlobFilter.MatchesAny(f.Path, new[] { pattern }));
                AnsiConsole.MarkupLine($"  Pattern matches [bold]{matchCount}[/] file(s) in manifest.");
            }
            else
            {
                matchCount = group.Count();
                AnsiConsole.MarkupLine($"  Pattern would replace [bold]{matchCount}[/] exact entries.");
            }

            var confirm = AnsiConsole.Prompt(
                new TextPrompt<bool>($"Replace {group.Count()} entries with [green]{Markup.Escape(pattern)}[/]?")
                    .AddChoice(true)
                    .AddChoice(false)
                    .DefaultValue(false)
                    .WithConverter(v => v ? "y" : "n"));

            if (!confirm)
            {
                continue;
            }

            foreach (var entry in group)
            {
                removedIndices.Add(entry.Index);
            }

            replacements[group.Key] = pattern;
            insertionPoints[group.Key] = group.Min(x => x.Index);
            totalReplaced += group.Count();
        }

        if (totalReplaced > 0)
        {
            // Rebuild the list: keep non-removed rows, insert replacements at
            // the earliest original position of each group.
            var result = new List<CatalogRow>(rows.Count);
            var inserted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < rows.Count; i++)
            {
                // Check if a replacement should be inserted at this position
                foreach (var (category, insertAt) in insertionPoints)
                {
                    if (insertAt == i && !inserted.Contains(category))
                    {
                        result.Add(new CatalogRow(CatalogRowType.Glob, replacements[category], category));
                        inserted.Add(category);
                    }
                }

                if (!removedIndices.Contains(i))
                {
                    result.Add(rows[i]);
                }
            }

            catalogWriter.Write(catalog, result);
            Console.Out.WriteLine($"Catalog updated: {totalReplaced} entries compacted.");
        }
        else
        {
            Console.Out.WriteLine("No changes made.");
        }

        return 0;
    }

    /// <summary>
    /// Returns <c>true</c> when the pattern contains glob wildcard characters.
    /// </summary>
    private static bool ContainsGlobWildcard(string pattern)
    {
        return pattern.Contains('*') || pattern.Contains('?') || pattern.Contains('[');
    }
}
