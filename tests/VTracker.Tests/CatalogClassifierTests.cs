using VTracker.Core;

namespace VTracker.Tests;

public sealed class CatalogClassifierTests
{
    private static CatalogFile BuildCatalog(params CatalogRow[] rows)
    {
        var entries = rows.Select(CompiledCatalogEntry.Compile).ToArray();
        return new CatalogFile("test.csv", entries);
    }

    [Fact]
    public void Classify_FirstMatchWins_EarlierRowShadowsLater()
    {
        var catalog = BuildCatalog(
            new CatalogRow(CatalogRowType.Glob, "**/*.dll", "EarlyCategory"),
            new CatalogRow(CatalogRowType.Glob, "**/specific.dll", "LateCategory"));

        var classifier = new CatalogClassifier();
        Assert.Equal("EarlyCategory", classifier.Classify(catalog, "bin/specific.dll"));
    }

    [Fact]
    public void Classify_GlobCaseInsensitive()
    {
        var catalog = BuildCatalog(
            new CatalogRow(CatalogRowType.Glob, "**/*.DLL", "Libs"));

        var classifier = new CatalogClassifier();
        Assert.Equal("Libs", classifier.Classify(catalog, "bin/aplcore.dll"));
    }

    [Fact]
    public void Classify_GlobDoubleStarAcrossSegments()
    {
        var catalog = BuildCatalog(
            new CatalogRow(CatalogRowType.Glob, "FontsFolder/**", "Fonts"));

        var classifier = new CatalogClassifier();
        Assert.Equal("Fonts", classifier.Classify(catalog, "FontsFolder/sub/deep/font.ttf"));
    }

    [Fact]
    public void Classify_RegexCaseInsensitive()
    {
        var catalog = BuildCatalog(
            new CatalogRow(CatalogRowType.Regex, @"dyalog\d+_64.*unicode\.dll", "Core"));

        var classifier = new CatalogClassifier();
        Assert.Equal("Core", classifier.Classify(catalog, "ProgramFiles64Folder/Dyalog/bin/DYALOG19_64_UNICODE.DLL"));
    }

    [Fact]
    public void Classify_RegexMatchesFullPath()
    {
        var catalog = BuildCatalog(
            new CatalogRow(CatalogRowType.Regex, @"^bin/core\.dll$", "Core"));

        var classifier = new CatalogClassifier();
        Assert.Equal("Core", classifier.Classify(catalog, "bin/core.dll"));
        Assert.Equal(CatalogClassifier.UnclassifiedCategory, classifier.Classify(catalog, "lib/bin/core.dll"));
    }

    [Fact]
    public void Classify_NoMatch_ReturnsUnclassified()
    {
        var catalog = BuildCatalog(
            new CatalogRow(CatalogRowType.Glob, "**/*.dll", "Libs"));

        var classifier = new CatalogClassifier();
        Assert.Equal(CatalogClassifier.UnclassifiedCategory, classifier.Classify(catalog, "readme.txt"));
    }

    [Fact]
    public void Classify_LiteralGlobExactMatch()
    {
        var catalog = BuildCatalog(
            new CatalogRow(CatalogRowType.Glob, "bin/exact-file.dll", "Exact"));

        var classifier = new CatalogClassifier();
        Assert.Equal("Exact", classifier.Classify(catalog, "bin/exact-file.dll"));
        Assert.Equal(CatalogClassifier.UnclassifiedCategory, classifier.Classify(catalog, "bin/other-file.dll"));
    }

    [Fact]
    public void ClassifyAll_BulkClassification()
    {
        var catalog = BuildCatalog(
            new CatalogRow(CatalogRowType.Glob, "**/*.dll", "Libs"),
            new CatalogRow(CatalogRowType.Glob, "**/*.exe", "Apps"));

        var classifier = new CatalogClassifier();
        var result = classifier.ClassifyAll(catalog, ["bin/a.dll", "bin/b.exe", "readme.txt"]);

        Assert.Equal("Libs", result["bin/a.dll"]);
        Assert.Equal("Apps", result["bin/b.exe"]);
        Assert.Equal(CatalogClassifier.UnclassifiedCategory, result["readme.txt"]);
    }

    [Fact]
    public void Classify_EmptyCatalog_ReturnsUnclassified()
    {
        var catalog = new CatalogFile("test.csv", []);
        var classifier = new CatalogClassifier();

        Assert.Equal(CatalogClassifier.UnclassifiedCategory, classifier.Classify(catalog, "bin/anything.dll"));
    }

    [Fact]
    public void Classify_RegexWithCommaQuantifier()
    {
        var catalog = BuildCatalog(
            new CatalogRow(CatalogRowType.Regex, @"^foo{1,3}\.dll$", "Core"));

        var classifier = new CatalogClassifier();
        Assert.Equal("Core", classifier.Classify(catalog, "foo.dll"));
        Assert.Equal("Core", classifier.Classify(catalog, "foooo.dll")); // fo + ooo → {1,3} matches 3 o's
        Assert.Equal(CatalogClassifier.UnclassifiedCategory, classifier.Classify(catalog, "fooooo.dll")); // fo + oooo → exceeds {1,3}
    }
}
