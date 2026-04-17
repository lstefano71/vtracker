using Spectre.Console;
using Spectre.Console.Rendering;
using VTracker.Core;

namespace VTracker.Cli;

/// <summary>
/// Spectre.Console progress reporter for interactive terminals.
/// Shows a spinner per step and optionally tails the installer log file
/// so the user can see live msiexec output.
/// Falls back to plain console writes if Spectre rendering fails.
/// </summary>
public sealed class SpectreExtractProgressReporter : IExtractProgressReporter
{
    private const int TailLineCount = 5;
    private readonly IAnsiConsole _console;

    public SpectreExtractProgressReporter(IAnsiConsole console)
    {
        _console = console;
    }

    public async Task RunWithLogTailAsync(
        string description,
        string logPath,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await RunWithLiveTailAsync(description, logPath, action, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (VTrackerException)
        {
            throw;
        }
        catch
        {
            // Spectre rendering failure — degrade to plain output
            await PlainRunAsync(description, action, cancellationToken);
        }
    }

    public async Task RunAsync(
        string description,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await _console.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(
                    $"[blue]{Markup.Escape(description)}[/]",
                    async ctx => await action(cancellationToken));

            _console.MarkupLine($"[green]✓[/] {Markup.Escape(description)}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (VTrackerException)
        {
            throw;
        }
        catch
        {
            await PlainRunAsync(description, action, cancellationToken);
        }
    }

    public async Task RunWithStatusAsync(
        string description,
        Func<Action<string>, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await _console.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(
                    $"[blue]{Markup.Escape(description)}[/]",
                    async ctx =>
                    {
                        var escapedDesc = Markup.Escape(description);
                        await action(
                            status => ctx.Status = $"[blue]{escapedDesc}[/] [grey]— {Markup.Escape(TruncateLine(status, 60))}[/]",
                            cancellationToken);
                    });

            _console.MarkupLine($"[green]✓[/] {Markup.Escape(description)}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (VTrackerException)
        {
            throw;
        }
        catch
        {
            await PlainRunAsync(description, ct => action(_ => { }, ct), cancellationToken);
        }
    }

    /// <summary>
    /// Uses Spectre Live display with a fixed-height renderable and a
    /// dedicated render loop. The tail callback only mutates shared state
    /// under a lock; all <c>ctx.UpdateTarget</c> calls happen from the
    /// render loop on the Live callback's own async context.
    /// </summary>
    private async Task RunWithLiveTailAsync(
        string description,
        string logPath,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        // Leave margin to prevent any line wrapping that breaks Live redraw.
        var maxLineWidth = Math.Max(20, _console.Profile.Width - 6);
        var tailLines = new List<string>(TailLineCount);
        var tailLock = new object();

        await _console.Live(BuildTailRenderable(description, [], maxLineWidth))
            .AutoClear(true)
            .Overflow(VerticalOverflow.Crop)
            .StartAsync(async ctx =>
            {
                using var tailCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                // The tail callback only buffers lines under a lock — no Spectre calls.
                var tailTask = TailLogAsync(
                    logPath,
                    line =>
                    {
                        var truncated = TruncateLine(line, maxLineWidth - 2);
                        lock (tailLock)
                        {
                            tailLines.Add(truncated);
                            if (tailLines.Count > TailLineCount)
                                tailLines.RemoveAt(0);
                        }
                    },
                    tailCts.Token);

                var actionTask = action(cancellationToken);

                // Render loop: poll every 150 ms, refresh from this context only.
                while (!actionTask.IsCompleted)
                {
                    await Task.WhenAny(actionTask, Task.Delay(150, CancellationToken.None));
                    List<string> snapshot;
                    lock (tailLock) { snapshot = [.. tailLines]; }
                    ctx.UpdateTarget(BuildTailRenderable(description, snapshot, maxLineWidth));
                }

                await actionTask; // observe / propagate exceptions

                await tailCts.CancelAsync();
                try { await tailTask; } catch { /* swallow tail failure — never fails extract */ }

                // Final flush so the last few lines are visible briefly.
                List<string> finalSnapshot;
                lock (tailLock) { finalSnapshot = [.. tailLines]; }
                ctx.UpdateTarget(BuildTailRenderable(description, finalSnapshot, maxLineWidth));
            });

        _console.MarkupLine($"[green]✓[/] {Markup.Escape(description)}");
    }

    /// <summary>
    /// Builds a fixed-height renderable: one description row plus
    /// <see cref="TailLineCount"/> tail rows (padded with spaces when empty).
    /// Uses <see cref="Text"/> instead of <see cref="Markup"/> so that raw
    /// log content never needs escaping and cannot cause width surprises.
    /// </summary>
    private static IRenderable BuildTailRenderable(
        string description,
        IReadOnlyList<string> tailLines,
        int maxLineWidth)
    {
        var parts = new IRenderable[TailLineCount + 1];
        parts[0] = new Text(TruncateLine($"◆ {description}", maxLineWidth), new Style(Color.Blue));

        for (var i = 0; i < TailLineCount; i++)
        {
            parts[i + 1] = i < tailLines.Count
                ? new Text($"  {tailLines[i]}", new Style(Color.Grey))
                : new Text(" "); // non-empty to guarantee stable row height
        }

        return new Rows(parts);
    }

    private static async Task TailLogAsync(
        string logPath,
        Action<string> onLine,
        CancellationToken cancellationToken)
    {
        try
        {
            // Wait briefly for the installer to create the file
            var deadline = Environment.TickCount64 + 5_000;
            while (!File.Exists(logPath) && Environment.TickCount64 < deadline)
            {
                await Task.Delay(200, cancellationToken);
            }

            if (!File.Exists(logPath))
            {
                return;
            }

            using var stream = new FileStream(
                logPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is not null)
                {
                    // Skip blank and separator lines that add no value
                    if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("===", StringComparison.Ordinal))
                    {
                        onLine(line);
                    }
                }
                else
                {
                    await Task.Delay(150, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected cancellation when the step completes
        }
        catch
        {
            // File-sharing or access failures are silently swallowed per the spec
        }
    }

    private static async Task PlainRunAsync(
        string description,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        Console.Out.WriteLine($"{description}...");
        await action(cancellationToken);
    }

    private static string TruncateLine(string line, int maxLength) =>
        line.Length <= maxLength ? line : line[..maxLength] + "…";
}
