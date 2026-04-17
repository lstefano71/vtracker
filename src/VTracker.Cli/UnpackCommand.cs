using ConsoleAppFramework;
using Spectre.Console;
using VTracker.Core;

namespace VTracker.Cli;

[RegisterCommands]
public sealed class UnpackCommand(UnpackService unpackService, IAnsiConsole? console = null)
{
    /// <summary>
    /// Extract files of a given category from a VTracker ZIP archive, with optional path-prefix stripping.
    /// </summary>
    /// <param name="from">Source ZIP archive path. Must be a .zip file (passing a .json manifest is an error).</param>
    /// <param name="catalog">Path to a catalog CSV file. Auto-discovers vtracker.catalog.csv from CWD when omitted.</param>
    /// <param name="category">Category name to extract (matched case-insensitively against catalog classifications).</param>
    /// <param name="out">Output directory for the extracted files.</param>
    /// <param name="stripPrefix">Remove this leading path prefix from extracted file paths. Case-insensitive. When omitted in interactive mode, prompts with the detected common prefix.</param>
    /// <param name="dryRun">Print the extraction plan without writing files.</param>
    [Command("unpack")]
    public async Task<int> Unpack(
        string from,
        string category,
        string @out,
        string? catalog = null,
        string? stripPrefix = null,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        // When no explicit strip-prefix is given and we're interactive, do a
        // planning pass first so we can prompt for the detected common prefix
        // before any files are written.
        if (!dryRun && stripPrefix is null && !Console.IsOutputRedirected)
        {
            var planRequest = new UnpackRequest(from, catalog, category, @out, null, DryRun: true);
            var planResult = await unpackService.UnpackAsync(planRequest, cancellationToken);

            if (planResult.DetectedCommonPrefix is not null)
            {
                Console.Write($"Detected common prefix: \"{planResult.DetectedCommonPrefix}\". Strip it? [Y/n] ");
                var response = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(response)
                    || response.Equals("Y", StringComparison.OrdinalIgnoreCase)
                    || response.Equals("yes", StringComparison.OrdinalIgnoreCase))
                {
                    stripPrefix = planResult.DetectedCommonPrefix;
                }
            }
        }

        var request = new UnpackRequest(from, catalog, category, @out, stripPrefix, dryRun);
        var result = await unpackService.UnpackAsync(request, cancellationToken);

        if (dryRun)
        {
            PrintDryRunTable(result, console);
        }
        else
        {
            Console.Out.WriteLine($"Extracted {result.Files.Count} file(s).");
        }

        return 0;
    }

    private static void PrintDryRunTable(UnpackResult result, IAnsiConsole? ansiConsole)
    {
        if (ansiConsole is not null)
        {
            var table = new Table()
                .AddColumn("Source Path")
                .AddColumn("Destination Path");

            foreach (var mapping in result.Files)
            {
                table.AddRow(
                    Markup.Escape(mapping.SourcePath),
                    Markup.Escape(mapping.DestinationPath));
            }

            ansiConsole.Write(table);
        }
        else
        {
            foreach (var mapping in result.Files)
            {
                Console.Out.WriteLine($"{mapping.SourcePath} -> {mapping.DestinationPath}");
            }
        }
    }
}
