using DeskBox.Services.AgentApi;

namespace DeskBox.Tests;

public sealed class AgentApiRouterTests
{
    [Fact]
    public async Task Health_ReturnsOkPayload()
    {
        var backend = new FakeAgentApiBackend();
        var router = new AgentApiRouter(backend, port: 17865);

        AgentApiResponse response = await router.HandleAsync(
            "GET",
            "/health",
            query: null,
            body: null,
            CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("deskbox-agent", response.JsonBody, StringComparison.Ordinal);
        Assert.Contains("17865", response.JsonBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateWidget_PostsThroughBackend()
    {
        var backend = new FakeAgentApiBackend();
        var router = new AgentApiRouter(backend, port: 17865);

        AgentApiResponse response = await router.HandleAsync(
            "POST",
            "/v1/widgets",
            query: null,
            body: """{"name":"Alpha","x":10,"y":20,"width":300,"height":400}""",
            CancellationToken.None);

        Assert.Equal(201, response.StatusCode);
        Assert.Equal(1, backend.Widgets.Count);
        Assert.Equal("Alpha", backend.Widgets[0].Name);
        Assert.Equal(10, backend.Widgets[0].X);
        Assert.Equal(300, backend.Widgets[0].Width);
    }

    [Fact]
    public async Task PatchAndDeleteWidget_RoundTrip()
    {
        var backend = new FakeAgentApiBackend();
        backend.Widgets.Add(new AgentApiWidgetDto
        {
            Id = "w1",
            Name = "Box",
            X = 1,
            Y = 2,
            Width = 100,
            Height = 200
        });
        var router = new AgentApiRouter(backend, port: 17865);

        AgentApiResponse patched = await router.HandleAsync(
            "PATCH",
            "/v1/widgets/w1",
            query: null,
            body: """{"x":50,"width":180}""",
            CancellationToken.None);
        Assert.Equal(200, patched.StatusCode);
        Assert.Equal(50, backend.Widgets[0].X);
        Assert.Equal(180, backend.Widgets[0].Width);

        AgentApiResponse deleted = await router.HandleAsync(
            "DELETE",
            "/v1/widgets/w1",
            query: null,
            body: null,
            CancellationToken.None);
        Assert.Equal(204, deleted.StatusCode);
        Assert.Empty(backend.Widgets);
    }

    [Fact]
    public async Task StackCreatePatchDelete_RoundTrip()
    {
        var backend = new FakeAgentApiBackend();
        backend.Widgets.Add(new AgentApiWidgetDto { Id = "w1", Name = "Files" });
        var router = new AgentApiRouter(backend, port: 17865);

        AgentApiResponse created = await router.HandleAsync(
            "POST",
            "/v1/widgets/w1/stacks",
            query: null,
            body: """{"memberPaths":["C:\\\\a.txt","C:\\\\b.txt"],"name":"Pair"}""",
            CancellationToken.None);
        Assert.Equal(201, created.StatusCode);
        Assert.Single(backend.Stacks);
        string stackKey = backend.Stacks[0].StackKey;

        AgentApiResponse patched = await router.HandleAsync(
            "PATCH",
            $"/v1/widgets/w1/stacks/{Uri.EscapeDataString(stackKey)}",
            query: null,
            body: """{"orderIndex":0,"expanded":true,"name":"Pair2"}""",
            CancellationToken.None);
        Assert.Equal(200, patched.StatusCode);
        Assert.Equal("Pair2", backend.Stacks[0].Name);
        Assert.True(backend.Stacks[0].IsExpanded);
        Assert.Equal(0, backend.LastStackOrderIndex);

        AgentApiResponse deleted = await router.HandleAsync(
            "DELETE",
            $"/v1/widgets/w1/stacks/{Uri.EscapeDataString(stackKey)}",
            query: null,
            body: null,
            CancellationToken.None);
        Assert.Equal(204, deleted.StatusCode);
        Assert.Empty(backend.Stacks);
    }

    [Fact]
    public async Task UnknownRoute_Returns404()
    {
        var router = new AgentApiRouter(new FakeAgentApiBackend(), port: 17865);
        AgentApiResponse response = await router.HandleAsync(
            "GET",
            "/v1/nope",
            query: null,
            body: null,
            CancellationToken.None);
        Assert.Equal(404, response.StatusCode);
    }

    private sealed class FakeAgentApiBackend : IAgentApiBackend
    {
        public List<AgentApiWidgetDto> Widgets { get; } = [];
        public List<AgentApiStackDto> Stacks { get; } = [];
        public int? LastStackOrderIndex { get; private set; }

        public Task<IReadOnlyList<AgentApiWidgetDto>> ListFileWidgetsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AgentApiWidgetDto>>(Widgets.ToList());

        public Task<AgentApiWidgetDto> CreateFileWidgetAsync(
            AgentApiCreateWidgetRequest request,
            CancellationToken cancellationToken)
        {
            var widget = new AgentApiWidgetDto
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = request.Name ?? "Deskbox",
                FolderPath = request.FolderPath,
                X = request.X ?? 100,
                Y = request.Y ?? 100,
                Width = request.Width ?? 280,
                Height = request.Height ?? 400,
                Kind = "File",
                IsVisible = true
            };
            Widgets.Add(widget);
            return Task.FromResult(widget);
        }

        public Task<AgentApiWidgetDto> PatchFileWidgetAsync(
            string widgetId,
            AgentApiPatchWidgetRequest request,
            CancellationToken cancellationToken)
        {
            AgentApiWidgetDto widget = Widgets.Single(item => item.Id == widgetId);
            if (request.X is double x) widget.X = x;
            if (request.Y is double y) widget.Y = y;
            if (request.Width is double width) widget.Width = width;
            if (request.Height is double height) widget.Height = height;
            if (!string.IsNullOrWhiteSpace(request.Name)) widget.Name = request.Name;
            return Task.FromResult(widget);
        }

        public Task DeleteFileWidgetAsync(string widgetId, CancellationToken cancellationToken)
        {
            Widgets.RemoveAll(item => item.Id == widgetId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AgentApiStackDto>> ListStacksAsync(
            string widgetId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AgentApiStackDto>>(Stacks.ToList());

        public Task<AgentApiStackDto> CreateStackAsync(
            string widgetId,
            AgentApiCreateStackRequest request,
            CancellationToken cancellationToken)
        {
            var stack = new AgentApiStackDto
            {
                StackKey = "Manual:" + Guid.NewGuid().ToString("N"),
                Name = request.Name ?? "Stack",
                IsManual = true,
                MemberPaths = request.MemberPaths.ToList()
            };
            Stacks.Add(stack);
            return Task.FromResult(stack);
        }

        public Task<AgentApiStackDto> PatchStackAsync(
            string widgetId,
            string stackKey,
            AgentApiPatchStackRequest request,
            CancellationToken cancellationToken)
        {
            AgentApiStackDto stack = Stacks.Single(item => item.StackKey == stackKey);
            if (!string.IsNullOrWhiteSpace(request.Name)) stack.Name = request.Name;
            if (request.Expanded is bool expanded) stack.IsExpanded = expanded;
            LastStackOrderIndex = request.OrderIndex;
            return Task.FromResult(stack);
        }

        public Task DeleteStackAsync(
            string widgetId,
            string stackKey,
            CancellationToken cancellationToken)
        {
            Stacks.RemoveAll(item => item.StackKey == stackKey);
            return Task.CompletedTask;
        }
    }
}
