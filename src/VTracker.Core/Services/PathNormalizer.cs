namespace VTracker.Core;

public sealed class PathNormalizer
{
    public string GetRelativeManifestPath(string rootPath, string filePath)
    {
        var normalizedRootPath = Path.GetFullPath(rootPath);
        var normalizedFilePath = Path.GetFullPath(filePath);
        var relativePath = Path.GetRelativePath(normalizedRootPath, normalizedFilePath);

        if (Path.IsPathRooted(relativePath) || relativePath.StartsWith("..", StringComparison.Ordinal))
        {
            throw new VTrackerException($"File '{normalizedFilePath}' is not contained beneath '{normalizedRootPath}'.");
        }

        return NormalizeRelativePath(relativePath);
    }

    public string NormalizeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new VTrackerException("Relative paths must not be empty.");
        }

        var trimmedPath = path.Trim();
        if (Path.IsPathRooted(trimmedPath))
        {
            throw new VTrackerException($"Path '{path}' must be relative.");
        }

        var slashNormalizedPath = trimmedPath.Replace('\\', '/');
        while (slashNormalizedPath.StartsWith("./", StringComparison.Ordinal))
        {
            slashNormalizedPath = slashNormalizedPath[2..];
        }

        if (slashNormalizedPath.Length == 0)
        {
            throw new VTrackerException("Relative paths must not collapse to empty values.");
        }

        var parts = slashNormalizedPath.Split('/');
        var normalizedParts = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                throw new VTrackerException($"Path '{path}' contains empty segments.");
            }

            if (part == ".")
            {
                throw new VTrackerException($"Path '{path}' contains '.' segments.");
            }

            if (part == "..")
            {
                throw new VTrackerException($"Path '{path}' contains '..' segments.");
            }

            normalizedParts.Add(part);
        }

        return string.Join('/', normalizedParts);
    }
}
