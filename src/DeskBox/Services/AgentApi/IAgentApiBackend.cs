namespace DeskBox.Services.AgentApi;

/// <summary>
/// Operations the localhost agent API exposes. Implemented against live
/// <see cref="WidgetManager"/> paths so layout persistence stays shared.
/// </summary>
internal interface IAgentApiBackend
{
    Task<IReadOnlyList<AgentApiWidgetDto>> ListFileWidgetsAsync(CancellationToken cancellationToken);

    Task<AgentApiWidgetDto> CreateFileWidgetAsync(
        AgentApiCreateWidgetRequest request,
        CancellationToken cancellationToken);

    Task<AgentApiWidgetDto> PatchFileWidgetAsync(
        string widgetId,
        AgentApiPatchWidgetRequest request,
        CancellationToken cancellationToken);

    Task DeleteFileWidgetAsync(string widgetId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentApiStackDto>> ListStacksAsync(
        string widgetId,
        CancellationToken cancellationToken);

    Task<AgentApiStackDto> CreateStackAsync(
        string widgetId,
        AgentApiCreateStackRequest request,
        CancellationToken cancellationToken);

    Task<AgentApiStackDto> PatchStackAsync(
        string widgetId,
        string stackKey,
        AgentApiPatchStackRequest request,
        CancellationToken cancellationToken);

    Task DeleteStackAsync(
        string widgetId,
        string stackKey,
        CancellationToken cancellationToken);
}
