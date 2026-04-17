using VTracker.Core;

namespace VTracker.Tests;

public sealed class OutputPathResolverTests
{
    [Fact]
    public void Resolve_UsesTheSourceParentDirectoryNameWhenOutputIsOmitted()
    {
        using var tempDirectory = new TempDirectory();
        var resolver = new OutputPathResolver();

        var outputs = resolver.Resolve(
            @"D:\releases\release-123\setup.msi",
            outputPath: null,
            currentDirectory: tempDirectory.RootPath,
            emitManifest: true);

        Assert.Equal(Path.Combine(tempDirectory.RootPath, "release-123.zip"), outputs.ArchivePath);
        Assert.Equal(Path.Combine(tempDirectory.RootPath, "release-123.manifest.json"), outputs.ManifestPath);
        Assert.StartsWith(tempDirectory.RootPath, outputs.StagingArchivePath, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(outputs.StagingManifestPath);
    }

    [Fact]
    public void Resolve_RejectsNonZipOutputPaths()
    {
        using var tempDirectory = new TempDirectory();
        var resolver = new OutputPathResolver();

        Assert.Throws<VTrackerException>(
            () => resolver.Resolve(
                @"D:\releases\release-123\setup.msi",
                outputPath: Path.Combine(tempDirectory.RootPath, "release-123.json"),
                currentDirectory: tempDirectory.RootPath,
                emitManifest: false));
    }
}
