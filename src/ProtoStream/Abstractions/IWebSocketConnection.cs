namespace ProtoStream;

public interface IWebSocketConnection : IAsyncDisposable
{
    Task SendAsync(string message, CancellationToken ct = default);
    Task<string> ReceiveAsync(CancellationToken ct = default);
}
