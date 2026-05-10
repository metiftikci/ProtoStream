using System.Collections.Concurrent;
using System.Text.Json;

namespace ProtoStream;

public class ProtoStreamClient : IAsyncDisposable
{
    private readonly IWebSocketConnection _connection;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Action<string>> _eventHandlers = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _cts = new();
    private Task? _receiveTask;

    public ProtoStreamClient(IWebSocketConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
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

                var envelope = MessageEnvelope.FromJson(message);
                if (envelope == null) continue;

                switch (envelope.Type)
                {
                    case MessageType.CommandResponse:
                        if (!string.IsNullOrEmpty(envelope.CorrelationId) &&
                            _pending.TryRemove(envelope.CorrelationId, out var tcs))
                        {
                            tcs.TrySetResult(envelope.Data ?? "");
                        }
                        break;

                    case MessageType.Event:
                        if (!string.IsNullOrEmpty(envelope.Method) &&
                            _eventHandlers.TryGetValue(envelope.Method, out var handler))
                        {
                            handler(envelope.Data ?? "");
                        }
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

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[correlationId] = tcs;

        try
        {
            await _connection.SendAsync(envelope.ToJson(), ct);

            using var registration = ct.Register(() => tcs.TrySetCanceled(ct));
            var result = await tcs.Task;

            return JsonSerializer.Deserialize<TResponse>(result, JsonHelper.Options)!;
        }
        catch
        {
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
