using System.IO.Compression;

namespace VTracker.Core;

public sealed class CompareExportService
{
    /// <summary>
    /// Copies a subset of files from a right-hand ZIP archive into a new self-contained ZIP.
    /// </summary>
    /// <param name="rightZipPath">Path to the right-hand source ZIP produced by <c>extract</c>.</param>
    /// <param name="normalizedPaths">Normalized <c>/</c>-separated paths to export (from compare results).</param>
    /// <param name="outputZipPath">Destination ZIP path. Must not already exist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<CompareExportResult> ExportAsync(
        string rightZipPath,
        IReadOnlyList<string> normalizedPaths,
        string outputZipPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(normalizedPaths);

        // --- Validate right input -------------------------------------------------
        var resolvedRight = Path.GetFullPath(rightZipPath);
        if (!string.Equals(Path.GetExtension(resolvedRight), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new VTrackerException(
                $"--export-zip requires the right-hand input '{resolvedRight}' to be a '.zip' archive; standalone manifests do not contain file content.");
        }

        if (!File.Exists(resolvedRight))
        {
            throw new VTrackerException($"Right-hand input '{resolvedRight}' does not exist.");
        }

        // --- Validate output path -------------------------------------------------
        var resolvedOutput = Path.GetFullPath(outputZipPath);
        if (!string.Equals(Path.GetExtension(resolvedOutput), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new VTrackerException($"Export output path '{resolvedOutput}' must have a '.zip' extension.");
        }

        if (File.Exists(resolvedOutput))
        {
            throw new VTrackerException($"Export output path '{resolvedOutput}' already exists.");
        }

        if (Directory.Exists(resolvedOutput))
        {
            throw new VTrackerException($"Export output path '{resolvedOutput}' is an existing directory.");
        }

        var parentDir = Path.GetDirectoryName(resolvedOutput);
        if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        // --- Build entry lookup from right ZIP (collision-safe) -------------------
        using var rightArchive = ZipFile.OpenRead(resolvedRight);

        var lookup = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in rightArchive.Entries)
        {
            var normalizedEntryPath = entry.FullName.Replace('\\', '/').Trim('/');
            if (lookup.ContainsKey(normalizedEntryPath))
            {
                throw new VTrackerException(
                    $"Right-hand archive '{resolvedRight}' contains ambiguous entries that both normalize to '{normalizedEntryPath}'.");
            }

            lookup[normalizedEntryPath] = entry;
        }

        // --- Resolve all requested paths BEFORE creating output (fail fast) -------
        var resolvedEntries = new List<(string NormalizedPath, ZipArchiveEntry Entry)>(normalizedPaths.Count);
        foreach (var path in normalizedPaths)
        {
            if (!lookup.TryGetValue(path, out var entry))
            {
                throw new VTrackerException(
                    $"Export failed: path '{path}' was not found in right-hand archive '{resolvedRight}'.");
            }

            resolvedEntries.Add((path, entry));
        }

        // --- Write output ZIP; delete partial file on failure ---------------------
        try
        {
            await using var outputStream = new FileStream(resolvedOutput, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var outputArchive = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true);

            foreach (var (normalizedPath, sourceEntry) in resolvedEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var newEntry = outputArchive.CreateEntry(normalizedPath, CompressionLevel.Optimal);
                newEntry.LastWriteTime = sourceEntry.LastWriteTime;

                await using var srcStream = sourceEntry.Open();
                await using var dstStream = newEntry.Open();
                await srcStream.CopyToAsync(dstStream, cancellationToken);
            }
        }
        catch
        {
            try { File.Delete(resolvedOutput); } catch { /* best-effort cleanup */ }
            throw;
        }

        return new CompareExportResult(resolvedEntries.Count, resolvedOutput);
    }
}
