using VTracker.Core;

namespace VTracker.Tests;

public sealed class ManifestComparatorTests
{
    [Fact]
    public void Compare_ClassifiesFileAndProvenanceDifferencesSeparately()
    {
        var comparator = new ManifestComparator(new CatalogClassifier());

        var left = CreateManifest(
            sourcePath: @"D:\releases\release-1\setup.msi",
            sourceHash: "aaaa",
            patches: Array.Empty<ManifestPatchInfo>(),
            files:
            [
                CreateFile("bin/common.dll", "1111", 10, "1.0.0.0"),
                CreateFile("bin/removed.dll", "2222", 20),
            ]);

        var right = CreateManifest(
            sourcePath: @"D:\releases\release-2\setup.msi",
            sourceHash: "bbbb",
            patches:
            [
                new ManifestPatchInfo
                {
                    Sequence = 1,
                    Path = @"D:\releases\release-2\patch.msp",
                    Sha256 = "cccc",
                },
            ],
            files:
            [
                CreateFile("bin/common.dll", "9999", 99, "2.0.0.0"),
                CreateFile("bin/added.dll", "3333", 30),
            ]);

        var result = comparator.Compare(left, right);

        Assert.Equal(["bin/added.dll"], result.Added.Select(a => a.Path).ToArray());
        Assert.Equal(["bin/removed.dll"], result.Removed.Select(r => r.Path).ToArray());
        var updated = Assert.Single(result.Updated);
        Assert.Equal("bin/common.dll", updated.Path);
        Assert.Equal("1111", updated.Left.Sha256);
        Assert.Equal("9999", updated.Right.Sha256);
        Assert.Contains("Source MSI path differs.", result.ProvenanceDifferences);
        Assert.Contains("Source MSI hash differs.", result.ProvenanceDifferences);
        Assert.Contains("Patch list differs.", result.ProvenanceDifferences);
    }

    [Fact]
    public async Task CompareService_RejectsMissingInputsWithFriendlyExceptions()
    {
        var repository = new ManifestRepository(new PathNormalizer(), new PathCollisionValidator());
        var classifier = new CatalogClassifier();
        var service = new CompareService(repository, new ManifestComparator(classifier), new CatalogDiscovery(), new CatalogParser());

        var exception = await Assert.ThrowsAsync<VTrackerException>(
            () => service.CompareAsync(
                new CompareRequest(
                    @"D:\missing-left.json",
                    @"D:\missing-right.json"),
                CancellationToken.None));

        Assert.Contains("Left input", exception.Message);
    }

    [Fact]
    public void Compare_CreatedUtcDifferenceIsNotAProvenanceDifference()
    {
        var comparator = new ManifestComparator(new CatalogClassifier());

        var left = CreateManifest(@"D:\releases\r1\setup.msi", "aaaa", Array.Empty<ManifestPatchInfo>(), []);
        var right = CreateManifest(@"D:\releases\r1\setup.msi", "aaaa", Array.Empty<ManifestPatchInfo>(), []);

        var result = comparator.Compare(left, right);

        Assert.Empty(result.ProvenanceDifferences);
    }

    private static ManifestDocument CreateManifest(string sourcePath, string sourceHash, ManifestPatchInfo[] patches, ManifestFileEntry[] files)
    {
        return new ManifestDocument
        {
            Tool = new ManifestToolInfo
            {
                Name = "vtracker",
                Version = "1.0.0",
            },
            Source = new ManifestSourceInfo
            {
                MsiPath = sourcePath,
                MsiSha256 = sourceHash,
            },
            Patches = patches,
            Extraction = new ManifestExtractionInfo
            {
                Mode = "administrative-image",
                WorkDirKept = false,
                Compression = "Optimal",
                CreatedUtc = DateTime.UtcNow,
            },
            Files = files,
        };
    }

    private static ManifestFileEntry CreateFile(string path, string sha256, long size, string? fileVersion = null)
    {
        return new ManifestFileEntry
        {
            Path = path,
            LastWriteTimeUtc = DateTime.SpecifyKind(new DateTime(2026, 4, 17, 0, 0, 0), DateTimeKind.Utc),
            Size = size,
            Sha256 = sha256,
            FileVersion = fileVersion,
            ProductVersion = fileVersion,
        };
    }
}
