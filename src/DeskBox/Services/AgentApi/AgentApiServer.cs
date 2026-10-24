using System.Net;
using System.Text;

namespace DeskBox.Services.AgentApi;

/// <summary>
/// Loopback-only HTTP listener that lets an external AI agent drive file
/// widgets and stacks without touching the GUI. Bound to 127.0.0.1 only.
/// </summary>
internal sealed class AgentApiServer : IAsyncDisposable
{
    public const int DefaultPort = 17865;
    public const string DisabledEnvironmentVariable = "DESKBOX_AGENT_API";
    public const string PortEnvironmentVariable = "DESKBOX_AGENT_API_PORT";

    private readonly HttpListener _listener = new();
    private readonly AgentApiRouter _router;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private int _started;

    public AgentApiServer(IAgentApiBackend backend, int port)
    {
        Port = port;
        Prefix = $"http://127.0.0.1:{port}/";
        _router = new AgentApiRouter(backend, port);
        _listener.Prefixes.Add(Prefix);
    }

    public int Port { get; }

    public string Prefix { get; }

    public bool IsRunning => Volatile.Read(ref _started) == 1;

    public static bool IsEnabledByEnvironment()
    {
        string? raw = Environment.GetEnvironmentVariable(DisabledEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        return !string.Equals(raw, "0", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(raw, "off", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(raw, "disabled", StringComparison.OrdinalIgnoreCase);
    }

    public static int ResolvePort()
    {
        string? raw = Environment.GetEnvironmentVariable(PortEnvironmentVariable);
        if (int.TryParse(raw, out int port) && port is > 0 and < 65536)
        {
            return port;
        }

        return DefaultPort;
    }

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        try
        {
            _listener.Start();
        }
        catch
        {
            Volatile.Write(ref _started, 0);
            throw;
        }

        _loop = Task.Run(() => AcceptLoopAsync(_cts.Token));
        App.Log($"[AgentApi] Listening on {Prefix}");
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0 && _loop is null)
        {
            _cts.Dispose();
            return;
        }

        try
        {
            _cts.Cancel();
        }
        catch
        {
            // Ignore double-cancel during shutdown races.
        }

        try
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
            }
        }
        catch (Exception ex)
        {
            App.Log($"[AgentApi] Stop failed: {ex.Message}");
        }

        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                App.Log($"[AgentApi] Accept loop ended with: {ex.Message}");
            }
        }

        _listener.Close();
        _cts.Dispose();
        App.Log("[AgentApi] Stopped.");
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested || !_listener.IsListening)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                App.Log($"[AgentApi] Accept failed: {ex.Message}");
                continue;
            }

            _ = Task.Run(() => HandleContextAsync(context, cancellationToken), CancellationToken.None);
        }
    }

    private async Task HandleContextAsync(
        HttpListenerContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!IsLoopback(context.Request.RemoteEndPoint))
            {
                await WriteResponseAsync(
                        context.Response,
                        new AgentApiResponse(
                            403,
                            """{"error":"Agent API accepts loopback clients only."}"""),
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            string? body = null;
            if (context.Request.HasEntityBody)
            {
                using var reader = new StreamReader(
                    context.Request.InputStream,
                    context.Request.ContentEncoding ?? Encoding.UTF8);
                body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            }

            AgentApiResponse response = await _router.HandleAsync(
                    context.Request.HttpMethod ?? "GET",
                    context.Request.Url?.AbsolutePath ?? "/",
                    context.Request.Url?.Query,
                    body,
                    cancellationToken)
                .ConfigureAwait(false);

            await WriteResponseAsync(context.Response, response, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            App.Log($"[AgentApi] Request handling failed: {ex.Message}");
            try
            {
                await WriteResponseAsync(
                        context.Response,
                        new AgentApiResponse(
                            500,
                            """{"error":"Unhandled agent API failure."}"""),
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Response may already be closed.
            }
        }
        finally
        {
            try
            {
                context.Response.OutputStream.Close();
            }
            catch
            {
                // Ignore close races.
            }
        }
    }

    private static bool IsLoopback(IPEndPoint? remote)
    {
        if (remote is null)
        {
            return false;
        }

        return IPAddress.IsLoopback(remote.Address);
    }

    private static async Task WriteResponseAsync(
        HttpListenerResponse response,
        AgentApiResponse payload,
        CancellationToken cancellationToken)
    {
        byte[] bytes = string.IsNullOrEmpty(payload.JsonBody)
            ? []
            : Encoding.UTF8.GetBytes(payload.JsonBody);
        response.StatusCode = payload.StatusCode;
        response.ContentType = payload.ContentType;
        response.ContentLength64 = bytes.Length;
        response.Headers["Cache-Control"] = "no-store";
        if (bytes.Length > 0)
        {
            await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
    }
}
