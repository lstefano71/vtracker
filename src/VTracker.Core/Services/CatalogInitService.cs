namespace VTracker.Core;

/// <summary>
/// Generates an initial catalog CSV from a manifest by creating one glob row
/// per file path, all assigned to the <c>Unclassified</c> category.
/// </summary>
public sealed class CatalogInitService(CatalogWriter catalogWriter)
{
    /// <summary>
    /// Creates a catalog CSV at <paramref name="outputPath"/> with one exact-path
    /// glob row per file in the manifest, sorted by normalized path, all categorised
    /// as <see cref="CatalogClassifier.UnclassifiedCategory"/>.
    /// </summary>
    /// <param name="manifest">Source manifest whose file entries seed the catalog.</param>
    /// <param name="outputPath">Destination CSV file path.</param>
    public void Init(ManifestDocument manifest, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var rows = manifest.Files
            .Select(f => f.Path)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p, StringComparer.Ordinal)
            .Select(p => new CatalogRow(CatalogRowType.Glob, p, CatalogClassifier.UnclassifiedCategory))
            .ToList();

        catalogWriter.Write(outputPath, rows);
    }
}
