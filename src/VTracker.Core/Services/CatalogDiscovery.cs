namespace VTracker.Core;

/// <summary>
/// Resolves the effective catalog file path from explicit user input or
/// automatic discovery of <c>vtracker.catalog.csv</c> in the working directory.
/// </summary>
public sealed class CatalogDiscovery
{
    /// <summary>
    /// Well-known catalog file name used for auto-discovery.
    /// </summary>
    public const string DefaultCatalogFileName = "vtracker.catalog.csv";

    /// <summary>
    /// Resolves the catalog path to use.
    /// </summary>
    /// <param name="explicitPath">
    /// Explicit <c>--catalog</c> value from the user, or <c>null</c> if omitted.
    /// </param>
    /// <param name="workingDirectory">
    /// Directory to search for the default catalog file when <paramref name="explicitPath"/> is <c>null</c>.
    /// </param>
    /// <returns>
    /// The resolved absolute path to the catalog file, or <c>null</c> when no catalog is active.
    /// </returns>
    public string? Resolve(string? explicitPath, string workingDirectory)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var resolvedPath = Path.GetFullPath(explicitPath);
            if (!File.Exists(resolvedPath))
            {
                throw new VTrackerException($"Catalog file '{resolvedPath}' does not exist.");
            }

            return resolvedPath;
        }

        var autoPath = Path.Combine(workingDirectory, DefaultCatalogFileName);
        return File.Exists(autoPath) ? Path.GetFullPath(autoPath) : null;
    }
}
