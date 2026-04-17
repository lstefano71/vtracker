using System.Text;

namespace VTracker.Core;

public static class CompareTextFormatter
{
    public static string Format(CompareResult result, int hiddenCount = 0)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Added: {result.Summary.Added}");
        builder.AppendLine($"Removed: {result.Summary.Removed}");
        builder.AppendLine($"Updated: {result.Summary.Updated}");
        builder.AppendLine($"Provenance differences: {result.Summary.ProvenanceDifferences}");

        if (result.Summary.CategoryBreakdown is { Length: > 0 } breakdown)
        {
            builder.AppendLine();
            builder.AppendLine("Per-category breakdown:");
            foreach (var cat in breakdown)
            {
                builder.AppendLine($"  {cat.Category}: +{cat.Added} -{cat.Removed} ~{cat.Updated}");
            }
        }

        var hasDetails = result.Added.Length > 0 ||
            result.Removed.Length > 0 ||
            result.Updated.Length > 0 ||
            result.ProvenanceDifferences.Length > 0;

        if (hasDetails)
        {
            builder.AppendLine();
            foreach (var added in result.Added)
            {
                builder.AppendLine(added.Category is not null
                    ? $"+ {added.Path}  [{added.Category}]"
                    : $"+ {added.Path}");
            }

            foreach (var removed in result.Removed)
            {
                builder.AppendLine(removed.Category is not null
                    ? $"- {removed.Path}  [{removed.Category}]"
                    : $"- {removed.Path}");
            }

            foreach (var update in result.Updated)
            {
                builder.AppendLine(update.Category is not null
                    ? $"~ {update.Path}  [{update.Category}]"
                    : $"~ {update.Path}");
            }

            foreach (var difference in result.ProvenanceDifferences)
            {
                builder.AppendLine($"! {difference}");
            }
        }

        if (hiddenCount > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"({hiddenCount} file {(hiddenCount == 1 ? "row" : "rows")} hidden by --include filter)");
        }

        return builder.ToString().TrimEnd();
    }
}
