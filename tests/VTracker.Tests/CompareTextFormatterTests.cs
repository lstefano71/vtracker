using VTracker.Core;

namespace VTracker.Tests;

public sealed class CompareTextFormatterTests
{
    [Fact]
    public void Format_NoHiddenRows_DoesNotAppendFilterNote()
    {
        var result = MakeResult(added: 1, removed: 0, updated: 0);
        var output = CompareTextFormatter.Format(result, hiddenCount: 0);
        Assert.DoesNotContain("hidden", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_SingleHiddenRow_AppendsNoteWithSingular()
    {
        var result = MakeResult(added: 2, removed: 0, updated: 0);
        var output = CompareTextFormatter.Format(result, hiddenCount: 1);
        Assert.Contains("1 file row hidden by --include filter", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_MultipleHiddenRows_AppendsNoteWithPlural()
    {
        var result = MakeResult(added: 5, removed: 0, updated: 0);
        var output = CompareTextFormatter.Format(result, hiddenCount: 3);
        Assert.Contains("3 file rows hidden by --include filter", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_SummaryAlwaysShowsFullCounts()
    {
        // Summary counts must equal the full result even when detail rows are filtered
        var result = MakeResult(added: 10, removed: 5, updated: 2);
        var output = CompareTextFormatter.Format(result, hiddenCount: 7);
        Assert.Contains("Added: 10", output);
        Assert.Contains("Removed: 5", output);
        Assert.Contains("Updated: 2", output);
    }

    [Fact]
    public void Format_WithDetailRows_RendersCorrectPrefixes()
    {
        var result = new CompareResult
        {
            Summary = new CompareSummary { Added = 1, Removed = 1, Updated = 1, ProvenanceDifferences = 1 },
            Added = [new CompareAddedFile { Path = "bin/new.dll" }],
            Removed = [new CompareRemovedFile { Path = "bin/old.dll" }],
            Updated =
            [
                new CompareUpdatedFile
                {
                    Path = "bin/changed.dll",
                    Left = new CompareFileSnapshot { Sha256 = "aaaa", Size = 10 },
                    Right = new CompareFileSnapshot { Sha256 = "bbbb", Size = 20 },
                },
            ],
            ProvenanceDifferences = ["Source MSI hash differs."],
        };

        var output = CompareTextFormatter.Format(result);
        Assert.Contains("+ bin/new.dll", output);
        Assert.Contains("- bin/old.dll", output);
        Assert.Contains("~ bin/changed.dll", output);
        Assert.Contains("! Source MSI hash differs.", output);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static CompareResult MakeResult(int added, int removed, int updated) =>
        new()
        {
            Summary = new CompareSummary
            {
                Added = added,
                Removed = removed,
                Updated = updated,
                ProvenanceDifferences = 0,
            },
            Added = Enumerable.Range(0, added).Select(i => new CompareAddedFile { Path = $"bin/added{i}.dll" }).ToArray(),
            Removed = Enumerable.Range(0, removed).Select(i => new CompareRemovedFile { Path = $"bin/removed{i}.dll" }).ToArray(),
            Updated = [],
            ProvenanceDifferences = [],
        };
}
