namespace VTracker.Core;

public sealed record CompareExportRequest(
    string RightZipPath,
    IReadOnlyList<string> NormalizedPaths,
    string OutputZipPath);

public sealed record CompareExportResult(
    int ExportedFileCount,
    string OutputZipPath);
