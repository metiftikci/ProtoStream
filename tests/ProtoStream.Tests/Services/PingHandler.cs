using ProtoStream;
using ProtoStream.Generated;

namespace ProtoStream.Tests.Services;

public class PingHandler : PingServiceHandler
{
    public PingHandler(ConnectionContext context) : base(context) { }

    public override async Task<PingResponse> Ping(PingRequest request, CancellationToken ct = default)
    {
        await SendOnPongEvent(new PongEvent($"Pong: {request.Data}"), ct);
        return new PingResponse($"Result: {request.Data}");
    }
}
