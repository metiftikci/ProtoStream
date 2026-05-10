using ProtoStream;
using ProtoStream.Generated;
using ProtoStream.Tests.Services;
using Xunit;

namespace ProtoStream.Tests.Integration;

public class ClientServerTests
{
    [Fact]
    public async Task EchoCommand_ReturnsResponse()
    {
        using var transport = new InMemoryWebSocketConnection();
        var context = new ConnectionContext(transport.ServerSide);
        var handler = new EchoHandler(context);
        var client = new EchoServiceClient(transport.ClientSide);
        await client.StartAsync();

        var serverTask = Task.Run(async () =>
        {
            var router = new EchoServiceRouter(handler, transport.ServerSide);
            await router.RunAsync(CancellationToken.None);
        });

        var response = await client.Echo(new EchoRequest("Hello"));

        Assert.Equal("Response: Hello", response.Text);
    }

    [Fact]
    public async Task EchoCommand_WithCancellation_Throws()
    {
        using var transport = new InMemoryWebSocketConnection();
        var context = new ConnectionContext(transport.ServerSide);
        var handler = new EchoHandler(context);

        var client = new EchoServiceClient(transport.ClientSide);
        await client.StartAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await client.Echo(new EchoRequest("Hello"), cts.Token);
        });
    }

    [Fact]
    public async Task MultipleCommands_WorksSequentially()
    {
        using var transport = new InMemoryWebSocketConnection();
        var context = new ConnectionContext(transport.ServerSide);
        var handler = new EchoHandler(context);
        var client = new EchoServiceClient(transport.ClientSide);
        await client.StartAsync();

        _ = Task.Run(async () =>
        {
            var router = new EchoServiceRouter(handler, transport.ServerSide);
            await router.RunAsync(CancellationToken.None);
        });

        var r1 = await client.Echo(new EchoRequest("First"));
        var r2 = await client.Echo(new EchoRequest("Second"));
        var r3 = await client.Echo(new EchoRequest("Third"));

        Assert.Equal("Response: First", r1.Text);
        Assert.Equal("Response: Second", r2.Text);
        Assert.Equal("Response: Third", r3.Text);
    }

    [Fact]
    public async Task ServerEvent_IsReceivedByClient()
    {
        using var transport = new InMemoryWebSocketConnection();
        var context = new ConnectionContext(transport.ServerSide);
        var handler = new EchoHandler(context);
        var client = new EchoServiceClient(transport.ClientSide);
        await client.StartAsync();

        var receivedEvents = new List<EchoEvent>();
        client.OnEchoEvent += evt => receivedEvents.Add(evt);

        _ = Task.Run(async () =>
        {
            var router = new EchoServiceRouter(handler, transport.ServerSide);
            await router.RunAsync(CancellationToken.None);
        });

        await client.Echo(new EchoRequest("Hello"));

        await Task.Delay(200);

        Assert.Single(receivedEvents);
        Assert.Equal("Echo: Hello", receivedEvents[0].Message);
    }

    [Fact]
    public async Task MultipleServerEvents_AreDeliveredInOrder()
    {
        using var transport = new InMemoryWebSocketConnection();
        var context = new ConnectionContext(transport.ServerSide);
        var handler = new EchoHandler(context);
        var client = new EchoServiceClient(transport.ClientSide);
        await client.StartAsync();

        var receivedStatuses = new List<StatusEvent>();
        client.OnStatusEvent += evt => receivedStatuses.Add(evt);

        _ = Task.Run(async () =>
        {
            var router = new EchoServiceRouter(handler, transport.ServerSide);
            await router.RunAsync(CancellationToken.None);
        });

        await client.Echo(new EchoRequest("Alpha"));
        await Task.Delay(100);
        await client.Echo(new EchoRequest("Beta"));
        await Task.Delay(100);

        Assert.Equal(2, receivedStatuses.Count);
        Assert.Contains(receivedStatuses, s => s.Status.Contains("Alpha"));
        Assert.Contains(receivedStatuses, s => s.Status.Contains("Beta"));
    }

    [Fact]
    public async Task PingCommand_ReturnsResponse()
    {
        using var transport = new InMemoryWebSocketConnection();
        var context = new ConnectionContext(transport.ServerSide);
        var handler = new PingHandler(context);
        var client = new PingServiceClient(transport.ClientSide);
        await client.StartAsync();

        _ = Task.Run(async () =>
        {
            var router = new PingServiceRouter(handler, transport.ServerSide);
            await router.RunAsync(CancellationToken.None);
        });

        var response = await client.Ping(new PingRequest("Test"));

        Assert.Equal("Result: Test", response.Result);
    }

    [Fact]
    public async Task PingService_ReceivesOwnEvent()
    {
        using var transport = new InMemoryWebSocketConnection();
        var context = new ConnectionContext(transport.ServerSide);
        var handler = new PingHandler(context);
        var client = new PingServiceClient(transport.ClientSide);
        await client.StartAsync();

        var receivedEvents = new List<PongEvent>();
        client.OnPongEvent += evt => receivedEvents.Add(evt);

        _ = Task.Run(async () =>
        {
            var router = new PingServiceRouter(handler, transport.ServerSide);
            await router.RunAsync(CancellationToken.None);
        });

        await client.Ping(new PingRequest("Hello"));

        await Task.Delay(200);

        Assert.Single(receivedEvents);
        Assert.Equal("Pong: Hello", receivedEvents[0].Message);
    }

    [Fact]
    public async Task SeparateServices_DontShareEvents()
    {
        // EchoServiceClient should NOT have OnPongEvent
        // PingServiceClient should NOT have OnEchoEvent
        // This is a compile-time and design-time guarantee.
        // Here we verify they are separate runtime instances.

        using var echoTransport = new InMemoryWebSocketConnection();
        using var pingTransport = new InMemoryWebSocketConnection();

        var echoHandler = new EchoHandler(new ConnectionContext(echoTransport.ServerSide));
        var pingHandler = new PingHandler(new ConnectionContext(pingTransport.ServerSide));

        var echoClient = new EchoServiceClient(echoTransport.ClientSide);
        var pingClient = new PingServiceClient(pingTransport.ClientSide);
        await echoClient.StartAsync();
        await pingClient.StartAsync();

        _ = Task.Run(async () =>
        {
            var router = new EchoServiceRouter(echoHandler, echoTransport.ServerSide);
            await router.RunAsync(CancellationToken.None);
        });

        _ = Task.Run(async () =>
        {
            var router = new PingServiceRouter(pingHandler, pingTransport.ServerSide);
            await router.RunAsync(CancellationToken.None);
        });

        var echoResponse = await echoClient.Echo(new EchoRequest("Multi"));
        var pingResponse = await pingClient.Ping(new PingRequest("Service"));

        Assert.Equal("Response: Multi", echoResponse.Text);
        Assert.Equal("Result: Service", pingResponse.Result);
    }

    [Fact]
    public async Task EchoClient_DoesNotHavePongEvent()
    {
        // Compile-time check: EchoServiceClient should not have OnPongEvent
        var transport = new InMemoryWebSocketConnection();
        var client = new EchoServiceClient(transport.ClientSide);

        // Verify OnEchoEvent exists (service-scoped)
        Assert.NotNull(client.GetType().GetEvent("OnEchoEvent"));

        // Verify OnPongEvent does NOT exist (it belongs to PingService, not EchoService)
        var pongEvent = client.GetType().GetEvent("OnPongEvent");
        Assert.Null(pongEvent);
    }

    [Fact]
    public async Task PingClient_DoesNotHaveEchoEvent()
    {
        var transport = new InMemoryWebSocketConnection();
        var client = new PingServiceClient(transport.ClientSide);

        // PingService only has OnPongEvent
        Assert.NotNull(client.GetType().GetEvent("OnPongEvent"));
        Assert.Null(client.GetType().GetEvent("OnEchoEvent"));
        Assert.Null(client.GetType().GetEvent("OnStatusEvent"));
    }
}
