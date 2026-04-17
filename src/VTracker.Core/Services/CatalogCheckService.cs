namespace VTracker.Core;

/// <summary>
/// Checks a catalog file for dead patterns — entries that match zero files
/// in the given manifest.
/// </summary>
public sealed class CatalogCheckService(CatalogParser catalogParser)
{
    /// <summary>
    /// Parses the catalog, evaluates every entry against the manifest file list,
    /// and returns patterns that match no files.
    /// </summary>
    /// <param name="catalogPath">Path to the catalog CSV file.</param>
    /// <param name="manifest">Manifest whose file paths are tested against each catalog entry.</param>
    /// <returns>A result containing all dead (unmatched) catalog entries.</returns>
    public CatalogCheckResult Check(string catalogPath, ManifestDocument manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);
        ArgumentNullException.ThrowIfNull(manifest);

        var catalog = catalogParser.Parse(catalogPath);
        var paths = manifest.Files.Select(f => f.Path).ToArray();

        var deadEntries = new List<CatalogCheckDeadEntry>();

        for (var i = 0; i < catalog.Entries.Count; i++)
        {
            var entry = catalog.Entries[i];
            var matchCount = 0;

            foreach (var path in paths)
            {
                if (entry.IsMatch(path))
                {
                    matchCount++;
                    break; // one match is enough to know it's alive
                }
            }

            if (matchCount == 0)
            {
                deadEntries.Add(new CatalogCheckDeadEntry(
                    RowNumber: i + 2, // +2: 1-based, header is row 1
                    Type: entry.Type,
                    Pattern: entry.Pattern,
                    Category: entry.Category));
            }
        }

        return new CatalogCheckResult(deadEntries);
    }
}

/// <summary>
/// Result of a catalog check operation listing dead patterns.
/// </summary>
/// <param name="DeadEntries">Catalog entries that matched zero files in the manifest.</param>
public sealed record CatalogCheckResult(IReadOnlyList<CatalogCheckDeadEntry> DeadEntries);

/// <summary>
/// A single dead catalog entry that matched no manifest files.
/// </summary>
/// <param name="RowNumber">1-based row number in the CSV (header = row 1).</param>
/// <param name="Type">The matching strategy (glob or regex).</param>
/// <param name="Pattern">The pattern text.</param>
/// <param name="Category">The assigned category.</param>
public sealed record CatalogCheckDeadEntry(int RowNumber, CatalogRowType Type, string Pattern, string Category);
