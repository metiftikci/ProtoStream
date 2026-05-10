using System.Text.Json;

namespace ProtoStream;

public class ConnectionContext
{
    private readonly IWebSocketConnection _connection;

    public ConnectionContext(IWebSocketConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task SendEventAsync<T>(string method, T eventData, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(eventData, JsonHelper.Options);
        var envelope = MessageEnvelope.CreateEvent(method, json);
        await _connection.SendAsync(envelope.ToJson(), ct);
    }

    public async Task SendResponseAsync(string correlationId, string data, CancellationToken ct = default)
    {
        var envelope = MessageEnvelope.CreateResponse(correlationId, data);
        await _connection.SendAsync(envelope.ToJson(), ct);
    }
}
