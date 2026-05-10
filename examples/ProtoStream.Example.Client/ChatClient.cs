using ProtoStream;
using ProtoStream.Generated;
using ProtoStream.Example.Contracts;

namespace ProtoStream.Example.Client;

public class ChatClient
{
    private readonly ChatServiceClient _client;

    public ChatClient(ChatServiceClient client)
    {
        _client = client;
        _client.OnMessageReceived += OnMessageReceived;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        await _client.StartAsync(ct);
    }

    public async Task<SendMessageResponse> SendMessageAsync(string userName, string content, CancellationToken ct = default)
    {
        return await _client.SendMessage(new SendMessageRequest(userName, content), ct);
    }

    private void OnMessageReceived(MessageReceivedEvent evt)
    {
        Console.WriteLine($"[EVENT] {evt.Timestamp:HH:mm:ss} {evt.UserName}: {evt.Content}");
    }
}
