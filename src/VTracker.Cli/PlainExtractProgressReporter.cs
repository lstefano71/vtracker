using VTracker.Core;

namespace VTracker.Cli;

/// <summary>
/// Plain-text progress reporter for non-interactive or redirected terminals.
/// Each step is announced on a single line before it runs; no spinner is shown.
/// </summary>
public sealed class PlainExtractProgressReporter : IExtractProgressReporter
{
    public async Task RunWithLogTailAsync(
        string description,
        string logPath,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        Console.Out.WriteLine($"{description}...");
        await action(cancellationToken);
    }

    public async Task RunAsync(
        string description,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        Console.Out.WriteLine($"{description}...");
        await action(cancellationToken);
    }
}
