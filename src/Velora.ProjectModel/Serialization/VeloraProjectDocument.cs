using System.Text.Json.Serialization;

namespace Velora.ProjectModel.Serialization;

internal sealed class VeloraProjectDocument
{
    [JsonPropertyName("formatVersion")]
    public int? FormatVersion { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("entryScript")]
    public string? EntryScript { get; init; }

    [JsonPropertyName("assetsDirectory")]
    public string? AssetsDirectory { get; init; }
}
