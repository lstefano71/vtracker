using System.IO.Compression;
using VTracker.Core;

namespace VTracker.Tests;

public sealed class ArchiveBuilderTests
{
    [Fact]
    public async Task CreateAsync_WritesFilesAndEmbeddedManifest()
    {
        using var tempDirectory = new TempDirectory();
        Directory.CreateDirectory(tempDirectory.GetPath("bin"));
        var filePath = tempDirectory.GetPath(@"bin\alpha.txt");
        File.WriteAllText(filePath, "alpha");

        var hashService = new HashService();
        var repository = new ManifestRepository(new PathNormalizer(), new PathCollisionValidator());
        var archiveBuilder = new ArchiveBuilder(repository);
        var archivePath = tempDirectory.GetPath("release.zip");

        var manifest = new ManifestDocument
        {
            Tool = new ManifestToolInfo
            {
                Name = "vtracker",
                Version = "1.0.0",
            },
            Source = new ManifestSourceInfo
            {
                MsiPath = @"D:\releases\release-123\setup.msi",
                MsiSha256 = "aaaa",
            },
            Patches = Array.Empty<ManifestPatchInfo>(),
            Extraction = new ManifestExtractionInfo
            {
                Mode = "administrative-image",
                WorkDirKept = false,
                Compression = "Optimal",
            },
            Files =
            [
                new ManifestFileEntry
                {
                    Path = "bin/alpha.txt",
                    LastWriteTimeUtc = File.GetLastWriteTimeUtc(filePath),
                    Size = new FileInfo(filePath).Length,
                    Sha256 = await hashService.ComputeSha256Async(filePath, CancellationToken.None),
                    FileVersion = null,
                    ProductVersion = null,
                },
            ],
        };

        await archiveBuilder.CreateAsync(archivePath, tempDirectory.RootPath, manifest, CancellationToken.None);

        using var archive = ZipFile.OpenRead(archivePath);
        Assert.Contains(archive.Entries, entry => entry.FullName == "bin/alpha.txt");
        Assert.Contains(archive.Entries, entry => entry.FullName == "_manifest.json");

        var roundTrippedManifest = await repository.LoadFromPathAsync(archivePath, CancellationToken.None);
        Assert.Equal("bin/alpha.txt", Assert.Single(roundTrippedManifest.Files).Path);
    }
}
