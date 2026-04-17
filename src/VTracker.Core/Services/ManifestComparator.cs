namespace VTracker.Core;

public sealed class ManifestComparator
{
    public CompareResult Compare(ManifestDocument left, ManifestDocument right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var leftFiles = left.Files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
        var rightFiles = right.Files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);

        var added = rightFiles.Keys
            .Except(leftFiles.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToArray();

        var removed = leftFiles.Keys
            .Except(rightFiles.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToArray();

        var updated = leftFiles.Keys
            .Intersect(rightFiles.Keys, StringComparer.OrdinalIgnoreCase)
            .Select(path => (Path: path, Left: leftFiles[path], Right: rightFiles[path]))
            .Where(item => !string.Equals(item.Left.Sha256, item.Right.Sha256, StringComparison.OrdinalIgnoreCase))
            .Select(item => new CompareUpdatedFile
            {
                Path = item.Path,
                Left = CreateSnapshot(item.Left),
                Right = CreateSnapshot(item.Right),
            })
            .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .ToArray();

        var provenanceDifferences = CompareProvenance(left, right);
        return new CompareResult
        {
            Summary = new CompareSummary
            {
                Added = added.Length,
                Removed = removed.Length,
                Updated = updated.Length,
                ProvenanceDifferences = provenanceDifferences.Length,
            },
            Added = added,
            Removed = removed,
            Updated = updated,
            ProvenanceDifferences = provenanceDifferences,
        };
    }

    private static CompareFileSnapshot CreateSnapshot(ManifestFileEntry file)
    {
        return new CompareFileSnapshot
        {
            Sha256 = file.Sha256,
            Size = file.Size,
            FileVersion = file.FileVersion,
            ProductVersion = file.ProductVersion,
        };
    }

    private static string[] CompareProvenance(ManifestDocument left, ManifestDocument right)
    {
        var differences = new List<string>();

        if (!string.Equals(left.Tool.Name, right.Tool.Name, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(left.Tool.Version, right.Tool.Version, StringComparison.OrdinalIgnoreCase))
        {
            differences.Add("Tool metadata differs.");
        }

        if (!string.Equals(left.Source.MsiPath, right.Source.MsiPath, StringComparison.OrdinalIgnoreCase))
        {
            differences.Add("Source MSI path differs.");
        }

        if (!string.Equals(left.Source.MsiSha256, right.Source.MsiSha256, StringComparison.OrdinalIgnoreCase))
        {
            differences.Add("Source MSI hash differs.");
        }

        if (!PatchListsEqual(left.Patches, right.Patches))
        {
            differences.Add("Patch list differs.");
        }

        if (!string.Equals(left.Extraction.Mode, right.Extraction.Mode, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(left.Extraction.Compression, right.Extraction.Compression, StringComparison.OrdinalIgnoreCase) ||
            left.Extraction.WorkDirKept != right.Extraction.WorkDirKept)
        {
            differences.Add("Extraction metadata differs.");
        }

        return differences.ToArray();
    }

    private static bool PatchListsEqual(ManifestPatchInfo[] left, ManifestPatchInfo[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            var leftPatch = left[index];
            var rightPatch = right[index];
            if (leftPatch.Sequence != rightPatch.Sequence ||
                !string.Equals(leftPatch.Path, rightPatch.Path, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(leftPatch.Sha256, rightPatch.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
