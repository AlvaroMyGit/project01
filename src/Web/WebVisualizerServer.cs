using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StalkerALifeSandbox.Web
{
    /// <summary>
    /// Tracks connected WebSocket clients and broadcasts telemetry frames to them.
    /// The socket itself is accepted by ASP.NET Core's WebSocket middleware (see
    /// <see cref="WebApiEndpoints"/>'s <c>/ws</c> mapping) — this class no longer
    /// owns a listener or a port; it is a passive hub handed already-accepted
    /// <see cref="WebSocket"/> instances via <see cref="HandleConnectionAsync"/>.
    /// </summary>
    public class WebVisualizerServer : IDisposable
    {
        private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new();
        private Func<string, InspectorDTO?>? _inspectHandler;
        private Action<string, JsonElement>? _commandHandler;

        public void SetInspectHandler(Func<string, InspectorDTO?> handler) =>
            _inspectHandler = handler;

        public void SetCommandHandler(Action<string, JsonElement> handler) =>
            _commandHandler = handler;

        /// <summary>
        /// Registers an already-accepted WebSocket connection and services it
        /// until the client disconnects. Awaited directly by the Kestrel request
        /// delegate that accepted the upgrade, per the standard ASP.NET Core
        /// WebSocket pattern.
        /// </summary>
        public async Task HandleConnectionAsync(WebSocket socket)
        {
            var clientId = Guid.NewGuid();
            _clients.TryAdd(clientId, socket);
            Console.WriteLine($"[WebVisualizerServer] Client {clientId} connected.");

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

        /// <summary>Aborts all tracked client connections. Safe to call more than once.</summary>
        public void Stop()
        {
            foreach (var socket in _clients.Values)
            {
                try { socket.Abort(); } catch { /* client already gone */ }
            }
            _clients.Clear();
        }

        /// <summary>Aborts all tracked client connections. Equivalent to <see cref="Stop"/>.</summary>
        public void Dispose() => Stop();

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
                        // Ignore send errors, client disconnect will be handled in HandleConnectionAsync
                    }
                }
            }
        }
    }
}
