using System.Text.Json.Serialization;

namespace VTracker.Core;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ManifestDocument))]
[JsonSerializable(typeof(CompareResult))]
public partial class VTrackerJsonContext : JsonSerializerContext
{
}
