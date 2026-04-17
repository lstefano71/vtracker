namespace VTracker.Core;

public sealed class ManifestDocument
{
    public int SchemaVersion { get; init; } = 1;

    public required ManifestToolInfo Tool { get; init; }

    public required ManifestSourceInfo Source { get; init; }

    public required ManifestPatchInfo[] Patches { get; init; }

    public required ManifestExtractionInfo Extraction { get; init; }

    public required ManifestFileEntry[] Files { get; init; }
}

public sealed class ManifestToolInfo
{
    public required string Name { get; init; }

    public required string Version { get; init; }
}

public sealed class ManifestSourceInfo
{
    public required string MsiPath { get; init; }

    public required string MsiSha256 { get; init; }
}

public sealed class ManifestPatchInfo
{
    public int Sequence { get; init; }

    public required string Path { get; init; }

    public required string Sha256 { get; init; }
}

public sealed class ManifestExtractionInfo
{
    public required string Mode { get; init; }

    public bool WorkDirKept { get; init; }

    public required string Compression { get; init; }
}

public sealed class ManifestFileEntry
{
    public required string Path { get; init; }

    public required DateTime LastWriteTimeUtc { get; init; }

    public long Size { get; init; }

    public required string Sha256 { get; init; }

    public string? FileVersion { get; init; }

    public string? ProductVersion { get; init; }
}
