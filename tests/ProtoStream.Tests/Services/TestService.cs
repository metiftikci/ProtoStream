using ProtoStream;

namespace ProtoStream.Tests.Services;

[ProtoCommand]
public record EchoRequest(string Text);

[ProtoCommand]
public record EchoResponse(string Text);

[ProtoEvent]
public record EchoEvent(string Message);

[ProtoEvent]
public record StatusEvent(string Status);

[ProtoService]
public interface IEchoService
{
    Task<EchoResponse> Echo(EchoRequest request, CancellationToken ct = default);
    Task OnEchoEvent(EchoEvent evt, CancellationToken ct = default);
    Task OnStatusEvent(StatusEvent evt, CancellationToken ct = default);
}
