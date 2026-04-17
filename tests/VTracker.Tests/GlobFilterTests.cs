using VTracker.Core;

namespace VTracker.Tests;

public sealed class GlobFilterTests
{
    // ── empty-pattern semantics ──────────────────────────────────────────────

    [Fact]
    public void MatchesAny_NullPatterns_ReturnsTrue()
    {
        Assert.True(GlobFilter.MatchesAny("bin/foo.dll", null!));
    }

    [Fact]
    public void MatchesAny_EmptyPatterns_ReturnsTrue()
    {
        Assert.True(GlobFilter.MatchesAny("bin/foo.dll", []));
    }

    [Fact]
    public void FilterAdded_EmptyPatterns_ReturnsAllEntries()
    {
        CompareAddedFile[] entries =
        [
            new() { Path = "bin/a.dll" },
            new() { Path = "bin/b.exe" },
            new() { Path = "config.ini" },
        ];
        var result = GlobFilter.FilterAdded(entries, []);
        Assert.Equal(3, result.Length);
    }

    // ── case-insensitive matching ────────────────────────────────────────────

    [Theory]
    [InlineData("**/*.dll", "bin/aplcore.dll", true)]
    [InlineData("**/*.DLL", "bin/aplcore.dll", true)]   // pattern uppercase, path lower
    [InlineData("**/*.dll", "BIN/APLCORE.DLL", true)]   // path uppercase, pattern lower
    [InlineData("**/*.DLL", "BIN/APLCORE.DLL", true)]   // both uppercase
    public void MatchesAny_CaseInsensitive(string pattern, string path, bool expected)
    {
        Assert.Equal(expected, GlobFilter.MatchesAny(path, [pattern]));
    }

    // ── OR semantics (any pattern matches) ──────────────────────────────────

    [Fact]
    public void MatchesAny_ORSemantics_TrueWhenAnyMatches()
    {
        Assert.True(GlobFilter.MatchesAny("bin/foo.dll", ["**/*.exe", "**/*.dll"]));
    }

    [Fact]
    public void MatchesAny_ORSemantics_FalseWhenNoneMatch()
    {
        Assert.False(GlobFilter.MatchesAny("config/settings.ini", ["**/*.exe", "**/*.dll"]));
    }

    // ── common glob patterns ─────────────────────────────────────────────────

    [Theory]
    [InlineData("**/*.dll",   "bin/sub/deep.dll",  true)]
    [InlineData("**/*.dll",   "bin/sub/deep.exe",  false)]
    [InlineData("bin/*.dll",  "bin/direct.dll",    true)]
    [InlineData("bin/*.dll",  "bin/sub/deep.dll",  false)]   // single-segment wildcard
    [InlineData("bin/**",     "bin/a/b/c.dll",     true)]
    [InlineData("*.dll",      "root.dll",          true)]
    [InlineData("*.dll",      "sub/root.dll",      false)]   // no path separator
    public void MatchesAny_GlobPatterns(string pattern, string path, bool expected)
    {
        Assert.Equal(expected, GlobFilter.MatchesAny(path, [pattern]));
    }

    // ── FilterPaths / FilterUpdated with active patterns ─────────────────────

    [Fact]
    public void FilterAdded_WithPattern_RetainsOnlyMatchingEntries()
    {
        CompareAddedFile[] entries =
        [
            new() { Path = "bin/a.dll" },
            new() { Path = "bin/b.exe" },
            new() { Path = "config/settings.ini" },
        ];
        var result = GlobFilter.FilterAdded(entries, ["**/*.dll"]);
        Assert.Single(result);
        Assert.Equal("bin/a.dll", result[0].Path);
    }

    [Fact]
    public void FilterRemoved_RepeatedPatterns_ORSemantics()
    {
        CompareRemovedFile[] entries =
        [
            new() { Path = "bin/a.dll" },
            new() { Path = "bin/b.exe" },
            new() { Path = "config/settings.ini" },
        ];
        var result = GlobFilter.FilterRemoved(entries, ["**/*.dll", "**/*.exe"]);
        Assert.Equal(2, result.Length);
    }

    [Fact]
    public void FilterUpdated_WithPattern_RetainsOnlyMatchingEntries()
    {
        CompareUpdatedFile[] entries =
        [
            MakeUpdated("bin/a.dll"),
            MakeUpdated("bin/b.exe"),
            MakeUpdated("config/settings.ini"),
        ];

        var result = GlobFilter.FilterUpdated(entries, ["**/*.dll"]);
        Assert.Single(result);
        Assert.Equal("bin/a.dll", result[0].Path);
    }

    [Fact]
    public void FilterUpdated_EmptyPatterns_ReturnsAll()
    {
        CompareUpdatedFile[] entries = [MakeUpdated("bin/a.dll"), MakeUpdated("bin/b.exe")];
        var result = GlobFilter.FilterUpdated(entries, []);
        Assert.Equal(2, result.Length);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static CompareUpdatedFile MakeUpdated(string path) =>
        new()
        {
            Path = path,
            Left = new CompareFileSnapshot { Sha256 = "aaaa", Size = 10 },
            Right = new CompareFileSnapshot { Sha256 = "bbbb", Size = 20 },
        };
}
