# ProtoStream

ProtoStream is a lightweight .NET library that lets you define real-time WebSocket APIs using simple C# interfaces and records. It uses a Roslyn source generator to automatically produce the networking boilerplate — clients, handlers, and routers — so you can focus on your application logic.

## How It Works

1. **Define a contract** — Create an interface decorated with `[ProtoService]` and use `[ProtoCommand]` / `[ProtoEvent]` records as message types.
2. **Generate code** — The source generator creates a typed client, an abstract handler, and a message router at compile time.
3. **Implement & host** — Implement the abstract handler on the server and host it via ASP.NET Core WebSockets.
4. **Connect** — Use the generated client with a `ClientWebSocketConnection` to talk to the server.

## Project Structure

```
ProtoStream/
├── src/
│   ├── ProtoStream/                  # Core library (attributes, abstractions, client)
│   ├── ProtoStream.AspNetCore/       # ASP.NET Core integration
│   └── ProtoStream.Generator/        # Roslyn source generator
├── tests/
│   └── ProtoStream.Tests/            # Unit & integration tests
├── examples/
│   ├── ProtoStream.Example.Contracts/# Shared chat service contract
│   ├── ProtoStream.Example.Server/   # ASP.NET API server
│   └── ProtoStream.Example.Client/   # Console client app
└── .github/workflows/dotnet.yml      # CI pipeline
```

## Quick Start

### 1. Define the Contract

```csharp
using ProtoStream;

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
```

> Add a project reference to `ProtoStream` and `ProtoStream.Generator` (as an analyzer).

### 2. Implement the Server

```csharp
using ProtoStream;
using ProtoStream.Generated;

public class ChatHandler : ChatServiceHandler
{
    public ChatHandler(ConnectionContext context) : base(context) { }

    public override async Task<SendMessageResponse> SendMessage(
        SendMessageRequest request, CancellationToken ct = default)
    {
        await SendOnMessageReceived(new MessageReceivedEvent(
            request.UserName, request.Content, DateTimeOffset.UtcNow), ct);

        return new SendMessageResponse(Guid.NewGuid(), "Delivered");
    }
}
```

Wire it up in `Program.cs`:

```csharp
using ProtoStream;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ChatHandler>();

var app = builder.Build();

app.UseWebSockets();
app.MapProtoStream<ChatHandler>("/ws");

app.Run();
```

### 3. Create the Client

```csharp
using ProtoStream;
using ProtoStream.Generated;

var uri = new Uri("ws://localhost:5000/ws");
await using var connection = new ClientWebSocketConnection(uri);
await connection.ConnectAsync();

await using var client = new ChatServiceClient(connection);
await client.StartAsync();

client.OnMessageReceived += evt =>
    Console.WriteLine($"[{evt.Timestamp:HH:mm:ss}] {evt.UserName}: {evt.Content}");

var response = await client.SendMessage(new SendMessageRequest("Alice", "Hello!"));
Console.WriteLine($"ACK: {response.Status}");
```

## Running the Examples

The repository includes a working chat example.

**Start the server:**

```bash
dotnet run --project examples/ProtoStream.Example.Server
```

**Run the client:**

```bash
dotnet run --project examples/ProtoStream.Example.Client
```

The client prompts for a username, then lets you send messages. You will see both server events (`MessageReceivedEvent`) and command responses (`SendMessageResponse`) printed in the console.

## Building & Testing

```bash
# Restore packages
dotnet restore

# Build the entire solution
dotnet build

# Run tests
dotnet test
```

## Attributes

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[ProtoService]` | `interface` | Marks a service contract for code generation |
| `[ProtoCommand]` | `class` / `record` | Marks a request or response DTO for commands |
| `[ProtoEvent]` | `class` / `record` | Marks a DTO that the server can push to the client |

## Requirements

- .NET 8.0+
- ASP.NET Core (for server projects)

## License

MIT
