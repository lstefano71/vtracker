using VTracker.Core;

namespace VTracker.Tests;

public sealed class ManifestBuilderTests
{
    [Fact]
    public async Task BuildAsync_SortsEntriesAndComputesHashes()
    {
        using var tempDirectory = new TempDirectory();
        Directory.CreateDirectory(tempDirectory.GetPath("bin"));
        File.WriteAllText(tempDirectory.GetPath(@"bin\Zeta.txt"), "zeta");
        File.WriteAllText(tempDirectory.GetPath(@"bin\alpha.txt"), "alpha");

        var builder = new ManifestBuilder(
            new PathNormalizer(),
            new PathCollisionValidator(),
            new HashService(),
            new PeVersionService(),
            new CatalogClassifier());

        var manifest = await builder.BuildAsync(
            new ManifestBuildRequest(
                tempDirectory.RootPath,
                @"D:\releases\release-123\setup.msi",
                "0123456789abcdef",
                Array.Empty<ManifestPatchInfo>(),
                WorkDirectoryKept: false,
                MaxParallelism: 1,
                new ToolIdentity("vtracker", "1.0.0")),
            CancellationToken.None);

        Assert.Equal(["bin/alpha.txt", "bin/Zeta.txt"], manifest.Files.Select(file => file.Path).ToArray());
        Assert.All(manifest.Files, file => Assert.Matches("^[0-9a-f]{64}$", file.Sha256));
        Assert.All(manifest.Files, file => Assert.Null(file.FileVersion));
        Assert.NotNull(manifest.Extraction.CreatedUtc);
        Assert.Equal(DateTimeKind.Utc, manifest.Extraction.CreatedUtc.Value.Kind);
        Assert.True(manifest.Extraction.CreatedUtc.Value <= DateTime.UtcNow);
    }
}
