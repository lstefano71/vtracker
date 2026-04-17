using System.Collections.ObjectModel;
using System.Diagnostics;

namespace VTracker.Core;

public sealed class MsiexecRunner
{
    public Task CreateAdministrativeImageAsync(
        string sourceMsiPath,
        string targetDirectory,
        string logPath,
        CancellationToken cancellationToken)
    {
        return RunAsync(
            stepName: "Administrative image creation",
            configureArguments: arguments =>
            {
                arguments.Add("/a");
                arguments.Add(sourceMsiPath);
                arguments.Add("/qn");
                arguments.Add($"TARGETDIR={targetDirectory}");
                arguments.Add("/l*vx");
                arguments.Add(logPath);
            },
            cancellationToken);
    }

    public Task ApplyPatchAsync(
        string patchPath,
        string targetMsiPath,
        string logPath,
        CancellationToken cancellationToken)
    {
        return RunAsync(
            stepName: $"Patch application for '{patchPath}'",
            configureArguments: arguments =>
            {
                arguments.Add("/p");
                arguments.Add(patchPath);
                arguments.Add("/a");
                arguments.Add(targetMsiPath);
                arguments.Add("/qn");
                arguments.Add("/l*vx");
                arguments.Add(logPath);
            },
            cancellationToken);
    }

    private static async Task RunAsync(
        string stepName,
        Action<Collection<string>> configureArguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "msiexec.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        configureArguments(startInfo.ArgumentList);

        using var process = new Process
        {
            StartInfo = startInfo,
        };

        if (!process.Start())
        {
            throw new VTrackerException($"{stepName} could not start msiexec.exe.");
        }

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var logPath = startInfo.ArgumentList[^1];
            throw new ProcessFailureException(stepName, process.ExitCode, logPath);
        }
    }
}
