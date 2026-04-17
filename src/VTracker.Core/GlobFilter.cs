using DotNet.Globbing;

namespace VTracker.Core;

/// <summary>
/// Applies glob include-patterns to normalized manifest paths.
/// Patterns are matched case-insensitively using <c>/</c>-separated paths,
/// following the semantics agreed in FR-08.
/// </summary>
public static class GlobFilter
{
    private static readonly GlobOptions MatchOptions = new()
    {
        Evaluation = { CaseInsensitive = true },
    };

    /// <summary>
    /// Returns <c>true</c> when <paramref name="patterns"/> is empty (match-all),
    /// or when at least one pattern matches the <paramref name="normalizedPath"/>.
    /// </summary>
    public static bool MatchesAny(string normalizedPath, IReadOnlyList<string> patterns)
    {
        if (patterns is null || patterns.Count == 0)
        {
            return true;
        }

        foreach (var pattern in patterns)
        {
            if (Glob.Parse(pattern, MatchOptions).IsMatch(normalizedPath))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Filters an array of added-file entries to those whose paths match any of the given patterns.
    /// When <paramref name="patterns"/> is empty, all entries are returned unchanged.
    /// </summary>
    public static CompareAddedFile[] FilterAdded(
        IReadOnlyList<CompareAddedFile> entries,
        IReadOnlyList<string> patterns)
    {
        if (patterns is null || patterns.Count == 0)
        {
            return entries is CompareAddedFile[] arr ? arr : [.. entries];
        }

        return [.. entries.Where(e => MatchesAny(e.Path, patterns))];
    }

    /// <summary>
    /// Filters an array of removed-file entries to those whose paths match any of the given patterns.
    /// When <paramref name="patterns"/> is empty, all entries are returned unchanged.
    /// </summary>
    public static CompareRemovedFile[] FilterRemoved(
        IReadOnlyList<CompareRemovedFile> entries,
        IReadOnlyList<string> patterns)
    {
        if (patterns is null || patterns.Count == 0)
        {
            return entries is CompareRemovedFile[] arr ? arr : [.. entries];
        }

        return [.. entries.Where(e => MatchesAny(e.Path, patterns))];
    }

    /// <summary>
    /// Filters an array of updated-file entries to those whose paths match any of the given patterns.
    /// When <paramref name="patterns"/> is empty, all entries are returned unchanged.
    /// </summary>
    public static CompareUpdatedFile[] FilterUpdated(
        IReadOnlyList<CompareUpdatedFile> entries,
        IReadOnlyList<string> patterns)
    {
        if (patterns is null || patterns.Count == 0)
        {
            return entries is CompareUpdatedFile[] arr ? arr : [.. entries];
        }

        return [.. entries.Where(e => MatchesAny(e.Path, patterns))];
    }
}
