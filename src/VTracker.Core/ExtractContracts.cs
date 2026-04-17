namespace VTracker.Core;

public sealed record ToolIdentity(string Name, string Version);

public enum OutputFormat
{
    Text,
    Json,
}

public sealed record ExtractRequest(
    string MsiPath,
    IReadOnlyList<string> PatchPaths,
    string? OutputPath,
    string? WorkDirectory,
    bool KeepWorkDirectory,
    bool EmitManifest,
    int? MaxParallelism);

public sealed record ExtractResult(
    string ArchivePath,
    string? ManifestPath,
    int FileCount,
    string? WorkDirectoryPath,
    bool WorkDirectoryKept);

public sealed record CompareRequest(
    string LeftPath,
    string RightPath,
    OutputFormat Format);
