using Spectre.Console.Testing;
using VTracker.Cli;
using VTracker.Core;

namespace VTracker.Tests;

public sealed class ComparePrettyFormatterTests
{
    private static TestConsole CreateConsole() => new();

    // ── summary table ────────────────────────────────────────────────────────

    [Fact]
    public void Write_SummaryCountsAlwaysPresent()
    {
        var console = CreateConsole();
        var result = MakeResult(added: 3, removed: 2, updated: 1, provenance: 0);

        ComparePrettyFormatter.Write(console, result);

        var output = console.Output;
        Assert.Contains("3", output);
        Assert.Contains("2", output);
        Assert.Contains("1", output);
    }

    [Fact]
    public void Write_AddedRemoved_ShowsPlusMinus()
    {
        var console = CreateConsole();
        var result = new CompareResult
        {
            Summary = new CompareSummary { Added = 1, Removed = 1 },
            Added = ["bin/new.dll"],
            Removed = ["bin/old.dll"],
            Updated = [],
            ProvenanceDifferences = [],
        };

        ComparePrettyFormatter.Write(console, result);

        var output = console.Output;
        Assert.Contains("+ bin/new.dll", output);
        Assert.Contains("- bin/old.dll", output);
    }

    [Fact]
    public void Write_UpdatedFiles_ShowsSizeChange()
    {
        var console = CreateConsole();
        var result = new CompareResult
        {
            Summary = new CompareSummary { Updated = 1 },
            Added = [],
            Removed = [],
            Updated =
            [
                new CompareUpdatedFile
                {
                    Path = "bin/changed.dll",
                    Left = new CompareFileSnapshot { Sha256 = "aaaa", Size = 1024 },
                    Right = new CompareFileSnapshot { Sha256 = "bbbb", Size = 2048 },
                },
            ],
            ProvenanceDifferences = [],
        };

        ComparePrettyFormatter.Write(console, result);

        var output = console.Output;
        Assert.Contains("~ bin/changed.dll", output);
        Assert.Contains("→", output);   // size or version change arrow
    }

    [Fact]
    public void Write_UpdatedFiles_ShowsVersionWhenPresent()
    {
        var console = CreateConsole();
        var result = new CompareResult
        {
            Summary = new CompareSummary { Updated = 1 },
            Added = [],
            Removed = [],
            Updated =
            [
                new CompareUpdatedFile
                {
                    Path = "bin/versioned.dll",
                    Left = new CompareFileSnapshot { Sha256 = "aaaa", Size = 10, FileVersion = "1.0.0.0" },
                    Right = new CompareFileSnapshot { Sha256 = "bbbb", Size = 10, FileVersion = "2.0.0.0" },
                },
            ],
            ProvenanceDifferences = [],
        };

        ComparePrettyFormatter.Write(console, result);

        var output = console.Output;
        Assert.Contains("1.0.0.0", output);
        Assert.Contains("2.0.0.0", output);
    }

    [Fact]
    public void Write_UpdatedFiles_OmitsVersionWhenAbsent()
    {
        var console = CreateConsole();
        var result = new CompareResult
        {
            Summary = new CompareSummary { Updated = 1 },
            Added = [],
            Removed = [],
            Updated =
            [
                new CompareUpdatedFile
                {
                    Path = "bin/no-version.dll",
                    Left = new CompareFileSnapshot { Sha256 = "aaaa", Size = 10 },
                    Right = new CompareFileSnapshot { Sha256 = "bbbb", Size = 10 },
                },
            ],
            ProvenanceDifferences = [],
        };

        ComparePrettyFormatter.Write(console, result);

        // Should NOT show version arrows when both versions are null
        var output = console.Output;
        Assert.DoesNotContain("→", output);
    }

    [Fact]
    public void Write_HiddenCount_ShowsFilterNote()
    {
        var console = CreateConsole();
        var result = MakeResult(added: 5, removed: 0, updated: 0, provenance: 0);

        ComparePrettyFormatter.Write(console, result, hiddenCount: 3);

        Assert.Contains("3 file rows hidden by --include filter", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Write_NoHiddenCount_NoFilterNote()
    {
        var console = CreateConsole();
        var result = MakeResult(added: 2, removed: 0, updated: 0, provenance: 0);

        ComparePrettyFormatter.Write(console, result, hiddenCount: 0);

        Assert.DoesNotContain("hidden", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Write_ProvenanceDifferences_NotFilteredByInclude()
    {
        var console = CreateConsole();
        var result = new CompareResult
        {
            Summary = new CompareSummary { ProvenanceDifferences = 1 },
            Added = [],
            Removed = [],
            Updated = [],
            ProvenanceDifferences = ["Source MSI hash differs."],
        };

        // Even with hiddenCount representing file rows hidden by filter,
        // provenance differences should still appear
        ComparePrettyFormatter.Write(console, result, hiddenCount: 5);

        Assert.Contains("Source MSI hash differs.", console.Output);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static CompareResult MakeResult(int added, int removed, int updated, int provenance) =>
        new()
        {
            Summary = new CompareSummary
            {
                Added = added,
                Removed = removed,
                Updated = updated,
                ProvenanceDifferences = provenance,
            },
            Added = Enumerable.Range(0, added).Select(i => $"bin/added{i}.dll").ToArray(),
            Removed = Enumerable.Range(0, removed).Select(i => $"bin/removed{i}.dll").ToArray(),
            Updated = [],
            ProvenanceDifferences = Enumerable.Range(0, provenance).Select(i => $"Provenance difference {i}.").ToArray(),
        };
}
