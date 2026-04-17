using nietras.SeparatedValues;

namespace VTracker.Core;

/// <summary>
/// Writes <see cref="CatalogRow"/> entries to a CSV file using <c>Sep</c>
/// for RFC 4180-compliant output with proper quoting and escaping.
/// </summary>
public sealed class CatalogWriter
{
    /// <summary>
    /// Writes the given catalog rows to a CSV file at <paramref name="outputPath"/>.
    /// The file contains a header row (<c>type,pattern,category</c>) followed by one
    /// data row per entry. Type is encoded as <c>G</c> (glob) or <c>R</c> (regex).
    /// </summary>
    /// <param name="outputPath">Destination file path. Overwrites any existing file.</param>
    /// <param name="rows">Catalog rows to write.</param>
    public void Write(string outputPath, IReadOnlyList<CatalogRow> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(rows);

        var resolvedPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = Sep.New(',')
            .Writer(o => o with { Escape = true })
            .ToFile(resolvedPath);

        foreach (var row in rows)
        {
            using var writeRow = writer.NewRow();
            writeRow["type"].Set(row.Type == CatalogRowType.Glob ? "G" : "R");
            writeRow["pattern"].Set(row.Pattern);
            writeRow["category"].Set(row.Category);
        }
    }
}
