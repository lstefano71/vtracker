namespace VTracker.Core;

public class VTrackerException(string message)
    : Exception(message)
{
}

public sealed class ProcessFailureException(string stepName, int exitCode, string logPath)
    : VTrackerException($"{stepName} failed with exit code {exitCode}. Inspect '{logPath}' for details.")
{
    public string StepName { get; } = stepName;

    public int ExitCode { get; } = exitCode;

    public string LogPath { get; } = logPath;
}

public sealed class NormalizedPathCollisionException(string normalizedPath, string firstPath, string secondPath)
    : VTrackerException($"Normalized path collision for '{normalizedPath}' between '{firstPath}' and '{secondPath}'.")
{
    public string NormalizedPath { get; } = normalizedPath;

    public string FirstPath { get; } = firstPath;

    public string SecondPath { get; } = secondPath;
}

public sealed class ManifestValidationException(string message)
    : VTrackerException(message)
{
}

public sealed class InstallerImageDiscoveryException(string message)
    : VTrackerException(message)
{
}
