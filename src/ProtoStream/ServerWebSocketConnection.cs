using System.Net.WebSockets;
using System.Text;

namespace ProtoStream;

public class ServerWebSocketConnection : IWebSocketConnection
{
    private readonly WebSocket _ws;

    public ServerWebSocketConnection(WebSocket ws)
    {
        _ws = ws ?? throw new ArgumentNullException(nameof(ws));
    }

    public async Task SendAsync(string message, CancellationToken ct = default)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    public async Task<string> ReceiveAsync(CancellationToken ct = default)
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await _ws.ReceiveAsync(buffer, ct);
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(ms.ToArray(), 0, (int)ms.Length);
    }

    public async ValueTask DisposeAsync()
    {
        if (_ws.State != WebSocketState.Closed && _ws.State != WebSocketState.Aborted)
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            catch { }
        }
        _ws.Dispose();
    }
}
