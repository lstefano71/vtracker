using System.IO.Compression;
using System.Text.Json;

namespace VTracker.Core;

public sealed class ManifestRepository(
    PathNormalizer pathNormalizer,
    PathCollisionValidator pathCollisionValidator)
{
    public async Task WriteToPathAsync(ManifestDocument manifest, string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await SerializeAsync(stream, manifest, cancellationToken);
    }

    public Task SerializeAsync(Stream stream, ManifestDocument manifest, CancellationToken cancellationToken)
    {
        return JsonSerializer.SerializeAsync(stream, manifest, VTrackerJsonContext.Default.ManifestDocument, cancellationToken);
    }

    public async Task<ManifestDocument> LoadFromPathAsync(string path, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(path);
        return extension.ToLowerInvariant() switch
        {
            ".json" => await LoadFromJsonAsync(path, cancellationToken),
            ".zip" => await LoadFromArchiveAsync(path, cancellationToken),
            _ => throw new ManifestValidationException($"Input '{path}' must be either a '.json' manifest or a '.zip' archive."),
        };
    }

    private async Task<ManifestDocument> LoadFromJsonAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var manifest = await JsonSerializer.DeserializeAsync(stream, VTrackerJsonContext.Default.ManifestDocument, cancellationToken);
        return NormalizeAndValidate(manifest, path);
    }

    private async Task<ManifestDocument> LoadFromArchiveAsync(string path, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(path);
        var manifestEntries = archive.Entries
            .Where(entry => string.Equals(entry.FullName.Replace('\\', '/').Trim('/'), "_manifest.json", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (manifestEntries.Length != 1)
        {
            throw new ManifestValidationException($"Archive '{path}' must contain exactly one '_manifest.json' entry at the ZIP root.");
        }

        await using var manifestStream = manifestEntries[0].Open();
        var manifest = await JsonSerializer.DeserializeAsync(manifestStream, VTrackerJsonContext.Default.ManifestDocument, cancellationToken);
        return NormalizeAndValidate(manifest, path);
    }

    private ManifestDocument NormalizeAndValidate(ManifestDocument? manifest, string sourcePath)
    {
        if (manifest is null)
        {
            throw new ManifestValidationException($"Manifest '{sourcePath}' could not be read.");
        }

        if (manifest.SchemaVersion != 1)
        {
            throw new ManifestValidationException($"Manifest '{sourcePath}' uses unsupported schema version '{manifest.SchemaVersion}'.");
        }

        if (manifest.Tool is null || string.IsNullOrWhiteSpace(manifest.Tool.Name) || string.IsNullOrWhiteSpace(manifest.Tool.Version))
        {
            throw new ManifestValidationException($"Manifest '{sourcePath}' is missing tool metadata.");
        }

        if (manifest.Source is null || string.IsNullOrWhiteSpace(manifest.Source.MsiPath) || string.IsNullOrWhiteSpace(manifest.Source.MsiSha256))
        {
            throw new ManifestValidationException($"Manifest '{sourcePath}' is missing source metadata.");
        }

        if (manifest.Extraction is null || string.IsNullOrWhiteSpace(manifest.Extraction.Mode) || string.IsNullOrWhiteSpace(manifest.Extraction.Compression))
        {
            throw new ManifestValidationException($"Manifest '{sourcePath}' is missing extraction metadata.");
        }

        var patches = (manifest.Patches ?? Array.Empty<ManifestPatchInfo>())
            .Select(patch => new ManifestPatchInfo
            {
                Sequence = patch.Sequence,
                Path = patch.Path,
                Sha256 = patch.Sha256.ToLowerInvariant(),
            })
            .OrderBy(patch => patch.Sequence)
            .ToArray();

        var originalFiles = manifest.Files ?? Array.Empty<ManifestFileEntry>();
        var normalizedFiles = new ManifestFileEntry[originalFiles.Length];
        var collisionCandidates = new (string NormalizedPath, string SourcePath)[originalFiles.Length];
        for (var index = 0; index < originalFiles.Length; index++)
        {
            var file = originalFiles[index] ?? throw new ManifestValidationException($"Manifest '{sourcePath}' contains a null file entry.");
            if (string.IsNullOrWhiteSpace(file.Path))
            {
                throw new ManifestValidationException($"Manifest '{sourcePath}' contains a file entry without a path.");
            }

            if (string.IsNullOrWhiteSpace(file.Sha256))
            {
                throw new ManifestValidationException($"Manifest '{sourcePath}' contains a file entry without a SHA-256 hash.");
            }

            var normalizedPath = pathNormalizer.NormalizeRelativePath(file.Path);
            collisionCandidates[index] = (normalizedPath, file.Path);
            normalizedFiles[index] = new ManifestFileEntry
            {
                Path = normalizedPath,
                LastWriteTimeUtc = EnsureUtc(file.LastWriteTimeUtc),
                Size = file.Size,
                Sha256 = file.Sha256.ToLowerInvariant(),
                FileVersion = NormalizeOptionalValue(file.FileVersion),
                ProductVersion = NormalizeOptionalValue(file.ProductVersion),
            };
        }

        pathCollisionValidator.EnsureUnique(collisionCandidates);

        return new ManifestDocument
        {
            SchemaVersion = manifest.SchemaVersion,
            Tool = new ManifestToolInfo
            {
                Name = manifest.Tool.Name,
                Version = manifest.Tool.Version,
            },
            Source = new ManifestSourceInfo
            {
                MsiPath = manifest.Source.MsiPath,
                MsiSha256 = manifest.Source.MsiSha256.ToLowerInvariant(),
            },
            Patches = patches,
            Extraction = new ManifestExtractionInfo
            {
                Mode = manifest.Extraction.Mode,
                WorkDirKept = manifest.Extraction.WorkDirKept,
                Compression = manifest.Extraction.Compression,
                CreatedUtc = manifest.Extraction.CreatedUtc,
            },
            Files = normalizedFiles
                .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(file => file.Path, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
