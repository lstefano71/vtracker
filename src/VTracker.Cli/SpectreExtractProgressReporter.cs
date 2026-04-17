using Spectre.Console;
using Spectre.Tui;
using VTracker.Core;

namespace VTracker.Cli;

/// <summary>
/// Spectre progress reporter for interactive terminals.
/// Uses Spectre.Console Status widget for simple spinner steps and
/// Spectre.TUI InlineMode for multi-line log tail rendering.
/// Falls back to plain console writes if rendering setup fails.
/// </summary>
public sealed class SpectreExtractProgressReporter : IExtractProgressReporter
{
    private const int TailLineCount = 10;

    private static readonly string[] SpinnerFrames =
        ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

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
        // Try to create a TUI terminal for proper multi-line rendering.
        // If creation fails (non-interactive, unsupported terminal), fall back
        // to plain output. The action has not started yet, so this is safe.
        ITerminal? terminal = null;
        try
        {
            terminal = Terminal.Create(new InlineMode(TailLineCount + 1));
        }
        catch
        {
            // TUI not available
        }

        if (terminal is null)
        {
            await PlainRunAsync(description, action, cancellationToken);
            return;
        }

        // Once we enter the TUI path, never fall back to PlainRunAsync —
        // that would re-invoke the action. Instead, let exceptions propagate.
        try
        {
            await RunWithTuiTailAsync(terminal, description, logPath, action, cancellationToken);
        }
        finally
        {
            terminal.Dispose();
            // Reset SGR attributes that may bleed from TUI rendering,
            // then move cursor up past the now-blank tail line rows.
            // InlineMode.OnDetach places the cursor at savedPos + height + 1;
            // we want savedPos + 1 (just below the ✓ line), so go up by height.
            Console.Write("\x1b[0m");
            Console.Write($"\x1b[{TailLineCount + 1}A");
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

            _console.MarkupLine($"[green]✓ {Markup.Escape(description)}[/]");
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

            _console.MarkupLine($"[green]✓ {Markup.Escape(description)}[/]");
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
    /// Renders a spinner + description on row 0 and the last
    /// <see cref="TailLineCount"/> log lines below using Spectre.TUI's
    /// inline mode with double-buffered diff rendering. Only changed cells
    /// are flushed each frame, eliminating the cursor-up/overwrite
    /// corruption that affects Spectre.Console's Live display.
    /// </summary>
    private static async Task RunWithTuiTailAsync(
        ITerminal terminal,
        string description,
        string logPath,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        var rawTailLines = new List<string>(TailLineCount);
        var tailLock = new object();
        var spinnerIndex = 0;
        var renderer = new Renderer(terminal);
        var succeeded = false;

        using var tailCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var tailTask = TailLogAsync(
            logPath,
            line =>
            {
                lock (tailLock)
                {
                    rawTailLines.Add(line);
                    if (rawTailLines.Count > TailLineCount)
                        rawTailLines.RemoveAt(0);
                }
            },
            tailCts.Token);

        var actionTask = action(cancellationToken);

        try
        {
            while (!actionTask.IsCompleted)
            {
                await Task.WhenAny(actionTask, Task.Delay(150, CancellationToken.None));

                List<string> snapshot;
                lock (tailLock) { snapshot = [.. rawTailLines]; }

                var frame = SpinnerFrames[spinnerIndex++ % SpinnerFrames.Length];
                var w = Math.Max(20, terminal.GetSize().Width);

                try
                {
                    renderer.Draw((ctx, info) =>
                    {
                        ctx.SetString(0, 0, TruncateLine($"{frame} {description}", w),
                            new Style(Color.Blue), w);
                        for (var i = 0; i < TailLineCount; i++)
                        {
                            if (i < snapshot.Count)
                                ctx.SetString(0, i + 1, TruncateLine($"  {snapshot[i]}", w),
                                    new Style(Color.Grey), w);
                            else
                                ctx.SetString(0, i + 1, " ", new Style(), w);
                        }
                    });
                }
                catch
                {
                    // Render failure — stop rendering but keep waiting for the action
                    break;
                }
            }

            await actionTask;
            succeeded = true;
        }
        finally
        {
            await tailCts.CancelAsync();
            try { await tailTask; } catch { }

            // Final frame: show result icon, clear tail lines
            try
            {
                var w = Math.Max(20, terminal.GetSize().Width);
                var (icon, color) = succeeded
                    ? ("✓", Color.Green)
                    : ("✗", Color.Red);

                renderer.Draw((ctx, info) =>
                {
                    ctx.SetString(0, 0, TruncateLine($"{icon} {description}", w),
                        new Style(color), w);
                    for (var i = 1; i <= TailLineCount; i++)
                        ctx.SetString(0, i, new string(' ', w), new Style());
                });
            }
            catch { /* best effort */ }
        }
    }

    private static async Task TailLogAsync(
        string logPath,
        Action<string> onLine,
        CancellationToken cancellationToken)
    {
        try
        {
            var deadline = Environment.TickCount64 + 5_000;
            while (!File.Exists(logPath) && Environment.TickCount64 < deadline)
            {
                await Task.Delay(200, cancellationToken);
            }

            if (!File.Exists(logPath))
                return;

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
                    if (!string.IsNullOrWhiteSpace(line) &&
                        !line.StartsWith("===", StringComparison.Ordinal))
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
        catch (OperationCanceledException) { }
        catch { }
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
        maxLength <= 0 ? string.Empty :
        line.Length <= maxLength ? line :
        maxLength == 1 ? "…" :
        string.Concat(line.AsSpan(0, maxLength - 1), "…");
}
