using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ProtoStream;

public static class ProtoStreamEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapProtoStream<THandler>(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/ws")
        where THandler : class, IWebSocketHandler
    {
        return endpoints.Map(pattern, async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var ws = await context.WebSockets.AcceptWebSocketAsync();
            var connection = new ServerWebSocketConnection(ws);
            var handler = context.RequestServices.GetRequiredService<THandler>();
            await handler.HandleConnectionAsync(connection, context.RequestAborted);
        });
    }
}
