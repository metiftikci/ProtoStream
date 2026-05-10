using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ProtoStream;

public class ConnectionContext
{
    private readonly IWebSocketConnection _connection;
    private readonly ILogger _logger;

    public ConnectionContext(IWebSocketConnection connection, ILogger? logger = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task SendEventAsync<T>(string method, T eventData, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(eventData, JsonHelper.Options);
        _logger.LogDebug("Sending event '{Method}': {Data}", method, json);
        var envelope = MessageEnvelope.CreateEvent(method, json);
        await _connection.SendAsync(envelope.ToJson(), ct);
    }

    public async Task SendResponseAsync(string correlationId, string data, CancellationToken ct = default)
    {
        _logger.LogDebug("Sending response for correlation '{CorrelationId}': {Data}", correlationId, data);
        var envelope = MessageEnvelope.CreateResponse(correlationId, data);
        await _connection.SendAsync(envelope.ToJson(), ct);
    }
}
