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

        var logsDir = tempDirectory.GetPath("logs");
        Directory.CreateDirectory(logsDir);
        File.WriteAllText(Path.Combine(logsDir, "01-admin-image.log"), "log content");

        var hashService = new HashService();
        var repository = new ManifestRepository(new PathNormalizer(), new PathCollisionValidator());
        var archiveBuilder = new ArchiveBuilder(repository);
        var archivePath = tempDirectory.GetPath("release.zip");

        var manifest = BuildManifest(filePath, await hashService.ComputeSha256Async(filePath, CancellationToken.None));

        await archiveBuilder.CreateAsync(archivePath, tempDirectory.RootPath, logsDir, manifest, CancellationToken.None);

        using var archive = ZipFile.OpenRead(archivePath);
        Assert.Contains(archive.Entries, entry => entry.FullName == "bin/alpha.txt");
        Assert.Contains(archive.Entries, entry => entry.FullName == "_manifest.json");

        var roundTrippedManifest = await repository.LoadFromPathAsync(archivePath, CancellationToken.None);
        Assert.Equal("bin/alpha.txt", Assert.Single(roundTrippedManifest.Files).Path);
    }

    [Fact]
    public async Task CreateAsync_IncludesLogsUnderReservedPrefix()
    {
        using var tempDirectory = new TempDirectory();
        Directory.CreateDirectory(tempDirectory.GetPath("bin"));
        var filePath = tempDirectory.GetPath(@"bin\alpha.txt");
        File.WriteAllText(filePath, "alpha");

        var logsDir = tempDirectory.GetPath("logs");
        Directory.CreateDirectory(logsDir);
        File.WriteAllText(Path.Combine(logsDir, "01-admin-image.log"), "admin log");
        File.WriteAllText(Path.Combine(logsDir, "02-patch-001.log"), "patch log");

        var hashService = new HashService();
        var repository = new ManifestRepository(new PathNormalizer(), new PathCollisionValidator());
        var archiveBuilder = new ArchiveBuilder(repository);
        var archivePath = tempDirectory.GetPath("release.zip");

        var manifest = BuildManifest(filePath, await hashService.ComputeSha256Async(filePath, CancellationToken.None));

        await archiveBuilder.CreateAsync(archivePath, tempDirectory.RootPath, logsDir, manifest, CancellationToken.None);

        using var archive = ZipFile.OpenRead(archivePath);
        Assert.Contains(archive.Entries, entry => entry.FullName == "_logs/01-admin-image.log");
        Assert.Contains(archive.Entries, entry => entry.FullName == "_logs/02-patch-001.log");
    }

    [Fact]
    public async Task CreateAsync_LogsAreNotInManifestFileEntries()
    {
        using var tempDirectory = new TempDirectory();
        Directory.CreateDirectory(tempDirectory.GetPath("bin"));
        var filePath = tempDirectory.GetPath(@"bin\alpha.txt");
        File.WriteAllText(filePath, "alpha");

        var logsDir = tempDirectory.GetPath("logs");
        Directory.CreateDirectory(logsDir);
        File.WriteAllText(Path.Combine(logsDir, "01-admin-image.log"), "admin log");

        var hashService = new HashService();
        var repository = new ManifestRepository(new PathNormalizer(), new PathCollisionValidator());
        var archiveBuilder = new ArchiveBuilder(repository);
        var archivePath = tempDirectory.GetPath("release.zip");

        var manifest = BuildManifest(filePath, await hashService.ComputeSha256Async(filePath, CancellationToken.None));

        await archiveBuilder.CreateAsync(archivePath, tempDirectory.RootPath, logsDir, manifest, CancellationToken.None);

        var roundTripped = await repository.LoadFromPathAsync(archivePath, CancellationToken.None);
        Assert.DoesNotContain(roundTripped.Files, f => f.Path.StartsWith("_logs/", StringComparison.OrdinalIgnoreCase));
    }

    private static ManifestDocument BuildManifest(string filePath, string sha256) =>
        new()
        {
            Tool = new ManifestToolInfo { Name = "vtracker", Version = "1.0.0" },
            Source = new ManifestSourceInfo { MsiPath = @"D:\releases\release-123\setup.msi", MsiSha256 = "aaaa" },
            Patches = Array.Empty<ManifestPatchInfo>(),
            Extraction = new ManifestExtractionInfo
            {
                Mode = "administrative-image",
                WorkDirKept = false,
                Compression = "Optimal",
                CreatedUtc = DateTime.UtcNow,
            },
            Files =
            [
                new ManifestFileEntry
                {
                    Path = "bin/alpha.txt",
                    LastWriteTimeUtc = File.GetLastWriteTimeUtc(filePath),
                    Size = new FileInfo(filePath).Length,
                    Sha256 = sha256,
                    FileVersion = null,
                    ProductVersion = null,
                },
            ],
        };
}
