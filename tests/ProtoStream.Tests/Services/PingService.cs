using ProtoStream;

namespace ProtoStream.Tests.Services;

[ProtoCommand]
public record PingRequest(string Data);

[ProtoCommand]
public record PingResponse(string Result);

[ProtoEvent]
public record PongEvent(string Message);

[ProtoService]
public interface IPingService
{
    Task<PingResponse> Ping(PingRequest request, CancellationToken ct = default);
    Task OnPongEvent(PongEvent evt, CancellationToken ct = default);
}
