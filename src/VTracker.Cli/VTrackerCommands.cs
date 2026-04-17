using System.Text.Json;
using ConsoleAppFramework;
using VTracker.Core;

namespace VTracker.Cli;

[RegisterCommands]
public sealed class VTrackerCommands(
    ExtractService extractService,
    CompareService compareService,
    ToolIdentity toolIdentity)
{
    /// <summary>
    /// Create an administrative image from an MSI, optionally apply patches, build a manifest, and package the result into a ZIP archive.
    /// </summary>
    /// <param name="msi">Source MSI path.</param>
    /// <param name="patch">Patch path. Repeat to preserve the caller-defined order.</param>
    /// <param name="out">ZIP output path. Defaults to .\{source-parent-name}.zip when omitted.</param>
    /// <param name="workDir">Working directory to use for the administrative image and installer logs.</param>
    /// <param name="keepWorkDir">Keep the work directory after a successful run.</param>
    /// <param name="emitManifest">Write an external .manifest.json beside the ZIP archive.</param>
    /// <param name="maxParallelism">Maximum degree of parallelism for hashing and metadata collection.</param>
    [Command("extract")]
    public async Task<int> Extract(
        string msi,
        string[]? patch = null,
        string? @out = null,
        string? workDir = null,
        bool keepWorkDir = false,
        bool emitManifest = false,
        int? maxParallelism = null,
        CancellationToken cancellationToken = default)
    {
        var result = await extractService.ExtractAsync(
            new ExtractRequest(
                msi,
                patch ?? Array.Empty<string>(),
                @out,
                workDir,
                keepWorkDir,
                emitManifest,
                maxParallelism),
            toolIdentity,
            cancellationToken);

        Console.Out.WriteLine($"Archive: {result.ArchivePath}");
        Console.Out.WriteLine($"Files: {result.FileCount}");
        if (result.ManifestPath is not null)
        {
            Console.Out.WriteLine($"Manifest: {result.ManifestPath}");
        }

        if (result.WorkDirectoryKept && result.WorkDirectoryPath is not null)
        {
            Console.Out.WriteLine($"Work directory: {result.WorkDirectoryPath}");
        }

        return 0;
    }

    /// <summary>
    /// Compare two manifests or archives and report added, removed, updated, and provenance-difference findings.
    /// </summary>
    /// <param name="left">Left-hand input path (.zip or .json).</param>
    /// <param name="right">Right-hand input path (.zip or .json).</param>
    /// <param name="format">Output format: text or json.</param>
    [Command("compare")]
    public async Task<int> Compare(
        string left,
        string right,
        OutputFormat format = OutputFormat.Text,
        CancellationToken cancellationToken = default)
    {
        var result = await compareService.CompareAsync(new CompareRequest(left, right, format), cancellationToken);

        if (format == OutputFormat.Json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(result, VTrackerJsonContext.Default.CompareResult));
        }
        else
        {
            Console.Out.WriteLine(CompareTextFormatter.Format(result));
        }

        return 0;
    }
}
