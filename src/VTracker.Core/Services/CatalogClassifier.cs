namespace VTracker.Core;

/// <summary>
/// Classifies normalized file paths against a compiled <see cref="CatalogFile"/>.
/// Evaluation follows file-order first-match-wins semantics.
/// </summary>
public sealed class CatalogClassifier
{
    /// <summary>
    /// The category assigned to paths that match no catalog entry.
    /// </summary>
    public const string UnclassifiedCategory = "Unclassified";

    /// <summary>
    /// Returns the category for a single normalized path.
    /// The first matching entry wins; unmatched paths return <c>"Unclassified"</c>.
    /// </summary>
    public string Classify(CatalogFile catalog, string normalizedPath)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedPath);

        foreach (var entry in catalog.Entries)
        {
            if (entry.IsMatch(normalizedPath))
            {
                return entry.Category;
            }
        }

        return UnclassifiedCategory;
    }

    /// <summary>
    /// Classifies all paths in bulk, returning a dictionary keyed by normalized path.
    /// </summary>
    public Dictionary<string, string> ClassifyAll(CatalogFile catalog, IEnumerable<string> normalizedPaths)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(normalizedPaths);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in normalizedPaths)
        {
            result[path] = Classify(catalog, path);
        }

        return result;
    }
}
