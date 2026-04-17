namespace VTracker.Core;

/// <summary>
/// No-op progress reporter — runs each step silently. Used as the default
/// when no reporter is injected (e.g. in tests or server-side scenarios).
/// </summary>
public sealed class NullExtractProgressReporter : IExtractProgressReporter
{
    public static readonly NullExtractProgressReporter Instance = new();

    public Task RunWithLogTailAsync(
        string description,
        string logPath,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
        => action(cancellationToken);

    public Task RunAsync(
        string description,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
        => action(cancellationToken);
}
