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

    /// <summary>
    /// When the archive was created. Informational only; absent in manifests produced
    /// before FR-07 and treated as <c>null</c> rather than a hard error.
    /// </summary>
    public DateTime? CreatedUtc { get; init; }
}

public sealed class ManifestFileEntry
{
    public required string Path { get; init; }

    /// <summary>
    /// File last-write time in UTC. Informational only; <c>null</c> when absent in
    /// older manifests. Never used as an identity or update signal.
    /// </summary>
    public DateTime? LastWriteTimeUtc { get; init; }

    public long Size { get; init; }

    public required string Sha256 { get; init; }

    public string? FileVersion { get; init; }

    public string? ProductVersion { get; init; }

    /// <summary>
    /// Catalog-assigned category. Present only in schema-version-2 manifests produced
    /// with <c>--catalog</c>. <c>null</c> when no catalog was active or in v1 manifests.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Category { get; init; }
}
