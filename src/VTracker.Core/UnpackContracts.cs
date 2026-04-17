namespace VTracker.Core;

public sealed record UnpackRequest(
    string FromPath,
    string? CatalogPath,
    string Category,
    string OutputDirectory,
    string? StripPrefix,
    bool DryRun);

public sealed record UnpackFileMapping(
    string SourcePath,
    string DestinationPath,
    string Category);

public sealed record UnpackResult(
    IReadOnlyList<UnpackFileMapping> Files,
    string? DetectedCommonPrefix);
