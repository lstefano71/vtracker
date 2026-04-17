namespace VTracker.Core;

public sealed class CompareResult
{
    public required CompareSummary Summary { get; init; }

    public required CompareAddedFile[] Added { get; init; }

    public required CompareRemovedFile[] Removed { get; init; }

    public required CompareUpdatedFile[] Updated { get; init; }

    public required string[] ProvenanceDifferences { get; init; }
}

public sealed class CompareSummary
{
    public int Added { get; init; }

    public int Removed { get; init; }

    public int Updated { get; init; }

    public int ProvenanceDifferences { get; init; }

    /// <summary>
    /// Per-category breakdown of changes. <c>null</c> when no catalog is active;
    /// omitted from JSON output via <see cref="System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull"/>.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CompareCategoryBreakdown[]? CategoryBreakdown { get; init; }
}

public sealed class CompareAddedFile
{
    public required string Path { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Category { get; init; }
}

public sealed class CompareRemovedFile
{
    public required string Path { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Category { get; init; }
}

public sealed class CompareUpdatedFile
{
    public required string Path { get; init; }

    public required CompareFileSnapshot Left { get; init; }

    public required CompareFileSnapshot Right { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Category { get; init; }
}

public sealed class CompareFileSnapshot
{
    public required string Sha256 { get; init; }

    public long Size { get; init; }

    public string? FileVersion { get; init; }

    public string? ProductVersion { get; init; }
}

public sealed class CompareCategoryBreakdown
{
    public required string Category { get; init; }

    public int Added { get; init; }

    public int Removed { get; init; }

    public int Updated { get; init; }
}
