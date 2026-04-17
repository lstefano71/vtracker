namespace VTracker.Core;

public sealed record ManifestBuildRequest(
    string ImageRootPath,
    string SourceMsiPath,
    string SourceMsiSha256,
    IReadOnlyList<ManifestPatchInfo> Patches,
    bool WorkDirectoryKept,
    int? MaxParallelism,
    ToolIdentity Tool);

public sealed class ManifestBuilder(
    PathNormalizer pathNormalizer,
    PathCollisionValidator pathCollisionValidator,
    HashService hashService,
    PeVersionService peVersionService)
{
    public async Task<ManifestDocument> BuildAsync(ManifestBuildRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var preparedFiles = PrepareFiles(request.ImageRootPath);
        var maxParallelism = request.MaxParallelism is > 0
            ? request.MaxParallelism.Value
            : Math.Max(2, Environment.ProcessorCount - 1);

        var entries = new ManifestFileEntry[preparedFiles.Length];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, preparedFiles.Length),
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = maxParallelism,
            },
            async (index, ct) =>
            {
                var preparedFile = preparedFiles[index];
                var fileInfo = new FileInfo(preparedFile.PhysicalPath);
                var hash = await hashService.ComputeSha256Async(preparedFile.PhysicalPath, ct);
                var versionInfo = peVersionService.Read(preparedFile.PhysicalPath);

                entries[index] = new ManifestFileEntry
                {
                    Path = preparedFile.ManifestPath,
                    LastWriteTimeUtc = fileInfo.LastWriteTimeUtc,
                    Size = fileInfo.Length,
                    Sha256 = hash,
                    FileVersion = versionInfo.FileVersion,
                    ProductVersion = versionInfo.ProductVersion,
                };
            });

        var orderedFiles = entries
            .OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Path, StringComparer.Ordinal)
            .ToArray();

        return new ManifestDocument
        {
            Tool = new ManifestToolInfo
            {
                Name = request.Tool.Name,
                Version = request.Tool.Version,
            },
            Source = new ManifestSourceInfo
            {
                MsiPath = request.SourceMsiPath,
                MsiSha256 = request.SourceMsiSha256,
            },
            Patches = request.Patches.ToArray(),
            Extraction = new ManifestExtractionInfo
            {
                Mode = "administrative-image",
                WorkDirKept = request.WorkDirectoryKept,
                Compression = "Optimal",
            },
            Files = orderedFiles,
        };
    }

    private PreparedFile[] PrepareFiles(string imageRootPath)
    {
        var filePaths = Directory.EnumerateFiles(imageRootPath, "*", SearchOption.AllDirectories).ToArray();
        var preparedFiles = new PreparedFile[filePaths.Length];

        for (var index = 0; index < filePaths.Length; index++)
        {
            var filePath = filePaths[index];
            preparedFiles[index] = new PreparedFile(
                filePath,
                pathNormalizer.GetRelativeManifestPath(imageRootPath, filePath));
        }

        pathCollisionValidator.EnsureUnique(preparedFiles.Select(file => (file.ManifestPath, file.PhysicalPath)));
        return preparedFiles;
    }

    private sealed record PreparedFile(string PhysicalPath, string ManifestPath);
}
