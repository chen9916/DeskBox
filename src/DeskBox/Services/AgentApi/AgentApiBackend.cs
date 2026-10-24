namespace DeskBox.Services.AgentApi;

/// <summary>WidgetManager-backed agent operations.</summary>
internal sealed class AgentApiBackend : IAgentApiBackend
{
    private readonly Func<WidgetManager?> _widgetManager;

    public AgentApiBackend(Func<WidgetManager?> widgetManager)
    {
        _widgetManager = widgetManager;
    }

    public Task<IReadOnlyList<AgentApiWidgetDto>> ListFileWidgetsAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(RequireManager().ListFileWidgetsForAgent());
    }

    public Task<AgentApiWidgetDto> CreateFileWidgetAsync(
        AgentApiCreateWidgetRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RequireManager().CreateFileWidgetForAgentAsync(request);
    }

    public Task<AgentApiWidgetDto> PatchFileWidgetAsync(
        string widgetId,
        AgentApiPatchWidgetRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RequireManager().PatchFileWidgetForAgentAsync(widgetId, request);
    }

    public Task DeleteFileWidgetAsync(string widgetId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RequireManager().DeleteFileWidgetForAgentAsync(widgetId);
    }

    public Task<IReadOnlyList<AgentApiStackDto>> ListStacksAsync(
        string widgetId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RequireManager().ListStacksForAgentAsync(widgetId);
    }

    public Task<AgentApiStackDto> CreateStackAsync(
        string widgetId,
        AgentApiCreateStackRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RequireManager().CreateStackForAgentAsync(widgetId, request);
    }

    public Task<AgentApiStackDto> PatchStackAsync(
        string widgetId,
        string stackKey,
        AgentApiPatchStackRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RequireManager().PatchStackForAgentAsync(widgetId, stackKey, request);
    }

    public Task DeleteStackAsync(
        string widgetId,
        string stackKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RequireManager().DeleteStackForAgentAsync(widgetId, stackKey);
    }

    private WidgetManager RequireManager() =>
        _widgetManager() ??
        throw new AgentApiException(503, "DeskBox WidgetManager is not ready yet.");
}
