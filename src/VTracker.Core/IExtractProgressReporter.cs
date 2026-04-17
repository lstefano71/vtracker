namespace VTracker.Core;

/// <summary>
/// Allows the CLI layer to render per-step progress during extraction without
/// coupling ExtractService to a specific UI framework.
/// </summary>
public interface IExtractProgressReporter
{
    /// <summary>
    /// Wraps an extraction step that writes a log file.
    /// Implementations may tail the log file for live preview.
    /// A failure inside <paramref name="action"/> must not be swallowed.
    /// A failure in log-tail must be swallowed and must not fail extraction.
    /// </summary>
    Task RunWithLogTailAsync(
        string description,
        string logPath,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken);

    /// <summary>
    /// Wraps an extraction step with no associated log file (e.g. hashing, archive creation).
    /// </summary>
    Task RunAsync(
        string description,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken);

    /// <summary>
    /// Wraps an extraction step that provides incremental status updates.
    /// The action receives a callback it can invoke to update the displayed status text.
    /// Implementations that do not support live status can ignore the updates.
    /// </summary>
    Task RunWithStatusAsync(
        string description,
        Func<Action<string>, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        return RunAsync(description, ct => action(_ => { }, ct), cancellationToken);
    }
}
