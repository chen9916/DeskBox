using System.Text.Json;

namespace DeskBox.Services.AgentApi;

/// <summary>
/// Pure HTTP route table for the agent API. Kept free of WinUI so unit tests
/// can exercise request parsing against a fake backend.
/// </summary>
internal sealed class AgentApiRouter
{
    private readonly IAgentApiBackend _backend;
    private readonly int _port;

    public AgentApiRouter(IAgentApiBackend backend, int port)
    {
        _backend = backend;
        _port = port;
    }

    public async Task<AgentApiResponse> HandleAsync(
        string method,
        string path,
        string? query,
        string? body,
        CancellationToken cancellationToken)
    {
        try
        {
            string normalizedMethod = method.Trim().ToUpperInvariant();
            string normalizedPath = NormalizePath(path);

            if (normalizedMethod == "GET" && normalizedPath == "/health")
            {
                return Ok(new AgentApiHealthBody { Port = _port });
            }

            if (normalizedMethod == "GET" && normalizedPath == "/v1/widgets")
            {
                IReadOnlyList<AgentApiWidgetDto> widgets =
                    await _backend.ListFileWidgetsAsync(cancellationToken);
                return Ok(new AgentApiWidgetListBody { Widgets = widgets.ToList() });
            }

            if (normalizedMethod == "POST" && normalizedPath == "/v1/widgets")
            {
                AgentApiCreateWidgetRequest request =
                    DeserializeCreateWidget(body);
                AgentApiWidgetDto created =
                    await _backend.CreateFileWidgetAsync(request, cancellationToken);
                return Json(201, created, AgentApiJsonContext.Default.AgentApiWidgetDto);
            }

            if (TryMatchWidgetId(normalizedPath, "/v1/widgets/", out string widgetId, out string? widgetTail))
            {
                if (widgetTail is null)
                {
                    if (normalizedMethod == "PATCH")
                    {
                        AgentApiPatchWidgetRequest request =
                            DeserializeRequired<AgentApiPatchWidgetRequest>(
                                body,
                                AgentApiJsonContext.Default.AgentApiPatchWidgetRequest);
                        AgentApiWidgetDto updated =
                            await _backend.PatchFileWidgetAsync(
                                widgetId,
                                request,
                                cancellationToken);
                        return Ok(updated, AgentApiJsonContext.Default.AgentApiWidgetDto);
                    }

                    if (normalizedMethod == "DELETE")
                    {
                        await _backend.DeleteFileWidgetAsync(widgetId, cancellationToken);
                        return NoContent();
                    }
                }
                else if (widgetTail == "stacks")
                {
                    if (normalizedMethod == "GET")
                    {
                        IReadOnlyList<AgentApiStackDto> stacks =
                            await _backend.ListStacksAsync(widgetId, cancellationToken);
                        return Ok(new AgentApiStackListBody
                        {
                            WidgetId = widgetId,
                            Stacks = stacks.ToList()
                        });
                    }

                    if (normalizedMethod == "POST")
                    {
                        AgentApiCreateStackRequest request =
                            DeserializeRequired<AgentApiCreateStackRequest>(
                                body,
                                AgentApiJsonContext.Default.AgentApiCreateStackRequest);
                        AgentApiStackDto created =
                            await _backend.CreateStackAsync(
                                widgetId,
                                request,
                                cancellationToken);
                        return Json(201, created, AgentApiJsonContext.Default.AgentApiStackDto);
                    }
                }
                else if (widgetTail.StartsWith("stacks/", StringComparison.Ordinal))
                {
                    string stackKey = Uri.UnescapeDataString(widgetTail["stacks/".Length..]);
                    if (string.IsNullOrWhiteSpace(stackKey))
                    {
                        return Error(400, "stackKey is required.");
                    }

                    if (normalizedMethod == "PATCH")
                    {
                        AgentApiPatchStackRequest request =
                            DeserializeRequired<AgentApiPatchStackRequest>(
                                body,
                                AgentApiJsonContext.Default.AgentApiPatchStackRequest);
                        AgentApiStackDto updated =
                            await _backend.PatchStackAsync(
                                widgetId,
                                stackKey,
                                request,
                                cancellationToken);
                        return Ok(updated, AgentApiJsonContext.Default.AgentApiStackDto);
                    }

                    if (normalizedMethod == "DELETE")
                    {
                        await _backend.DeleteStackAsync(
                            widgetId,
                            stackKey,
                            cancellationToken);
                        return NoContent();
                    }
                }
            }

            _ = query;
            return Error(404, $"No route for {normalizedMethod} {normalizedPath}");
        }
        catch (AgentApiException ex)
        {
            return Error(ex.StatusCode, ex.Message, ex.Detail);
        }
        catch (JsonException ex)
        {
            return Error(400, "Invalid JSON body.", ex.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Error(500, "Agent API request failed.", ex.Message);
        }
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        string trimmed = path.Trim();
        int queryIndex = trimmed.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            trimmed = trimmed[..queryIndex];
        }

        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        while (trimmed.Length > 1 && trimmed.EndsWith('/'))
        {
            trimmed = trimmed[..^1];
        }

        return trimmed;
    }

