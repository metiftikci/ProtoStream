using ProtoStream;
using ProtoStream.Generated;
using ProtoStream.Example.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ChatHandler>();

var app = builder.Build();

app.UseWebSockets();
app.MapProtoStream<ChatHandler>("/ws");

app.MapGet("/", () => "ProtoStream Example Server is running. Connect to /ws via WebSocket.");

app.Run();
