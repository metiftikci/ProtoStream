using ProtoStream;
using ProtoStream.Generated;
using ProtoStream.Example.Contracts;

namespace ProtoStream.Example.Client;

class Program
{
    static async Task Main(string[] args)
    {
        var uri = new Uri(args.Length > 0 ? args[0] : "ws://localhost:5000/ws");
        Console.WriteLine($"Connecting to {uri}...");

        await using var connection = new ClientWebSocketConnection(uri);
        await connection.ConnectAsync();

        await using var chatServiceClient = new ChatServiceClient(connection);
        var chatClient = new ChatClient(chatServiceClient);
        await chatClient.StartAsync();

        Console.WriteLine("Connected! Type messages and press Enter to send. Type 'exit' to quit.");
        Console.Write("Enter your name: ");
        var userName = Console.ReadLine() ?? "Anonymous";

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            try
            {
                var response = await chatClient.SendMessageAsync(userName, input);
                Console.WriteLine($"[ACK] {response.Status} (Id: {response.AckId})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
            }
        }

        Console.WriteLine("Disconnecting...");
    }
}
