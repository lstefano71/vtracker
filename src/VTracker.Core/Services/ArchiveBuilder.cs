using System.IO.Compression;

namespace VTracker.Core;

public sealed class ArchiveBuilder(ManifestRepository manifestRepository)
{
    private static readonly DateTimeOffset ZipEpoch = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public async Task CreateAsync(
        string archivePath,
        string imageRootPath,
        string logsDirectory,
        ManifestDocument manifest,
        CancellationToken cancellationToken)
    {
        await using var archiveStream = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: false);

        foreach (var file in manifest.Files)
        {
            var sourcePath = Path.Combine(imageRootPath, file.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(sourcePath))
            {
                throw new VTrackerException($"Archive packaging expected '{sourcePath}' but the file is missing.");
            }

            var entry = archive.CreateEntry(file.Path, CompressionLevel.Optimal);
            entry.LastWriteTime = ToZipTimestamp(file.LastWriteTimeUtc ?? DateTime.UtcNow);

            await using var sourceStream = new FileStream(
                sourcePath,
                new FileStreamOptions
                {
                    Access = FileAccess.Read,
                    Mode = FileMode.Open,
                    Share = FileShare.Read,
                    Options = FileOptions.SequentialScan,
                });
            await using var entryStream = entry.Open();
            await sourceStream.CopyToAsync(entryStream, cancellationToken);
        }

        foreach (var logPath in Directory.EnumerateFiles(logsDirectory).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var entryName = $"_logs/{Path.GetFileName(logPath)}";
            var logEntry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            logEntry.LastWriteTime = ToZipTimestamp(File.GetLastWriteTimeUtc(logPath));

            await using var logStream = new FileStream(
                logPath,
                new FileStreamOptions
                {
                    Access = FileAccess.Read,
                    Mode = FileMode.Open,
                    Share = FileShare.Read,
                    Options = FileOptions.SequentialScan,
                });
            await using var logEntryStream = logEntry.Open();
            await logStream.CopyToAsync(logEntryStream, cancellationToken);
        }

        var manifestEntry = archive.CreateEntry("_manifest.json", CompressionLevel.Optimal);
        manifestEntry.LastWriteTime = ZipEpoch;
        await using var manifestStream = manifestEntry.Open();
        await manifestRepository.SerializeAsync(manifestStream, manifest, cancellationToken);
    }

    private static DateTimeOffset ToZipTimestamp(DateTime value)
    {
        var utcValue = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        var timestamp = new DateTimeOffset(utcValue);
        return timestamp < ZipEpoch ? ZipEpoch : timestamp;
    }
}
