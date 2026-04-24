using System.IO.Compression;
using VTracker.Core;

namespace VTracker.Tests;

public sealed class CompareExportServiceTests
{
    private readonly CompareExportService _service = new();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Creates a minimal ZIP in a temp directory and returns its path.</summary>
    private static string CreateSourceZip(TempDirectory temp, string zipName, IEnumerable<(string EntryName, string Content)> entries)
    {
        var zipPath = temp.GetPath(zipName);
        using var stream = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
        foreach (var (entryName, content) in entries)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream);
            writer.Write(content);
        }

        return zipPath;
    }

    /// <summary>Returns all entry names (normalized) from a ZIP at <paramref name="zipPath"/>.</summary>
    private static IReadOnlyList<string> ReadEntryNames(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        return [.. archive.Entries.Select(e => e.FullName.Replace('\\', '/').Trim('/'))];
    }

    /// <summary>Returns the text content of a ZIP entry by normalized path.</summary>
    private static string ReadEntryContent(string zipPath, string normalizedPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.Entries.FirstOrDefault(e =>
            string.Equals(e.FullName.Replace('\\', '/').Trim('/'), normalizedPath, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Entry '{normalizedPath}' not found.");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // -------------------------------------------------------------------------
    // Happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExportAsync_CopiesRequestedEntriesToOutputZip()
    {
        using var temp = new TempDirectory();

        var sourceZip = CreateSourceZip(temp, "source.zip",
        [
            ("bin/alpha.dll", "alpha"),
            ("bin/beta.dll", "beta"),
            ("readme.txt", "readme"),
        ]);

        var outputZip = temp.GetPath("export.zip");

        var result = await _service.ExportAsync(
            sourceZip,
            ["bin/alpha.dll", "bin/beta.dll"],
            outputZip,
            CancellationToken.None);

        Assert.Equal(2, result.ExportedFileCount);
        Assert.Equal(Path.GetFullPath(outputZip), result.OutputZipPath);

        var entries = ReadEntryNames(result.OutputZipPath);
        Assert.Contains("bin/alpha.dll", entries);
        Assert.Contains("bin/beta.dll", entries);
        Assert.DoesNotContain("readme.txt", entries);
    }

    [Fact]
    public async Task ExportAsync_PreservesFileContent()
    {
        using var temp = new TempDirectory();

        var sourceZip = CreateSourceZip(temp, "source.zip",
        [
            ("bin/alpha.dll", "alpha-content-12345"),
        ]);

        var outputZip = temp.GetPath("export.zip");

        await _service.ExportAsync(sourceZip, ["bin/alpha.dll"], outputZip, CancellationToken.None);

        Assert.Equal("alpha-content-12345", ReadEntryContent(outputZip, "bin/alpha.dll"));
    }

    [Fact]
    public async Task ExportAsync_ZeroPathsProducesEmptyZip()
    {
        using var temp = new TempDirectory();

        var sourceZip = CreateSourceZip(temp, "source.zip",
        [
            ("bin/alpha.dll", "alpha"),
        ]);

        var outputZip = temp.GetPath("export.zip");

        var result = await _service.ExportAsync(sourceZip, [], outputZip, CancellationToken.None);

        Assert.Equal(0, result.ExportedFileCount);
        Assert.True(File.Exists(outputZip), "Output ZIP should exist even when empty.");
        Assert.Empty(ReadEntryNames(outputZip));
    }

    [Fact]
    public async Task ExportAsync_OutputPathIsResolvedToAbsolute()
    {
        using var temp = new TempDirectory();

        var sourceZip = CreateSourceZip(temp, "source.zip",
        [
            ("bin/alpha.dll", "alpha"),
        ]);

        var relativeName = Path.GetFileName(temp.GetPath("export.zip"));
        var originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = temp.RootPath;
            var result = await _service.ExportAsync(sourceZip, ["bin/alpha.dll"], relativeName, CancellationToken.None);
            Assert.True(Path.IsPathFullyQualified(result.OutputZipPath));
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }

    [Fact]
    public async Task ExportAsync_CreatesParentDirectoryWhenMissing()
    {
        using var temp = new TempDirectory();

        var sourceZip = CreateSourceZip(temp, "source.zip",
        [
            ("bin/alpha.dll", "alpha"),
        ]);

        var outputZip = temp.GetPath(@"nested\deep\export.zip");

        await _service.ExportAsync(sourceZip, ["bin/alpha.dll"], outputZip, CancellationToken.None);

        Assert.True(File.Exists(outputZip));
    }

    // -------------------------------------------------------------------------
    // Failure: right input is not a ZIP
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExportAsync_ThrowsWhenRightIsJson()
    {
        using var temp = new TempDirectory();

        var jsonPath = temp.GetPath("manifest.json");
        File.WriteAllText(jsonPath, "{}");
        var outputZip = temp.GetPath("export.zip");

        var ex = await Assert.ThrowsAsync<VTrackerException>(() =>
            _service.ExportAsync(jsonPath, [], outputZip, CancellationToken.None));

        Assert.Contains(".zip", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Failure: output already exists
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExportAsync_ThrowsWhenOutputAlreadyExists()
    {
        using var temp = new TempDirectory();

        var sourceZip = CreateSourceZip(temp, "source.zip", [("bin/alpha.dll", "alpha")]);
        var outputZip = temp.GetPath("export.zip");
        File.WriteAllText(outputZip, "existing");

        await Assert.ThrowsAsync<VTrackerException>(() =>
            _service.ExportAsync(sourceZip, [], outputZip, CancellationToken.None));
    }

    // -------------------------------------------------------------------------
    // Failure: output extension is not .zip
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExportAsync_ThrowsWhenOutputExtensionIsNotZip()
    {
        using var temp = new TempDirectory();

        var sourceZip = CreateSourceZip(temp, "source.zip", [("bin/alpha.dll", "alpha")]);
        var outputPath = temp.GetPath("export.tar");

        var ex = await Assert.ThrowsAsync<VTrackerException>(() =>
            _service.ExportAsync(sourceZip, [], outputPath, CancellationToken.None));

        Assert.Contains(".zip", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Failure: path not found in source ZIP
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExportAsync_ThrowsWhenPathNotInSourceZip()
    {
        using var temp = new TempDirectory();

        var sourceZip = CreateSourceZip(temp, "source.zip", [("bin/alpha.dll", "alpha")]);
        var outputZip = temp.GetPath("export.zip");

        var ex = await Assert.ThrowsAsync<VTrackerException>(() =>
            _service.ExportAsync(sourceZip, ["bin/missing.dll"], outputZip, CancellationToken.None));

        Assert.Contains("bin/missing.dll", ex.Message);
    }

    [Fact]
    public async Task ExportAsync_DeletesPartialOutputOnFailure()
    {
        using var temp = new TempDirectory();

        // source ZIP with only alpha.dll; requesting beta.dll triggers fail-fast
        var sourceZip = CreateSourceZip(temp, "source.zip", [("bin/alpha.dll", "alpha")]);
        var outputZip = temp.GetPath("export.zip");

        await Assert.ThrowsAsync<VTrackerException>(() =>
            _service.ExportAsync(sourceZip, ["bin/alpha.dll", "bin/beta.dll"], outputZip, CancellationToken.None));

        Assert.False(File.Exists(outputZip), "Partial output ZIP must be deleted on failure.");
    }

    // -------------------------------------------------------------------------
    // Failure: ambiguous entries (collision in right ZIP)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExportAsync_ThrowsOnAmbiguousEntriesInSourceZip()
    {
        using var temp = new TempDirectory();

        // Manually craft a ZIP with two entries that normalize to the same path
        var sourceZip = temp.GetPath("source.zip");
        using (var stream = new FileStream(sourceZip, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
        {
            // Close each entry stream before opening the next — ZipArchive requires this
            archive.CreateEntry("bin/Alpha.dll").Open().Dispose();
            archive.CreateEntry("bin/alpha.dll").Open().Dispose();
        }

        var outputZip = temp.GetPath("export.zip");

        await Assert.ThrowsAsync<VTrackerException>(() =>
            _service.ExportAsync(sourceZip, ["bin/alpha.dll"], outputZip, CancellationToken.None));
    }

    // -------------------------------------------------------------------------
    // Case-insensitive path matching
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExportAsync_MatchesEntriesCaseInsensitively()
    {
        using var temp = new TempDirectory();

        // entry stored as "Bin/Alpha.DLL" in the ZIP
        var sourceZip = CreateSourceZip(temp, "source.zip", [("Bin/Alpha.DLL", "alpha")]);
        var outputZip = temp.GetPath("export.zip");

        // request with all-lowercase path
        var result = await _service.ExportAsync(sourceZip, ["bin/alpha.dll"], outputZip, CancellationToken.None);

        Assert.Equal(1, result.ExportedFileCount);
    }
}
