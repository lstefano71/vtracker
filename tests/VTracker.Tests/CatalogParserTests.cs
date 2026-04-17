using VTracker.Core;

namespace VTracker.Tests;

public sealed class CatalogParserTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"vtracker-catalog-test-{Guid.NewGuid():N}");

    public CatalogParserTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string WriteCatalog(string content)
    {
        var path = Path.Combine(_tempDir, $"catalog-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Parse_ValidMixedGlobAndRegex_ProducesCorrectEntries()
    {
        var csv = """
            type,pattern,category
            G,FontsFolder/**,Fonts
            R,dyalog\d+_64.*unicode\.dll,Core
            """;
        var parser = new CatalogParser();
        var catalog = parser.Parse(WriteCatalog(csv));

        Assert.Equal(2, catalog.Entries.Count);
        Assert.Equal(CatalogRowType.Glob, catalog.Entries[0].Type);
        Assert.Equal("FontsFolder/**", catalog.Entries[0].Pattern);
        Assert.Equal("Fonts", catalog.Entries[0].Category);
        Assert.Equal(CatalogRowType.Regex, catalog.Entries[1].Type);
        Assert.Equal("Core", catalog.Entries[1].Category);
    }

    [Fact]
    public void Parse_RegexWithCommaInQuotedField_ParsesCorrectly()
    {
        // RFC 4180: field with comma must be quoted
        var csv = "type,pattern,category\r\nR,\"^foo{1,3}\\.dll$\",Core\r\n";
        var parser = new CatalogParser();
        var catalog = parser.Parse(WriteCatalog(csv));

        Assert.Single(catalog.Entries);
        Assert.Equal("^foo{1,3}\\.dll$", catalog.Entries[0].Pattern);
        Assert.Equal("Core", catalog.Entries[0].Category);
    }

    [Fact]
    public void Parse_InvalidTypeValue_Throws()
    {
        var csv = "type,pattern,category\r\nX,**/*.dll,Stuff\r\n";
        var parser = new CatalogParser();

        var ex = Assert.Throws<VTrackerException>(() => parser.Parse(WriteCatalog(csv)));
        Assert.Contains("type 'X' is not valid", ex.Message);
    }

    [Fact]
    public void Parse_MissingHeaderColumns_Throws()
    {
        var csv = "type,pattern\r\nG,**/*.dll\r\n";
        var parser = new CatalogParser();

        var ex = Assert.Throws<VTrackerException>(() => parser.Parse(WriteCatalog(csv)));
        Assert.Contains("header row", ex.Message);
    }

    [Fact]
    public void Parse_HeaderOnly_ProducesZeroEntries()
    {
        var csv = "type,pattern,category\r\n";
        var parser = new CatalogParser();
        var catalog = parser.Parse(WriteCatalog(csv));

        Assert.Empty(catalog.Entries);
    }

    [Fact]
    public void Parse_BlankRowsSkipped()
    {
        var csv = "type,pattern,category\r\nG,**/*.dll,Libs\r\n,,\r\nG,**/*.exe,Apps\r\n";
        var parser = new CatalogParser();
        var catalog = parser.Parse(WriteCatalog(csv));

        Assert.Equal(2, catalog.Entries.Count);
    }

    [Fact]
    public void Parse_EmptyPattern_Throws()
    {
        var csv = "type,pattern,category\r\nG,,Stuff\r\n";
        var parser = new CatalogParser();

        var ex = Assert.Throws<VTrackerException>(() => parser.Parse(WriteCatalog(csv)));
        Assert.Contains("pattern must not be empty", ex.Message);
    }

    [Fact]
    public void Parse_EmptyCategory_Throws()
    {
        var csv = "type,pattern,category\r\nG,**/*.dll,\r\n";
        var parser = new CatalogParser();

        var ex = Assert.Throws<VTrackerException>(() => parser.Parse(WriteCatalog(csv)));
        Assert.Contains("category must not be empty", ex.Message);
    }

    [Fact]
    public void Parse_AdditionalColumnsIgnored()
    {
        var csv = "type,pattern,category,notes\r\nG,**/*.dll,Libs,some note\r\n";
        var parser = new CatalogParser();
        var catalog = parser.Parse(WriteCatalog(csv));

        Assert.Single(catalog.Entries);
        Assert.Equal("Libs", catalog.Entries[0].Category);
    }

    [Fact]
    public void Parse_NonexistentFile_Throws()
    {
        var parser = new CatalogParser();
        Assert.Throws<VTrackerException>(() => parser.Parse(Path.Combine(_tempDir, "missing.csv")));
    }

    [Fact]
    public void ParseRows_ReturnsRawRows()
    {
        var csv = "type,pattern,category\r\nG,**/*.dll,Libs\r\nR,^core\\.dll$,Core\r\n";
        var parser = new CatalogParser();
        var rows = parser.ParseRows(WriteCatalog(csv));

        Assert.Equal(2, rows.Count);
        Assert.Equal(CatalogRowType.Glob, rows[0].Type);
        Assert.Equal(CatalogRowType.Regex, rows[1].Type);
    }
}
