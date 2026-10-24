using System.Text.Json.Serialization;

namespace DeskBox.Services.AgentApi;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(AgentApiErrorBody))]
[JsonSerializable(typeof(AgentApiHealthBody))]
[JsonSerializable(typeof(AgentApiWidgetListBody))]
[JsonSerializable(typeof(AgentApiWidgetDto))]
[JsonSerializable(typeof(AgentApiCreateWidgetRequest))]
[JsonSerializable(typeof(AgentApiPatchWidgetRequest))]
[JsonSerializable(typeof(AgentApiStackListBody))]
[JsonSerializable(typeof(AgentApiStackDto))]
[JsonSerializable(typeof(AgentApiCreateStackRequest))]
[JsonSerializable(typeof(AgentApiPatchStackRequest))]
internal sealed partial class AgentApiJsonContext : JsonSerializerContext
{
}
