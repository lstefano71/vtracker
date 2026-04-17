using System.Text;

namespace VTracker.Core;

public static class CompareTextFormatter
{
    public static string Format(CompareResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Added: {result.Summary.Added}");
        builder.AppendLine($"Removed: {result.Summary.Removed}");
        builder.AppendLine($"Updated: {result.Summary.Updated}");
        builder.AppendLine($"Provenance differences: {result.Summary.ProvenanceDifferences}");

        var hasDetails = result.Added.Length > 0 ||
            result.Removed.Length > 0 ||
            result.Updated.Length > 0 ||
            result.ProvenanceDifferences.Length > 0;

        if (hasDetails)
        {
            builder.AppendLine();
            foreach (var path in result.Added)
            {
                builder.AppendLine($"+ {path}");
            }

            foreach (var path in result.Removed)
            {
                builder.AppendLine($"- {path}");
            }

            foreach (var update in result.Updated)
            {
                builder.AppendLine($"~ {update.Path}");
            }

            foreach (var difference in result.ProvenanceDifferences)
            {
                builder.AppendLine($"! {difference}");
            }
        }

        return builder.ToString().TrimEnd();
    }
}
