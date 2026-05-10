using ProtoStream;

namespace ProtoStream.Example.Contracts;

[ProtoCommand]
public record SendMessageRequest(string UserName, string Content);

[ProtoCommand]
public record SendMessageResponse(Guid AckId, string Status);

[ProtoEvent]
public record MessageReceivedEvent(string UserName, string Content, DateTimeOffset Timestamp);

[ProtoService]
public interface IChatService
{
    Task<SendMessageResponse> SendMessage(SendMessageRequest request, CancellationToken ct = default);
    Task OnMessageReceived(MessageReceivedEvent evt, CancellationToken ct = default);
}
