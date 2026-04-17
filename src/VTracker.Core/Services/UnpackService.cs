using System.IO.Compression;

namespace VTracker.Core;

/// <summary>
/// Extracts files from a VTracker ZIP archive filtered by catalog category,
/// with optional path-prefix stripping for a flattened output layout.
/// </summary>
public sealed class UnpackService(
    ManifestRepository manifestRepository,
    CatalogDiscovery catalogDiscovery,
    CatalogParser catalogParser,
    CatalogClassifier catalogClassifier)
{
    public async Task<UnpackResult> UnpackAsync(UnpackRequest request, CancellationToken cancellationToken)
    {
        // 1. Validate --from path
        ValidateFromPath(request.FromPath);

        // 2. Resolve catalog
        var catalogPath = catalogDiscovery.Resolve(request.CatalogPath, Environment.CurrentDirectory)
            ?? throw new VTrackerException(
                "A catalog is required for unpack. Provide --catalog or place a vtracker.catalog.csv in the working directory.");

        // 3. Parse catalog
        var catalog = catalogParser.Parse(catalogPath);

        // 4. Load manifest from the ZIP
        var manifest = await manifestRepository.LoadFromPathAsync(request.FromPath, cancellationToken);

        // 5. Classify all files and filter to the requested category
        var allPaths = manifest.Files.Select(f => f.Path);
        var classifications = catalogClassifier.ClassifyAll(catalog, allPaths);

        var matchingFiles = manifest.Files
            .Where(f => classifications.TryGetValue(f.Path, out var cat)
                        && string.Equals(cat, request.Category, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // 6. Compute file mappings
        string? detectedPrefix = null;
        string? effectiveStrip = request.StripPrefix;

        if (effectiveStrip is null && matchingFiles.Count > 0)
        {
            detectedPrefix = ComputeCommonPrefix(matchingFiles.Select(f => f.Path));
        }

        var mappings = new List<UnpackFileMapping>(matchingFiles.Count);
        foreach (var file in matchingFiles)
        {
            var destination = ApplyStripPrefix(file.Path, effectiveStrip);
            mappings.Add(new UnpackFileMapping(file.Path, destination, request.Category));
        }

        // 7. Dry run — return without writing
        if (request.DryRun)
        {
            return new UnpackResult(mappings, detectedPrefix);
        }

        // 8. Extract matching files from the ZIP
        ExtractFiles(request.FromPath, request.OutputDirectory, mappings);

        return new UnpackResult(mappings, detectedPrefix);
    }

    private static void ValidateFromPath(string path)
    {
        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new VTrackerException(
                $"The 'unpack' command requires a ZIP archive, not a manifest file. Use the corresponding ZIP archive for '{path}'.");
        }

        if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new VTrackerException("'--from' must point to a '.zip' file.");
        }

        if (!File.Exists(path))
        {
            throw new VTrackerException($"File '{path}' does not exist.");
        }
    }

    /// <summary>
    /// Computes the longest common path prefix across all paths (split by <c>/</c>).
    /// Returns the common leading segments joined with <c>/</c>, or <c>null</c> if
    /// there is no common prefix beyond the root.
    /// </summary>
    internal static string? ComputeCommonPrefix(IEnumerable<string> paths)
    {
        string[]? commonSegments = null;

        foreach (var path in paths)
        {
            var segments = path.Split('/');
            // Only consider directory segments (exclude the file name)
            var dirSegments = segments[..^1];

            if (commonSegments is null)
            {
                commonSegments = dirSegments;
                continue;
            }

            var matchLength = 0;
            var limit = Math.Min(commonSegments.Length, dirSegments.Length);
            for (var i = 0; i < limit; i++)
            {
                if (string.Equals(commonSegments[i], dirSegments[i], StringComparison.OrdinalIgnoreCase))
                {
                    matchLength++;
                }
                else
                {
                    break;
                }
            }

            commonSegments = commonSegments[..matchLength];
        }

        if (commonSegments is null || commonSegments.Length == 0)
        {
            return null;
        }

        return string.Join('/', commonSegments);
    }

    private static string ApplyStripPrefix(string sourcePath, string? stripPrefix)
    {
        if (string.IsNullOrEmpty(stripPrefix))
        {
            return sourcePath;
        }

        var prefix = stripPrefix.TrimEnd('/') + "/";
        if (sourcePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return sourcePath[prefix.Length..];
        }

        return sourcePath;
    }

    private static void ExtractFiles(string zipPath, string outputDirectory, List<UnpackFileMapping> mappings)
    {
        using var archive = ZipFile.OpenRead(zipPath);

        // Build a lookup from normalized entry paths to entries
        var entryLookup = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var normalized = entry.FullName.Replace('\\', '/');
            entryLookup.TryAdd(normalized, entry);
        }

        foreach (var mapping in mappings)
        {
            if (!entryLookup.TryGetValue(mapping.SourcePath, out var entry))
            {
                throw new VTrackerException($"ZIP entry '{mapping.SourcePath}' not found in archive '{zipPath}'.");
            }

            var destinationPath = Path.Combine(outputDirectory, mapping.DestinationPath.Replace('/', Path.DirectorySeparatorChar));
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (destinationDir is not null)
            {
                Directory.CreateDirectory(destinationDir);
            }

            entry.ExtractToFile(destinationPath, overwrite: true);

            // Preserve the file timestamp from the ZIP entry
            if (entry.LastWriteTime != default)
            {
                File.SetLastWriteTime(destinationPath, entry.LastWriteTime.DateTime);
            }
        }
    }
}
