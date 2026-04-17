using System.Text.RegularExpressions;
using DotNet.Globbing;

namespace VTracker.Core;

/// <summary>
/// Discriminates the matching strategy for a catalog row.
/// </summary>
public enum CatalogRowType
{
    /// <summary>Glob pattern matched via <c>DotNet.Glob</c> with case-insensitive evaluation.</summary>
    Glob,

    /// <summary>
    /// Regex pattern matched via <see cref="Regex"/> with
    /// <c>RegexOptions.IgnoreCase | RegexOptions.NonBacktracking</c>.
    /// </summary>
    Regex,
}

/// <summary>
/// A raw catalog row as parsed from the CSV file before compilation.
/// </summary>
public sealed record CatalogRow(CatalogRowType Type, string Pattern, string Category);

/// <summary>
/// A compiled catalog entry ready for matching. Created once at load time so that
/// glob/regex compilation is not repeated on every file evaluation.
/// </summary>
public sealed class CompiledCatalogEntry
{
    private readonly Glob? _glob;
    private readonly Regex? _regex;

    private static readonly GlobOptions GlobMatchOptions = new()
    {
        Evaluation = { CaseInsensitive = true },
    };

    private CompiledCatalogEntry(CatalogRowType type, string pattern, string category, Glob? glob, Regex? regex)
    {
        Type = type;
        Pattern = pattern;
        Category = category;
        _glob = glob;
        _regex = regex;
    }

    public CatalogRowType Type { get; }

    public string Pattern { get; }

    public string Category { get; }

    /// <summary>
    /// Returns <c>true</c> when the given normalized path matches this entry.
    /// </summary>
    public bool IsMatch(string normalizedPath)
    {
        return Type switch
        {
            CatalogRowType.Glob => _glob!.IsMatch(normalizedPath),
            CatalogRowType.Regex => _regex!.IsMatch(normalizedPath),
            _ => false,
        };
    }

    /// <summary>
    /// Compiles a <see cref="CatalogRow"/> into a ready-to-match entry.
    /// </summary>
    public static CompiledCatalogEntry Compile(CatalogRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.Type switch
        {
            CatalogRowType.Glob => new CompiledCatalogEntry(
                row.Type,
                row.Pattern,
                row.Category,
                glob: Glob.Parse(row.Pattern, GlobMatchOptions),
                regex: null),

            CatalogRowType.Regex => new CompiledCatalogEntry(
                row.Type,
                row.Pattern,
                row.Category,
                glob: null,
                regex: new Regex(row.Pattern, RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)),

            _ => throw new VTrackerException($"Unsupported catalog row type '{row.Type}'."),
        };
    }
}

/// <summary>
/// Represents a fully loaded and compiled catalog file ready for classification.
/// </summary>
public sealed class CatalogFile
{
    public CatalogFile(string sourcePath, IReadOnlyList<CompiledCatalogEntry> entries)
    {
        SourcePath = sourcePath;
        Entries = entries;
    }

    /// <summary>Path the catalog was loaded from.</summary>
    public string SourcePath { get; }

    /// <summary>Compiled entries in file order (first-match-wins).</summary>
    public IReadOnlyList<CompiledCatalogEntry> Entries { get; }
}
