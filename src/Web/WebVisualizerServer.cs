using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StalkerALifeSandbox.Web
{
    public class WebVisualizerServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new();
        private readonly int _port;
        private Func<string, InspectorDTO?>? _inspectHandler;
        private Action<string, JsonElement>? _commandHandler;

        public WebVisualizerServer(int port = 8080)
        {
            _port = port;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
        }

        public void SetInspectHandler(Func<string, InspectorDTO?> handler) =>
            _inspectHandler = handler;

        public void SetCommandHandler(Action<string, JsonElement> handler) =>
            _commandHandler = handler;

        public void Start()
        {
            _listener.Start();
            Console.WriteLine($"[WebVisualizerServer] WebSocket server started on ws://localhost:{_port}/");
            Task.Run(AcceptConnectionsAsync);
        }

        /// <summary>Stops accepting connections, aborts open sockets, and releases the listener.</summary>
        public void Stop()
        {
            try
            {
                if (_listener.IsListening)
                    _listener.Stop();
                _listener.Close();
            }
            catch
            {
                // Already stopped/closed — nothing to do.
            }

            foreach (var socket in _clients.Values)
            {
                try { socket.Abort(); } catch { /* client already gone */ }
            }
            _clients.Clear();
        }

        /// <summary>Stops the listener and aborts open sockets. Equivalent to <see cref="Stop"/>; safe to call more than once.</summary>
        public void Dispose() => Stop();

        private async Task AcceptConnectionsAsync()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        var wsContext = await context.AcceptWebSocketAsync(null);
                        var clientId = Guid.NewGuid();
                        _clients.TryAdd(clientId, wsContext.WebSocket);
                        Console.WriteLine($"[WebVisualizerServer] Client {clientId} connected.");
                        
                        _ = Task.Run(() => HandleClientAsync(clientId, wsContext.WebSocket));
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
                catch (Exception ex) when (ex is ObjectDisposedException or HttpListenerException && !_listener.IsListening)
                {
                    // Expected during Stop() — the listener was disposed/closed.
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WebVisualizerServer] Accept error: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(Guid clientId, WebSocket socket)
        {
            var buffer = new byte[4096];
            var messageBuffer = new StringBuilder();
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
                        break;
                    }

                    if (result.MessageType != WebSocketMessageType.Text)
                        continue;

                    messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (!result.EndOfMessage)
                        continue;

                    var text = messageBuffer.ToString();
                    messageBuffer.Clear();
                    await TryHandleClientMessageAsync(socket, text);
                }
            }
            catch (WebSocketException)
            {
                // Client disconnected ungracefully
            }
            finally
            {
                _clients.TryRemove(clientId, out _);
                Console.WriteLine($"[WebVisualizerServer] Client {clientId} disconnected.");
            }
        }

        private async Task TryHandleClientMessageAsync(WebSocket socket, string text)
        {
            if (socket.State != WebSocketState.Open)
                return;

            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var typeProp))
                    return;

                var type = typeProp.GetString();
                if (type == "inspect")
                {
                    if (_inspectHandler == null) return;
                    if (!root.TryGetProperty("entityId", out var idProp))
                        return;

                    var entityId = idProp.GetString();
                    if (string.IsNullOrEmpty(entityId))
                        return;

                    var inspector = _inspectHandler(entityId);
                    if (inspector == null)
                        return;

                    var payload = JsonSerializer.Serialize(new
                    {
                        type = "inspector",
                        data = inspector
                    });
                    var bytes = Encoding.UTF8.GetBytes(payload);
                    await socket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                }
                else if (type != null)
                {
                    _commandHandler?.Invoke(type, root);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebVisualizerServer] Handler error: {ex.Message}");
            }
        }

        public async Task BroadcastFrameAsync(TelemetryFrame frame)
        {
            if (_clients.IsEmpty) return;

            var json = JsonSerializer.Serialize(frame);
            var buffer = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(buffer);

            foreach (var kvp in _clients)
            {
                var socket = kvp.Value;
                if (socket.State == WebSocketState.Open)
                {
                    try
                    {
                        await socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch
                    {
                        // Ignore send errors, client disconnect will be handled in HandleClientAsync
                    }
                }
            }
        }
    }
}
