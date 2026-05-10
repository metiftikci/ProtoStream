using ProtoStream;
using ProtoStream.Generated;
using ProtoStream.Example.Contracts;

namespace ProtoStream.Example.Server;

public class ChatHandler : ChatServiceHandler
{
    public ChatHandler(ConnectionContext context) : base(context) { }

    public override async Task<SendMessageResponse> SendMessage(SendMessageRequest request, CancellationToken ct = default)
    {
        var ackId = Guid.NewGuid();

        // Broadcast the message back to the client as an event
        await SendOnMessageReceived(new MessageReceivedEvent(
            request.UserName,
            request.Content,
            DateTimeOffset.UtcNow
        ), ct);

        return new SendMessageResponse(ackId, "Delivered");
    }
}