    private static bool TryMatchWidgetId(
        string path,
        string prefix,
        out string widgetId,
        out string? remainder)
    {
        widgetId = string.Empty;
        remainder = null;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string rest = path[prefix.Length..];
        if (string.IsNullOrEmpty(rest))
        {
            return false;
        }

        int slash = rest.IndexOf('/');
        if (slash < 0)
        {
            widgetId = Uri.UnescapeDataString(rest);
            return !string.IsNullOrWhiteSpace(widgetId);
        }

        widgetId = Uri.UnescapeDataString(rest[..slash]);
        remainder = rest[(slash + 1)..];
        return !string.IsNullOrWhiteSpace(widgetId);
    }

    private static AgentApiCreateWidgetRequest DeserializeCreateWidget(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new AgentApiCreateWidgetRequest();
        }

        return JsonSerializer.Deserialize(
                   body,
                   AgentApiJsonContext.Default.AgentApiCreateWidgetRequest)
               ?? new AgentApiCreateWidgetRequest();
    }

    private static T DeserializeRequired<T>(
        string? body,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new AgentApiException(400, "JSON body is required.");
        }

        return JsonSerializer.Deserialize(body, typeInfo)
               ?? throw new AgentApiException(400, "JSON body is required.");
    }

    private static AgentApiResponse Ok<T>(
        T value,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) =>
        Json(200, value, typeInfo);

    private static AgentApiResponse Ok(AgentApiHealthBody value) =>
        Json(200, value, AgentApiJsonContext.Default.AgentApiHealthBody);

    private static AgentApiResponse Ok(AgentApiWidgetListBody value) =>
        Json(200, value, AgentApiJsonContext.Default.AgentApiWidgetListBody);

    private static AgentApiResponse Ok(AgentApiStackListBody value) =>
        Json(200, value, AgentApiJsonContext.Default.AgentApiStackListBody);

    private static AgentApiResponse Json<T>(
        int status,
        T value,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) =>
        new(status, JsonSerializer.Serialize(value, typeInfo));

    private static AgentApiResponse NoContent() =>
        new(204, string.Empty, "text/plain");

    private static AgentApiResponse Error(int status, string error, string? detail = null) =>
        new(
            status,
            JsonSerializer.Serialize(
                new AgentApiErrorBody { Error = error, Detail = detail },
                AgentApiJsonContext.Default.AgentApiErrorBody));
}

internal sealed class AgentApiException : Exception
{
    public AgentApiException(int statusCode, string message, string? detail = null)
        : base(message)
    {
        StatusCode = statusCode;
        Detail = detail;
    }

    public int StatusCode { get; }

    public string? Detail { get; }
}
