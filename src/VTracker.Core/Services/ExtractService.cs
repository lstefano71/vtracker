namespace VTracker.Core;

public sealed class ExtractService(
    OutputPathResolver outputPathResolver,
    WorkspaceManager workspaceManager,
    MsiexecRunner msiexecRunner,
    HashService hashService,
    ManifestBuilder manifestBuilder,
    ManifestRepository manifestRepository,
    ArchiveBuilder archiveBuilder)
{
    public async Task<ExtractResult> ExtractAsync(
        ExtractRequest request,
        ToolIdentity toolIdentity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(toolIdentity);

        if (request.MaxParallelism is <= 0)
        {
            throw new VTrackerException("maxParallelism must be greater than zero when provided.");
        }

        var msiPath = ValidateInputFile(request.MsiPath, "MSI");
        var patchPaths = request.PatchPaths
            .Select((path, index) => ValidateInputFile(path, $"Patch {index + 1}"))
            .ToArray();

        var outputPaths = outputPathResolver.Resolve(msiPath, request.OutputPath, Environment.CurrentDirectory, request.EmitManifest);
        var workspace = workspaceManager.Create(request.WorkDirectory, request.KeepWorkDirectory);

        var adminLogPath = Path.Combine(workspace.LogsDirectory, "01-admin-image.log");
        await msiexecRunner.CreateAdministrativeImageAsync(msiPath, workspace.ImageDirectory, adminLogPath, cancellationToken);

        var extractedMsiPath = LocateExtractedMsi(msiPath, workspace.ImageDirectory);
        for (var index = 0; index < patchPaths.Length; index++)
        {
            var patchLogPath = Path.Combine(workspace.LogsDirectory, $"02-patch-{index + 1:000}.log");
            await msiexecRunner.ApplyPatchAsync(patchPaths[index], extractedMsiPath, patchLogPath, cancellationToken);
        }

        var sourceHash = await hashService.ComputeSha256Async(msiPath, cancellationToken);
        var patchMetadata = new ManifestPatchInfo[patchPaths.Length];
        for (var index = 0; index < patchPaths.Length; index++)
        {
            patchMetadata[index] = new ManifestPatchInfo
            {
                Sequence = index + 1,
                Path = patchPaths[index],
                Sha256 = await hashService.ComputeSha256Async(patchPaths[index], cancellationToken),
            };
        }

        var manifest = await manifestBuilder.BuildAsync(
            new ManifestBuildRequest(
                workspace.ImageDirectory,
                msiPath,
                sourceHash,
                patchMetadata,
                WorkDirectoryKept: !workspace.DeleteOnSuccess,
                request.MaxParallelism,
                toolIdentity),
            cancellationToken);

        if (outputPaths.StagingManifestPath is not null)
        {
            await manifestRepository.WriteToPathAsync(manifest, outputPaths.StagingManifestPath, cancellationToken);
        }

        await archiveBuilder.CreateAsync(outputPaths.StagingArchivePath, workspace.ImageDirectory, manifest, cancellationToken);

        if (outputPaths.StagingManifestPath is not null && outputPaths.ManifestPath is not null)
        {
            File.Move(outputPaths.StagingManifestPath, outputPaths.ManifestPath);
        }

        File.Move(outputPaths.StagingArchivePath, outputPaths.ArchivePath);

        workspaceManager.CleanupOnSuccess(workspace);

        return new ExtractResult(
            outputPaths.ArchivePath,
            outputPaths.ManifestPath,
            manifest.Files.Length,
            !workspace.DeleteOnSuccess ? workspace.RootDirectory : null,
            !workspace.DeleteOnSuccess);
    }

    private static string LocateExtractedMsi(string sourceMsiPath, string imageRootPath)
    {
        var expectedFileName = Path.GetFileName(sourceMsiPath);
        var rootCandidate = Path.Combine(imageRootPath, expectedFileName);
        if (File.Exists(rootCandidate))
        {
            return rootCandidate;
        }

        var discoveredCandidates = Directory.EnumerateFiles(imageRootPath, "*.msi", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(imageRootPath, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var details = discoveredCandidates.Length == 0
            ? "No MSI files were found beneath the administrative image root."
            : $"Discovered MSI candidates: {string.Join(", ", discoveredCandidates.Select(path => $"'{path}'"))}.";
        throw new InstallerImageDiscoveryException(
            $"Expected to find '{expectedFileName}' directly under '{imageRootPath}' after administrative-image creation. {details}");
    }

    private static string ValidateInputFile(string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new VTrackerException($"{description} path is required.");
        }

        var resolvedPath = Path.GetFullPath(path);
        if (!File.Exists(resolvedPath))
        {
            throw new VTrackerException($"{description} '{resolvedPath}' does not exist.");
        }

        using var stream = new FileStream(
            resolvedPath,
            new FileStreamOptions
            {
                Access = FileAccess.Read,
                Mode = FileMode.Open,
                Share = FileShare.Read,
                Options = FileOptions.SequentialScan,
            });
        return resolvedPath;
    }
}
