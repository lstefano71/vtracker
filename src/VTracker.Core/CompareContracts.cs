namespace VTracker.Core;

public sealed class CompareResult
{
    public required CompareSummary Summary { get; init; }

    public required string[] Added { get; init; }

    public required string[] Removed { get; init; }

    public required CompareUpdatedFile[] Updated { get; init; }

    public required string[] ProvenanceDifferences { get; init; }
}

public sealed class CompareSummary
{
    public int Added { get; init; }

    public int Removed { get; init; }

    public int Updated { get; init; }

    public int ProvenanceDifferences { get; init; }
}

public sealed class CompareUpdatedFile
{
    public required string Path { get; init; }

    public required CompareFileSnapshot Left { get; init; }

    public required CompareFileSnapshot Right { get; init; }
}

public sealed class CompareFileSnapshot
{
    public required string Sha256 { get; init; }

    public long Size { get; init; }

    public string? FileVersion { get; init; }

    public string? ProductVersion { get; init; }
}
