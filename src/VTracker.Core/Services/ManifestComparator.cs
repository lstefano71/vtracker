namespace VTracker.Core;

public sealed class ManifestComparator(CatalogClassifier catalogClassifier)
{
    public CompareResult Compare(ManifestDocument left, ManifestDocument right, CatalogFile? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var leftFiles = left.Files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
        var rightFiles = right.Files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);

        var added = rightFiles.Keys
            .Except(leftFiles.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal)
            .Select(path => new CompareAddedFile
            {
                Path = path,
                Category = ResolveCategory(rightFiles[path], path, catalog),
            })
            .ToArray();

        var removed = leftFiles.Keys
            .Except(rightFiles.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal)
            .Select(path => new CompareRemovedFile
            {
                Path = path,
                Category = ResolveCategory(leftFiles[path], path, catalog),
            })
            .ToArray();

        var commonPaths = leftFiles.Keys
            .Intersect(rightFiles.Keys, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var updated = commonPaths
            .Select(path => (Path: path, Left: leftFiles[path], Right: rightFiles[path]))
            .Where(item => !string.Equals(item.Left.Sha256, item.Right.Sha256, StringComparison.OrdinalIgnoreCase))
            .Select(item => new CompareUpdatedFile
            {
                Path = item.Path,
                Left = CreateSnapshot(item.Left),
                Right = CreateSnapshot(item.Right),
                Category = ResolveCategory(item.Right, item.Path, catalog),
            })
            .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .ToArray();

        var provenanceDifferences = CompareProvenance(left, right);

        // Detect category-only differences (same path, same SHA, different stored category)
        if (catalog is not null)
        {
            var categoryDiffs = commonPaths
                .Where(path =>
                {
                    var leftCat = leftFiles[path].Category;
                    var rightCat = rightFiles[path].Category;
                    return !string.Equals(leftCat, rightCat, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(leftFiles[path].Sha256, rightFiles[path].Sha256, StringComparison.OrdinalIgnoreCase);
                })
                .Select(path =>
                {
                    var leftCat = leftFiles[path].Category ?? "(none)";
                    var rightCat = rightFiles[path].Category ?? "(none)";
                    return $"Category changed for '{path}': '{leftCat}' → '{rightCat}'.";
                })
                .ToArray();

            if (categoryDiffs.Length > 0)
            {
                provenanceDifferences = [.. provenanceDifferences, .. categoryDiffs];
            }
        }

        // Build per-category breakdown when catalog is active
        CompareCategoryBreakdown[]? categoryBreakdown = null;
        if (catalog is not null)
        {
            var allCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in added) if (a.Category is not null) allCategories.Add(a.Category);
            foreach (var r in removed) if (r.Category is not null) allCategories.Add(r.Category);
            foreach (var u in updated) if (u.Category is not null) allCategories.Add(u.Category);

            categoryBreakdown = allCategories
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .Select(cat => new CompareCategoryBreakdown
                {
                    Category = cat,
                    Added = added.Count(a => string.Equals(a.Category, cat, StringComparison.OrdinalIgnoreCase)),
                    Removed = removed.Count(r => string.Equals(r.Category, cat, StringComparison.OrdinalIgnoreCase)),
                    Updated = updated.Count(u => string.Equals(u.Category, cat, StringComparison.OrdinalIgnoreCase)),
                })
                .ToArray();
        }

        return new CompareResult
        {
            Summary = new CompareSummary
            {
                Added = added.Length,
                Removed = removed.Length,
                Updated = updated.Length,
                ProvenanceDifferences = provenanceDifferences.Length,
                CategoryBreakdown = categoryBreakdown,
            },
            Added = added,
            Removed = removed,
            Updated = updated,
            ProvenanceDifferences = provenanceDifferences,
        };
    }

    /// <summary>
    /// Resolves the category for a file: catalog classification takes precedence,
    /// then stored manifest category, then null (no catalog active).
    /// </summary>
    private string? ResolveCategory(ManifestFileEntry entry, string path, CatalogFile? catalog)
    {
        if (catalog is not null)
        {
            return catalogClassifier.Classify(catalog, path);
        }

        return entry.Category;
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
