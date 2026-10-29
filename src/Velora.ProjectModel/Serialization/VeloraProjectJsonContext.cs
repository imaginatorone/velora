using System.Text.Json.Serialization;

namespace Velora.ProjectModel.Serialization;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(VeloraProjectDocument))]
internal sealed partial class VeloraProjectJsonContext : JsonSerializerContext;
