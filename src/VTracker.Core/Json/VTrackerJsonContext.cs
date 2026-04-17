using System.Text.Json.Serialization;

namespace VTracker.Core;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ManifestDocument))]
[JsonSerializable(typeof(CompareResult))]
[JsonSerializable(typeof(CompareAddedFile))]
[JsonSerializable(typeof(CompareRemovedFile))]
[JsonSerializable(typeof(CompareCategoryBreakdown))]
public partial class VTrackerJsonContext : JsonSerializerContext
{
}
