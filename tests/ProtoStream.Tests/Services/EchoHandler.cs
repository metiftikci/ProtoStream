using ProtoStream;
using ProtoStream.Generated;

namespace ProtoStream.Tests.Services;

public class EchoHandler : EchoServiceHandler
{
    public EchoHandler(ConnectionContext context) : base(context) { }

    public override async Task<EchoResponse> Echo(EchoRequest request, CancellationToken ct = default)
    {
        await SendOnEchoEvent(new EchoEvent($"Echo: {request.Text}"), ct);
        await SendOnStatusEvent(new StatusEvent($"Processed: {request.Text}"), ct);
        return new EchoResponse($"Response: {request.Text}");
    }
}
