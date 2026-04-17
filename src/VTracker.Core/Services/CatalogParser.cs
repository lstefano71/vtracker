using nietras.SeparatedValues;

namespace VTracker.Core;

/// <summary>
/// Reads a catalog CSV file using <c>Sep</c> for RFC 4180-compliant parsing,
/// validates the structure, and compiles all entries for use by <see cref="CatalogClassifier"/>.
/// </summary>
public sealed class CatalogParser
{
    /// <summary>
    /// Parses a catalog CSV file and returns a fully compiled <see cref="CatalogFile"/>.
    /// </summary>
    public CatalogFile Parse(string path)
    {
        var resolvedPath = Path.GetFullPath(path);
        if (!File.Exists(resolvedPath))
        {
            throw new VTrackerException($"Catalog file '{resolvedPath}' does not exist.");
        }

        var rows = ReadRows(resolvedPath);
        var compiled = new CompiledCatalogEntry[rows.Count];

        for (var i = 0; i < rows.Count; i++)
        {
            try
            {
                compiled[i] = CompiledCatalogEntry.Compile(rows[i]);
            }
            catch (Exception ex) when (ex is not VTrackerException)
            {
                throw new VTrackerException(
                    $"Catalog row {i + 2} (pattern '{rows[i].Pattern}'): {ex.Message}");
            }
        }

        return new CatalogFile(resolvedPath, compiled);
    }

    /// <summary>
    /// Parses raw catalog rows without compiling them (useful for catalog management commands).
    /// </summary>
    public IReadOnlyList<CatalogRow> ParseRows(string path)
    {
        var resolvedPath = Path.GetFullPath(path);
        if (!File.Exists(resolvedPath))
        {
            throw new VTrackerException($"Catalog file '{resolvedPath}' does not exist.");
        }

        return ReadRows(resolvedPath);
    }

    private static List<CatalogRow> ReadRows(string resolvedPath)
    {
        using var reader = Sep.New(',').Reader(o => o with { Unescape = true }).FromFile(resolvedPath);

        if (!reader.Header.NamesStartingWith("type").Any() ||
            !reader.Header.NamesStartingWith("pattern").Any() ||
            !reader.Header.NamesStartingWith("category").Any())
        {
            throw new VTrackerException(
                $"Catalog file '{resolvedPath}' must have a header row with columns: type, pattern, category.");
        }

        var typeIndex = reader.Header.IndexOf("type");
        var patternIndex = reader.Header.IndexOf("pattern");
        var categoryIndex = reader.Header.IndexOf("category");

        var rows = new List<CatalogRow>();
        var rowNumber = 1; // header is row 0
        foreach (var row in reader)
        {
            rowNumber++;
            var typeValue = row[typeIndex].ToString().Trim();
            var pattern = row[patternIndex].ToString();
            var category = row[categoryIndex].ToString().Trim();

            if (string.IsNullOrWhiteSpace(typeValue) && string.IsNullOrWhiteSpace(pattern) && string.IsNullOrWhiteSpace(category))
            {
                continue; // skip blank rows
            }

            var rowType = typeValue.ToUpperInvariant() switch
            {
                "G" => CatalogRowType.Glob,
                "R" => CatalogRowType.Regex,
                _ => throw new VTrackerException(
                    $"Catalog row {rowNumber}: type '{typeValue}' is not valid. Must be 'G' (glob) or 'R' (regex)."),
            };

            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new VTrackerException($"Catalog row {rowNumber}: pattern must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(category))
            {
                throw new VTrackerException($"Catalog row {rowNumber}: category must not be empty.");
            }

            rows.Add(new CatalogRow(rowType, pattern, category));
        }

        return rows;
    }
}
