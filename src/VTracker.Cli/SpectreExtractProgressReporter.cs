using Spectre.Console;
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
            await RunWithSpinnerAsync(description, logPath, action, cancellationToken);
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
            await RunWithSpinnerAsync(description, logPath: null, action, cancellationToken);
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

    private async Task RunWithSpinnerAsync(
        string description,
        string? logPath,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        if (logPath is not null)
        {
            // Use Live display for multi-line log tail — Status is single-line only.
            var maxLineWidth = Math.Max(20, _console.Profile.Width - 4);
            var tailLines = new List<string>();

            await _console.Live(BuildTailRenderable(description, tailLines))
                .AutoClear(true)
                .StartAsync(async ctx =>
                {
                    using var tailCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    var tailTask = TailLogAsync(
                        logPath,
                        line =>
                        {
                            tailLines.Add(TruncateLine(line, maxLineWidth));
                            if (tailLines.Count > TailLineCount)
                                tailLines.RemoveAt(0);
                            ctx.UpdateTarget(BuildTailRenderable(description, tailLines));
                        },
                        tailCts.Token);

                    try
                    {
                        await action(cancellationToken);
                    }
                    finally
                    {
                        await tailCts.CancelAsync();
                        try { await tailTask; } catch { /* swallow tail failure — never fails extract */ }
                    }
                });
        }
        else
        {
            await _console.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(
                    $"[blue]{Markup.Escape(description)}[/]",
                    async ctx => await action(cancellationToken));
        }

        _console.MarkupLine($"[green]✓[/] {Markup.Escape(description)}");
    }

    private static Rows BuildTailRenderable(string description, List<string> tailLines)
    {
        var parts = new Markup[TailLineCount + 1];
        parts[0] = new Markup($"[blue]◆ {Markup.Escape(description)}[/]");

        for (var i = 0; i < TailLineCount; i++)
        {
            parts[i + 1] = i < tailLines.Count
                ? new Markup($"  [grey]{Markup.Escape(tailLines[i])}[/]")
                : new Markup("");
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
