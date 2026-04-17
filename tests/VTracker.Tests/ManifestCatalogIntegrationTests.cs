using System.Text.Json;
using VTracker.Core;

namespace VTracker.Tests;

public sealed class ManifestCatalogIntegrationTests
{
    private static CatalogFile BuildCatalog(params CatalogRow[] rows)
    {
        var entries = rows.Select(CompiledCatalogEntry.Compile).ToArray();
        return new CatalogFile("test.csv", entries);
    }

    private static ManifestDocument CreateManifest(
        int schemaVersion,
        ManifestFileEntry[] files,
        string sourcePath = @"D:\test.msi",
        string sourceHash = "aaaa")
    {
        return new ManifestDocument
        {
            SchemaVersion = schemaVersion,
            Tool = new ManifestToolInfo { Name = "vtracker", Version = "1.0.0" },
            Source = new ManifestSourceInfo { MsiPath = sourcePath, MsiSha256 = sourceHash },
            Patches = [],
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

    private static ManifestFileEntry CreateFile(string path, string sha256, long size = 100, string? fileVersion = null, string? category = null)
    {
        return new ManifestFileEntry
        {
            Path = path,
            LastWriteTimeUtc = DateTime.SpecifyKind(new DateTime(2026, 4, 17, 0, 0, 0), DateTimeKind.Utc),
            Size = size,
            Sha256 = sha256,
            FileVersion = fileVersion,
            ProductVersion = fileVersion,
            Category = category,
        };
    }

    // ---------------------------------------------------------------
    // 1. ManifestBuilder with catalog produces categories and SchemaVersion 2
    // ---------------------------------------------------------------
    [Fact]
    public async Task BuildAsync_WithCatalog_ProducesCategoriesAndSchemaVersion2()
    {
        using var tempDir = new TempDirectory();
        Directory.CreateDirectory(tempDir.GetPath("bin"));
        File.WriteAllText(tempDir.GetPath(@"bin\core.dll"), "core-content");
        File.WriteAllText(tempDir.GetPath(@"bin\app.exe"), "app-content");
        File.WriteAllText(tempDir.GetPath("readme.txt"), "readme-content");

        var catalog = BuildCatalog(
            new CatalogRow(CatalogRowType.Glob, "**/*.dll", "Libs"),
            new CatalogRow(CatalogRowType.Glob, "**/*.exe", "Apps"));

        var builder = new ManifestBuilder(
            new PathNormalizer(),
            new PathCollisionValidator(),
            new HashService(),
            new PeVersionService(),
            new CatalogClassifier());

        var manifest = await builder.BuildAsync(
            new ManifestBuildRequest(
                tempDir.RootPath,
                @"D:\releases\setup.msi",
                "0123456789abcdef",
                Array.Empty<ManifestPatchInfo>(),
                WorkDirectoryKept: false,
                MaxParallelism: 1,
                new ToolIdentity("vtracker", "1.0.0"),
                Catalog: catalog),
            CancellationToken.None);

        Assert.Equal(2, manifest.SchemaVersion);
        var dll = manifest.Files.Single(f => f.Path.EndsWith("core.dll"));
        var exe = manifest.Files.Single(f => f.Path.EndsWith("app.exe"));
        var txt = manifest.Files.Single(f => f.Path.EndsWith("readme.txt"));

        Assert.Equal("Libs", dll.Category);
        Assert.Equal("Apps", exe.Category);
        Assert.Equal(CatalogClassifier.UnclassifiedCategory, txt.Category);
    }

    // ---------------------------------------------------------------
    // 2. ManifestBuilder without catalog produces SchemaVersion 1 and null categories
    // ---------------------------------------------------------------
    [Fact]
    public async Task BuildAsync_WithoutCatalog_ProducesSchemaVersion1AndNullCategories()
    {
        using var tempDir = new TempDirectory();
        File.WriteAllText(tempDir.GetPath("file.dll"), "content");

        var builder = new ManifestBuilder(
            new PathNormalizer(),
            new PathCollisionValidator(),
            new HashService(),
            new PeVersionService(),
            new CatalogClassifier());

        var manifest = await builder.BuildAsync(
            new ManifestBuildRequest(
                tempDir.RootPath,
                @"D:\releases\setup.msi",
                "0123456789abcdef",
                Array.Empty<ManifestPatchInfo>(),
                WorkDirectoryKept: false,
                MaxParallelism: 1,
                new ToolIdentity("vtracker", "1.0.0")),
            CancellationToken.None);

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.All(manifest.Files, file => Assert.Null(file.Category));
    }

    // ---------------------------------------------------------------
    // 3. ManifestComparator with catalog produces per-category breakdown
    // ---------------------------------------------------------------
    [Fact]
    public void Compare_WithCatalog_ProducesCategoryBreakdown()
    {
        var comparator = new ManifestComparator(new CatalogClassifier());

        var left = CreateManifest(1, [
            CreateFile("bin/old.dll", "1111"),
            CreateFile("bin/common.dll", "2222"),
        ]);

        var right = CreateManifest(1, [
            CreateFile("bin/common.dll", "9999"),
            CreateFile("bin/new.exe", "3333"),
        ]);

        var catalog = BuildCatalog(
            new CatalogRow(CatalogRowType.Glob, "**/*.dll", "Libs"),
            new CatalogRow(CatalogRowType.Glob, "**/*.exe", "Apps"));

        var result = comparator.Compare(left, right, catalog);

        Assert.NotNull(result.Summary.CategoryBreakdown);
        Assert.True(result.Summary.CategoryBreakdown.Length > 0);

        var apps = result.Summary.CategoryBreakdown.Single(b => b.Category == "Apps");
        Assert.Equal(1, apps.Added);
        Assert.Equal(0, apps.Removed);
        Assert.Equal(0, apps.Updated);

        var libs = result.Summary.CategoryBreakdown.Single(b => b.Category == "Libs");
        Assert.Equal(0, libs.Added);
        Assert.Equal(1, libs.Removed);
        Assert.Equal(1, libs.Updated);
    }

    // ---------------------------------------------------------------
    // 4. ManifestComparator without catalog produces null CategoryBreakdown
    // ---------------------------------------------------------------
    [Fact]
    public void Compare_WithoutCatalog_ProducesNullCategoryBreakdown()
    {
        var comparator = new ManifestComparator(new CatalogClassifier());

        var left = CreateManifest(1, [CreateFile("bin/a.dll", "1111")]);
        var right = CreateManifest(1, [CreateFile("bin/b.dll", "2222")]);

        var result = comparator.Compare(left, right);

        Assert.Null(result.Summary.CategoryBreakdown);
    }

    // ---------------------------------------------------------------
    // 5. Compare with catalog attaches categories to added/removed/updated
    // ---------------------------------------------------------------
    [Fact]
    public void Compare_WithCatalog_AttachesCategoriesToAddedRemovedUpdated()
    {
        var comparator = new ManifestComparator(new CatalogClassifier());

        var left = CreateManifest(1, [
            CreateFile("bin/removed.dll", "1111"),
            CreateFile("bin/updated.exe", "2222"),
            CreateFile("data/unchanged.txt", "4444"),
        ]);

        var right = CreateManifest(1, [
            CreateFile("bin/added.dll", "3333"),
            CreateFile("bin/updated.exe", "9999"),
            CreateFile("data/unchanged.txt", "4444"),
        ]);

        var catalog = BuildCatalog(
            new CatalogRow(CatalogRowType.Glob, "**/*.dll", "Libs"),
            new CatalogRow(CatalogRowType.Glob, "**/*.exe", "Apps"));

        var result = comparator.Compare(left, right, catalog);

        var added = Assert.Single(result.Added);
        Assert.Equal("bin/added.dll", added.Path);
        Assert.Equal("Libs", added.Category);

        var removed = Assert.Single(result.Removed);
        Assert.Equal("bin/removed.dll", removed.Path);
        Assert.Equal("Libs", removed.Category);

        var updated = Assert.Single(result.Updated);
        Assert.Equal("bin/updated.exe", updated.Path);
        Assert.Equal("Apps", updated.Category);
    }

    // ---------------------------------------------------------------
    // 5b. Unclassified files in compare get Unclassified category
    // ---------------------------------------------------------------
    [Fact]
    public void Compare_WithCatalog_UnmatchedFilesGetUnclassifiedCategory()
    {
        var comparator = new ManifestComparator(new CatalogClassifier());

        var left = CreateManifest(1, []);
        var right = CreateManifest(1, [CreateFile("readme.txt", "1111")]);

        var catalog = BuildCatalog(
            new CatalogRow(CatalogRowType.Glob, "**/*.dll", "Libs"));

        var result = comparator.Compare(left, right, catalog);

        var added = Assert.Single(result.Added);
        Assert.Equal(CatalogClassifier.UnclassifiedCategory, added.Category);

        Assert.NotNull(result.Summary.CategoryBreakdown);
        var unclassified = result.Summary.CategoryBreakdown.Single(b => b.Category == CatalogClassifier.UnclassifiedCategory);
        Assert.Equal(1, unclassified.Added);
    }

    // ---------------------------------------------------------------
    // 6. V2 manifest round-trips through JSON serialization
    // ---------------------------------------------------------------
    [Fact]
    public void V2Manifest_RoundTripsThroughJsonSerialization()
    {
        var original = CreateManifest(2, [
            CreateFile("bin/core.dll", "abcd1234", category: "Libs"),
            CreateFile("bin/app.exe", "ef567890", category: "Apps"),
            CreateFile("readme.txt", "11112222", category: CatalogClassifier.UnclassifiedCategory),
        ]);

        var json = JsonSerializer.Serialize(original, VTrackerJsonContext.Default.ManifestDocument);
        var deserialized = JsonSerializer.Deserialize(json, VTrackerJsonContext.Default.ManifestDocument);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.SchemaVersion);
        Assert.Equal(original.Files.Length, deserialized.Files.Length);

        for (var i = 0; i < original.Files.Length; i++)
        {
            Assert.Equal(original.Files[i].Path, deserialized.Files[i].Path);
            Assert.Equal(original.Files[i].Sha256, deserialized.Files[i].Sha256);
            Assert.Equal(original.Files[i].Category, deserialized.Files[i].Category);
        }
    }

    // ---------------------------------------------------------------
    // 7. V1 manifest loads with null categories
    // ---------------------------------------------------------------
    [Fact]
    public void V1ManifestJson_DeserializesWithNullCategories()
    {
        // V1 JSON has no "category" field at all
        var v1Json = """
        {
          "schemaVersion": 1,
          "tool": { "name": "vtracker", "version": "1.0.0" },
          "source": { "msiPath": "D:\\test.msi", "msiSha256": "aaaa" },
          "patches": [],
          "extraction": { "mode": "administrative-image", "workDirKept": false, "compression": "Optimal" },
          "files": [
            { "path": "bin/core.dll", "size": 100, "sha256": "abcd1234" },
            { "path": "bin/app.exe", "size": 200, "sha256": "ef567890" }
          ]
        }
        """;

        var manifest = JsonSerializer.Deserialize(v1Json, VTrackerJsonContext.Default.ManifestDocument);

        Assert.NotNull(manifest);
        Assert.Equal(1, manifest.SchemaVersion);
        Assert.All(manifest.Files, file => Assert.Null(file.Category));
    }

    // ---------------------------------------------------------------
    // 8. Schema version range: 0 rejected, 1 accepted, 2 accepted, 3 rejected
    // ---------------------------------------------------------------
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public async Task SchemaVersion_RangeCheck(int version, bool shouldSucceed)
    {
        using var tempDir = new TempDirectory();

        var json = $$"""
        {
          "schemaVersion": {{version}},
          "tool": { "name": "vtracker", "version": "1.0.0" },
          "source": { "msiPath": "D:\\test.msi", "msiSha256": "aaaa" },
          "patches": [],
          "extraction": { "mode": "administrative-image", "workDirKept": false, "compression": "Optimal" },
          "files": []
        }
        """;

        var jsonPath = tempDir.GetPath("manifest.json");
        await File.WriteAllTextAsync(jsonPath, json);

        var repository = new ManifestRepository(new PathNormalizer(), new PathCollisionValidator());

        if (shouldSucceed)
        {
            var manifest = await repository.LoadFromPathAsync(jsonPath, CancellationToken.None);
            Assert.Equal(version, manifest.SchemaVersion);
        }
        else
        {
            var ex = await Assert.ThrowsAsync<ManifestValidationException>(
                () => repository.LoadFromPathAsync(jsonPath, CancellationToken.None));
            Assert.Contains("unsupported schema version", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
