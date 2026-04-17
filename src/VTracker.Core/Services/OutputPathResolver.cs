namespace VTracker.Core;

public sealed record OutputArtifactPaths(
    string ArchivePath,
    string? ManifestPath,
    string StagingArchivePath,
    string? StagingManifestPath);

public sealed class OutputPathResolver
{
    public OutputArtifactPaths Resolve(string msiPath, string? outputPath, string currentDirectory, bool emitManifest)
    {
        var archivePath = ResolveArchivePath(msiPath, outputPath, currentDirectory);
        var manifestPath = emitManifest ? ResolveManifestPath(archivePath) : null;

        EnsureDestinationAvailable(archivePath, "Archive");
        if (manifestPath is not null)
        {
            EnsureDestinationAvailable(manifestPath, "Manifest");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
        if (manifestPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        }

        return new OutputArtifactPaths(
            archivePath,
            manifestPath,
            CreateStagingPath(archivePath),
            manifestPath is null ? null : CreateStagingPath(manifestPath));
    }

    public string ResolveArchivePath(string msiPath, string? outputPath, string currentDirectory)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            var resolvedOutputPath = Path.GetFullPath(outputPath, Path.GetFullPath(currentDirectory));
            if (!string.Equals(Path.GetExtension(resolvedOutputPath), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                throw new VTrackerException($"Output archive '{resolvedOutputPath}' must use a '.zip' extension.");
            }

            return resolvedOutputPath;
        }

        var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(msiPath));
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            throw new VTrackerException($"Unable to derive a default output name from '{msiPath}'.");
        }

        var releaseName = new DirectoryInfo(sourceDirectory).Name;
        if (string.IsNullOrWhiteSpace(releaseName))
        {
            throw new VTrackerException($"Unable to derive a default output name from '{msiPath}'.");
        }

        return Path.Combine(Path.GetFullPath(currentDirectory), $"{releaseName}.zip");
    }

    public string ResolveManifestPath(string archivePath)
    {
        var directory = Path.GetDirectoryName(archivePath)!;
        var baseName = Path.GetFileNameWithoutExtension(archivePath);
        return Path.Combine(directory, $"{baseName}.manifest.json");
    }

    private static void EnsureDestinationAvailable(string path, string description)
    {
        if (File.Exists(path))
        {
            throw new VTrackerException($"{description} output '{path}' already exists.");
        }

        if (Directory.Exists(path))
        {
            throw new VTrackerException($"{description} output '{path}' conflicts with an existing directory.");
        }
    }

    private static string CreateStagingPath(string finalPath)
    {
        return Path.Combine(
            Path.GetDirectoryName(finalPath)!,
            $"{Path.GetFileName(finalPath)}.{Guid.NewGuid():N}.tmp");
    }
}
