namespace VTracker.Core;

public sealed record WorkspacePaths(
    string RootDirectory,
    string ImageDirectory,
    string LogsDirectory,
    bool DeleteOnSuccess);

public sealed class WorkspaceManager
{
    public WorkspacePaths Create(string? requestedRootPath, bool keepWorkDirectory)
    {
        var rootDirectory = string.IsNullOrWhiteSpace(requestedRootPath)
            ? Path.Combine(Path.GetTempPath(), "VTracker", Guid.NewGuid().ToString("N"))
            : Path.GetFullPath(requestedRootPath);

        if (File.Exists(rootDirectory))
        {
            throw new VTrackerException($"Work directory '{rootDirectory}' conflicts with an existing file.");
        }

        if (Directory.Exists(rootDirectory) && Directory.EnumerateFileSystemEntries(rootDirectory).Any())
        {
            throw new VTrackerException($"Work directory '{rootDirectory}' must not already contain files.");
        }

        Directory.CreateDirectory(rootDirectory);

        var imageDirectory = Path.Combine(rootDirectory, "image");
        var logsDirectory = Path.Combine(rootDirectory, "logs");
        Directory.CreateDirectory(imageDirectory);
        Directory.CreateDirectory(logsDirectory);

        return new WorkspacePaths(rootDirectory, imageDirectory, logsDirectory, DeleteOnSuccess: !keepWorkDirectory);
    }

    public void CleanupOnSuccess(WorkspacePaths workspace)
    {
        if (workspace.DeleteOnSuccess && Directory.Exists(workspace.RootDirectory))
        {
            Directory.Delete(workspace.RootDirectory, recursive: true);
        }
    }
}
