using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ProtoStream;

public class ProtoStreamClient : IAsyncDisposable
{
    private readonly IWebSocketConnection _connection;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Action<string>> _eventHandlers = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _cts = new();
    private Task? _receiveTask;

    public ProtoStreamClient(IWebSocketConnection connection, ILogger? logger = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _receiveTask = ReceiveLoopAsync(_cts.Token);
        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        await _cts.CancelAsync();
        if (_receiveTask != null)
        {
            try { await _receiveTask; } catch (OperationCanceledException) { }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var message = await _connection.ReceiveAsync(ct);
                if (message == null) break;

                _logger.LogTrace("Raw message received: {Message}", message);

                var envelope = MessageEnvelope.FromJson(message);
                if (envelope == null) continue;

                switch (envelope.Type)
                {
                    case MessageType.CommandResponse:
                        _logger.LogDebug("Received response for correlation '{CorrelationId}'", envelope.CorrelationId);
                        if (!string.IsNullOrEmpty(envelope.CorrelationId) &&
                            _pending.TryRemove(envelope.CorrelationId, out var tcs))
                        {
                            tcs.TrySetResult(envelope.Data ?? "");
                        }
                        else
                        {
                            _logger.LogWarning("Received response for unknown correlation '{CorrelationId}'", envelope.CorrelationId);
                        }
                        break;

                    case MessageType.Event:
                        _logger.LogDebug("Received event '{Method}'", envelope.Method);
                        if (!string.IsNullOrEmpty(envelope.Method) &&
                            _eventHandlers.TryGetValue(envelope.Method, out var handler))
                        {
                            handler(envelope.Data ?? "");
                        }
                        else
                        {
                            _logger.LogWarning("Received unhandled event '{Method}'", envelope.Method);
                        }
                        break;

                    default:
                        _logger.LogWarning("Received unknown message type '{Type}'", envelope.Type);
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    protected async Task<TResponse> SendCommandAsync<TRequest, TResponse>(
        string method, TRequest request, CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid().ToString();
        var requestJson = JsonSerializer.Serialize(request, JsonHelper.Options);
        var envelope = MessageEnvelope.CreateCommand(method, correlationId, requestJson);

        _logger.LogDebug("Sending command '{Method}' with correlation '{CorrelationId}': {Data}",
            method, correlationId, requestJson);

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[correlationId] = tcs;

        try
        {
            await _connection.SendAsync(envelope.ToJson(), ct);

            using var registration = ct.Register(() => tcs.TrySetCanceled(ct));
            var result = await tcs.Task;

            _logger.LogDebug("Received response for command '{Method}' (correlation '{CorrelationId}'): {Data}",
                method, correlationId, result);

            return JsonSerializer.Deserialize<TResponse>(result, JsonHelper.Options)!;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Command '{Method}' (correlation '{CorrelationId}') was cancelled", method, correlationId);
            _pending.TryRemove(correlationId, out _);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command '{Method}' (correlation '{CorrelationId}') failed", method, correlationId);
            _pending.TryRemove(correlationId, out _);
            throw;
        }
    }

    protected void SubscribeToEvent<TEvent>(string method, Action<TEvent> handler)
    {
        _eventHandlers[method] = json =>
        {
            var evt = JsonSerializer.Deserialize<TEvent>(json, JsonHelper.Options);
            if (evt != null)
                handler(evt);
        };
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts.Dispose();
        await _connection.DisposeAsync();
    }
}
