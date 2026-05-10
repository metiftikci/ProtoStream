using System.Threading.Channels;

namespace ProtoStream.Tests;

public sealed class InMemoryWebSocketConnection : IAsyncDisposable, IDisposable
{
    private readonly Channel<string> _clientToServer = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly Channel<string> _serverToClient = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private bool _disposed;

    public IWebSocketConnection ClientSide { get; }
    public IWebSocketConnection ServerSide { get; }

    public InMemoryWebSocketConnection()
    {
        ClientSide = new ChannelSide(_clientToServer.Writer, _serverToClient.Reader);
        ServerSide = new ChannelSide(_serverToClient.Writer, _clientToServer.Reader);
    }

    private sealed class ChannelSide : IWebSocketConnection
    {
        private readonly ChannelWriter<string> _writer;
        private readonly ChannelReader<string> _reader;

        public ChannelSide(ChannelWriter<string> writer, ChannelReader<string> reader)
        {
            _writer = writer;
            _reader = reader;
        }

        public async Task SendAsync(string message, CancellationToken ct = default)
        {
            await _writer.WriteAsync(message, ct);
        }

        public async Task<string> ReceiveAsync(CancellationToken ct = default)
        {
            return await _reader.ReadAsync(ct);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _clientToServer.Writer.TryComplete();
            _serverToClient.Writer.TryComplete();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _clientToServer.Writer.TryComplete();
            _serverToClient.Writer.TryComplete();
        }
    }
}
