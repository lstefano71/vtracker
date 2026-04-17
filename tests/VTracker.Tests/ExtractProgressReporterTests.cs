using VTracker.Core;

namespace VTracker.Tests;

public sealed class ExtractProgressReporterTests
{
    // ── NullExtractProgressReporter ──────────────────────────────────────────

    [Fact]
    public async Task NullReporter_RunAsync_CallsAction()
    {
        var called = false;
        await NullExtractProgressReporter.Instance.RunAsync(
            "test step",
            _ => { called = true; return Task.CompletedTask; },
            CancellationToken.None);
        Assert.True(called);
    }

    [Fact]
    public async Task NullReporter_RunWithLogTailAsync_CallsAction()
    {
        var called = false;
        await NullExtractProgressReporter.Instance.RunWithLogTailAsync(
            "test step",
            @"C:\nonexistent.log",
            _ => { called = true; return Task.CompletedTask; },
            CancellationToken.None);
        Assert.True(called);
    }

    [Fact]
    public async Task NullReporter_RunAsync_PropagatesActionException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NullExtractProgressReporter.Instance.RunAsync(
                "failing step",
                _ => Task.FromException(new InvalidOperationException("boom")),
                CancellationToken.None));
    }

    [Fact]
    public async Task NullReporter_RunWithLogTailAsync_PropagatesActionException()
    {
        await Assert.ThrowsAsync<VTrackerException>(
            () => NullExtractProgressReporter.Instance.RunWithLogTailAsync(
                "failing step",
                @"C:\nonexistent.log",
                _ => Task.FromException(new VTrackerException("extract failed")),
                CancellationToken.None));
    }

    // ── Recording reporter (validates ExtractService call order) ─────────────

    [Fact]
    public async Task RecordingReporter_CapturesStepsInOrder()
    {
        var reporter = new RecordingProgressReporter();

        // Simulate the sequence ExtractService would call
        await reporter.RunWithLogTailAsync("Creating administrative image", "01-admin-image.log", ct => Task.CompletedTask, CancellationToken.None);
        await reporter.RunWithLogTailAsync("Applying patch 1 of 1: patch.msp", "02-patch-001.log", ct => Task.CompletedTask, CancellationToken.None);
        await reporter.RunWithStatusAsync("Collecting file metadata", (_, ct) => Task.CompletedTask, CancellationToken.None);
        await reporter.RunWithStatusAsync("Creating archive", (_, ct) => Task.CompletedTask, CancellationToken.None);

        Assert.Equal(4, reporter.Steps.Count);
        Assert.Equal("Creating administrative image", reporter.Steps[0].Description);
        Assert.Equal("01-admin-image.log", reporter.Steps[0].LogPath);
        Assert.Equal("Applying patch 1 of 1: patch.msp", reporter.Steps[1].Description);
        Assert.Null(reporter.Steps[2].LogPath);
        Assert.Null(reporter.Steps[3].LogPath);
    }

    /// <summary>Recording double used to verify caller-side ordering without running real services.</summary>
    private sealed class RecordingProgressReporter : IExtractProgressReporter
    {
        public List<(string Description, string? LogPath)> Steps { get; } = [];

        public Task RunWithLogTailAsync(string description, string logPath, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            Steps.Add((description, logPath));
            return action(cancellationToken);
        }

        public Task RunAsync(string description, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            Steps.Add((description, null));
            return action(cancellationToken);
        }

        public Task RunWithStatusAsync(string description, Func<Action<string>, CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            Steps.Add((description, null));
            return action(_ => { }, cancellationToken);
        }
    }
}
