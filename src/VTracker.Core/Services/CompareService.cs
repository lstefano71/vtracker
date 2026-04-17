namespace VTracker.Core;

public sealed class CompareService(
    ManifestRepository manifestRepository,
    ManifestComparator manifestComparator,
    CatalogDiscovery catalogDiscovery,
    CatalogParser catalogParser)
{
    public async Task<CompareResult> CompareAsync(CompareRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var leftPath = ValidateComparisonInput(request.LeftPath, "Left");
        var rightPath = ValidateComparisonInput(request.RightPath, "Right");

        var leftManifest = await manifestRepository.LoadFromPathAsync(leftPath, cancellationToken);
        var rightManifest = await manifestRepository.LoadFromPathAsync(rightPath, cancellationToken);

        var catalogPath = catalogDiscovery.Resolve(request.CatalogPath, Environment.CurrentDirectory);
        CatalogFile? catalog = catalogPath is not null ? catalogParser.Parse(catalogPath) : null;

        return manifestComparator.Compare(leftManifest, rightManifest, catalog);
    }

    private static string ValidateComparisonInput(string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new VTrackerException($"{description} input path is required.");
        }

        var resolvedPath = Path.GetFullPath(path);
        if (!File.Exists(resolvedPath))
        {
            throw new VTrackerException($"{description} input '{resolvedPath}' does not exist.");
        }

        var extension = Path.GetExtension(resolvedPath);
        if (!string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new VTrackerException($"{description} input '{resolvedPath}' must be a '.zip' archive or a '.json' manifest.");
        }

        return resolvedPath;
    }
}
