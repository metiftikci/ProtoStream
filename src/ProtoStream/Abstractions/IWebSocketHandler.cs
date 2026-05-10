namespace ProtoStream;

public interface IWebSocketHandler
{
    Task HandleConnectionAsync(IWebSocketConnection connection, CancellationToken ct);
}
