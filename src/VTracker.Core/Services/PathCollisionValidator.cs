namespace VTracker.Core;

public sealed class PathCollisionValidator
{
    public void EnsureUnique(IEnumerable<(string NormalizedPath, string SourcePath)> entries)
    {
        var seenPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (normalizedPath, sourcePath) in entries)
        {
            if (seenPaths.TryGetValue(normalizedPath, out var existingPath))
            {
                throw new NormalizedPathCollisionException(normalizedPath, existingPath, sourcePath);
            }

            seenPaths.Add(normalizedPath, sourcePath);
        }
    }
}
