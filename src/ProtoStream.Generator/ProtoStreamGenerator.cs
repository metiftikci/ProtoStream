using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ProtoStream.Generator
{
    [Generator(LanguageNames.CSharp)]
    public class ProtoStreamGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var services = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: static (ctx, ct) => TransformService(ctx, ct))
                .Where(static x => x is not null)
                .Select(static (x, _) => x!);

            context.RegisterSourceOutput(services, Execute);
        }

        private static bool HasAttribute(ISymbol symbol, string name)
        {
            return symbol.GetAttributes().Any(a =>
            {
                var attrName = a.AttributeClass?.Name;
                return attrName == name || attrName == name + "Attribute";
            });
        }

        private static ServiceInfo? TransformService(GeneratorSyntaxContext ctx, CancellationToken ct)
        {
            var syntax = (InterfaceDeclarationSyntax)ctx.Node;
            var model = ctx.SemanticModel;
            var symbol = model.GetDeclaredSymbol(syntax, ct);
            if (symbol == null) return null;

            if (!HasAttribute(symbol, "ProtoService")) return null;

            var ns = symbol.ContainingNamespace?.ToDisplayString() ?? "";
            var interfaceName = symbol.Name;
            var serviceName = interfaceName.StartsWith("I") && interfaceName.Length > 1
                ? interfaceName.Substring(1)
                : interfaceName;

            var commands = new List<CommandInfo>();
            var events = new List<EventInfo>();

            foreach (var member in symbol.GetMembers().OfType<IMethodSymbol>())
            {
                if (member.MethodKind != MethodKind.Ordinary) continue;
                if (member.DeclaredAccessibility != Accessibility.Public) continue;

                // Find the non-CancellationToken parameter
                var dataParam = member.Parameters.FirstOrDefault(p => p.Type.Name != "CancellationToken");
                if (dataParam == null) continue;

                var paramType = dataParam.Type;
                var paramTypeSymbol = paramType is INamedTypeSymbol named ? named : null;
                if (paramTypeSymbol == null) continue;

                var hasProtoCommand = HasAttribute(paramTypeSymbol, "ProtoCommand");
                var hasProtoEvent = HasAttribute(paramTypeSymbol, "ProtoEvent");

                if (hasProtoCommand)
                {
                    var retType = member.ReturnType;
                    string? responseType = null;
                    if (retType is INamedTypeSymbol namedRet &&
                        namedRet.Name == "Task" &&
                        namedRet.TypeArguments.Length == 1)
                    {
                        responseType = namedRet.TypeArguments[0].ToDisplayString();
                    }
                    if (responseType == null) continue;

                    commands.Add(new CommandInfo
                    {
                        Name = member.Name,
                        RequestType = paramType.ToDisplayString(),
                        ResponseType = responseType,
                        RequestParamName = dataParam.Name
                    });
                }
                else if (hasProtoEvent)
                {
                    events.Add(new EventInfo
                    {
                        MethodName = member.Name,
                        EventType = paramType.ToDisplayString(),
                        EventSimpleName = paramTypeSymbol.Name
                    });
                }
            }

            if (commands.Count == 0 && events.Count == 0) return null;

            return new ServiceInfo
            {
                Namespace = ns,
                InterfaceName = symbol.ToDisplayString(),
                ServiceName = serviceName,
                Commands = commands,
                Events = events
            };
        }

        private static void Execute(SourceProductionContext ctx, ServiceInfo service)
        {
            ctx.AddSource($"{service.ServiceName}Client.g.cs",
                SourceText.From(GenerateClient(service), Encoding.UTF8));
            ctx.AddSource($"{service.ServiceName}Handler.g.cs",
                SourceText.From(GenerateHandler(service), Encoding.UTF8));
            ctx.AddSource($"{service.ServiceName}Router.g.cs",
                SourceText.From(GenerateRouter(service), Encoding.UTF8));
        }

        private static string GenerateClient(ServiceInfo service)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("#pragma warning disable CS0108, CS0114, CS8618");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Text.Json;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine("using ProtoStream;");
            if (!string.IsNullOrEmpty(service.Namespace))
                sb.AppendLine($"using {service.Namespace};");
            sb.AppendLine();
            sb.AppendLine("namespace ProtoStream.Generated");
            sb.AppendLine("{");

            var clientName = $"{service.ServiceName}Client";
            sb.AppendLine($"    public class {clientName} : ProtoStreamClient");
            sb.AppendLine("    {");

            sb.AppendLine($"        public {clientName}(IWebSocketConnection connection, ILogger<{clientName}>? logger = null) : base(connection, logger)");
            sb.AppendLine("        {");
            foreach (var evt in service.Events)
                sb.AppendLine($"            SubscribeToEvent<{evt.EventType}>(\"{evt.MethodName}\", evt => {evt.MethodName}?.Invoke(evt));");
            sb.AppendLine("        }");
            sb.AppendLine();

            foreach (var evt in service.Events)
                sb.AppendLine($"        public event Action<{evt.EventType}>? {evt.MethodName};");

            if (service.Events.Count > 0) sb.AppendLine();

            foreach (var cmd in service.Commands)
            {
                sb.AppendLine($"        public async Task<{cmd.ResponseType}> {cmd.Name}({cmd.RequestType} request, CancellationToken ct = default)");
                sb.AppendLine("        {");
                sb.AppendLine($"            return await SendCommandAsync<{cmd.RequestType}, {cmd.ResponseType}>(\"{cmd.Name}\", request, ct);");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string GenerateHandler(ServiceInfo service)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("#pragma warning disable CS0108, CS0114, CS8618");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Text.Json;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine("using ProtoStream;");
            if (!string.IsNullOrEmpty(service.Namespace))
                sb.AppendLine($"using {service.Namespace};");
            sb.AppendLine();
            sb.AppendLine("namespace ProtoStream.Generated");
            sb.AppendLine("{");

            var handlerName = $"{service.ServiceName}Handler";
            sb.AppendLine($"    public abstract class {handlerName} : IWebSocketHandler");
            sb.AppendLine("    {");
            sb.AppendLine("        private readonly ConnectionContext _context;");
            sb.AppendLine("        private readonly ILogger _logger;");
            sb.AppendLine();
            sb.AppendLine($"        protected {handlerName}(ConnectionContext context, ILogger<{handlerName}>? logger = null)");
            sb.AppendLine("        {");
            sb.AppendLine("            _context = context ?? throw new ArgumentNullException(nameof(context));");
            sb.AppendLine("            _logger = logger ?? NullLogger.Instance;");
            sb.AppendLine("        }");
            sb.AppendLine();

            foreach (var cmd in service.Commands)
            {
                sb.AppendLine($"        public abstract Task<{cmd.ResponseType}> {cmd.Name}({cmd.RequestType} request, CancellationToken ct = default);");
            }

            if (service.Commands.Count > 0 && service.Events.Count > 0)
                sb.AppendLine();

            foreach (var evt in service.Events)
            {
                sb.AppendLine($"        protected async Task Send{evt.MethodName}({evt.EventType} evt, CancellationToken ct = default)");
                sb.AppendLine("        {");
                sb.AppendLine($"            await _context.SendEventAsync(\"{evt.MethodName}\", evt, ct);");
                sb.AppendLine("        }");
            }

            if (service.Events.Count > 0) sb.AppendLine();

            sb.AppendLine("        async Task IWebSocketHandler.HandleConnectionAsync(IWebSocketConnection connection, CancellationToken ct)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _logger.LogInformation(\"Client connected\");");
            sb.AppendLine($"            var router = new {service.ServiceName}Router(this, connection, _logger);");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                await router.RunAsync(ct);");
            sb.AppendLine("            }");
            sb.AppendLine("            finally");
            sb.AppendLine("            {");
            sb.AppendLine($"                _logger.LogInformation(\"Client disconnected\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string GenerateRouter(ServiceInfo service)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("#pragma warning disable CS0108, CS0114, CS8618");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Text.Json;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine("using ProtoStream;");
            if (!string.IsNullOrEmpty(service.Namespace))
                sb.AppendLine($"using {service.Namespace};");
            sb.AppendLine();
            sb.AppendLine("namespace ProtoStream.Generated");
            sb.AppendLine("{");

            var routerName = $"{service.ServiceName}Router";
            sb.AppendLine($"    public class {routerName}");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly {service.ServiceName}Handler _handler;");
            sb.AppendLine("        private readonly IWebSocketConnection _connection;");
            sb.AppendLine("        private readonly ConnectionContext _context;");
            sb.AppendLine("        private readonly ILogger _logger;");
            sb.AppendLine();
            sb.AppendLine($"        public {routerName}({service.ServiceName}Handler handler, IWebSocketConnection connection, ILogger? logger = null)");
            sb.AppendLine("        {");
            sb.AppendLine("            _handler = handler ?? throw new ArgumentNullException(nameof(handler));");
            sb.AppendLine("            _connection = connection ?? throw new ArgumentNullException(nameof(connection));");
            sb.AppendLine("            _context = new ConnectionContext(connection, logger);");
            sb.AppendLine("            _logger = logger ?? NullLogger.Instance;");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public async Task RunAsync(CancellationToken ct)");
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                while (!ct.IsCancellationRequested)");
            sb.AppendLine("                {");
            sb.AppendLine("                    var message = await _connection.ReceiveAsync(ct);");
            sb.AppendLine("                    if (message == null) break;");
            sb.AppendLine();
            sb.AppendLine("                    _logger.LogTrace(\"Raw message received: {Message}\", message);");
            sb.AppendLine();
            sb.AppendLine("                    var envelope = MessageEnvelope.FromJson(message);");
            sb.AppendLine("                    if (envelope == null)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        _logger.LogWarning(\"Failed to deserialize message: {Message}\", message);");
            sb.AppendLine("                        continue;");
            sb.AppendLine("                    }");
            sb.AppendLine();
            sb.AppendLine("                    switch (envelope.Type)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        case MessageType.Command:");
            sb.AppendLine("                            await HandleCommand(envelope, ct);");
            sb.AppendLine("                            break;");
            sb.AppendLine("                        default:");
            sb.AppendLine("                            _logger.LogWarning(\"Received unknown message type '{Type}'\", envelope.Type);");
            sb.AppendLine("                            break;");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (OperationCanceledException) { }");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        private async Task HandleCommand(MessageEnvelope envelope, CancellationToken ct)");
            sb.AppendLine("        {");
            sb.AppendLine("            _logger.LogDebug(\"Received command '{Method}' with correlation '{CorrelationId}': {Data}\",");
            sb.AppendLine("                envelope.Method, envelope.CorrelationId, envelope.Data);");
            sb.AppendLine("            switch (envelope.Method)");
            sb.AppendLine("            {");
            foreach (var cmd in service.Commands)
            {
                sb.AppendLine($"                case \"{cmd.Name}\":");
                sb.AppendLine($"                    var request_{cmd.Name} = JsonSerializer.Deserialize<{cmd.RequestType}>(envelope.Data!, JsonHelper.Options);");
                sb.AppendLine($"                    _logger.LogDebug(\"Dispatching command '{cmd.Name}'\");");
                sb.AppendLine($"                    var response_{cmd.Name} = await _handler.{cmd.Name}(request_{cmd.Name}!, ct);");
                sb.AppendLine($"                    var responseJson_{cmd.Name} = JsonSerializer.Serialize(response_{cmd.Name}, JsonHelper.Options);");
                sb.AppendLine($"                    await _context.SendResponseAsync(envelope.CorrelationId!, responseJson_{cmd.Name}, ct);");
                sb.AppendLine($"                    break;");
            }
            sb.AppendLine("                default:");
            sb.AppendLine("                    _logger.LogWarning(\"Received unknown command method '{Method}'\", envelope.Method);");
            sb.AppendLine("                    break;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        internal class ServiceInfo
        {
            public string Namespace { get; set; } = "";
            public string InterfaceName { get; set; } = "";
            public string ServiceName { get; set; } = "";
            public List<CommandInfo> Commands { get; set; } = new();
            public List<EventInfo> Events { get; set; } = new();
        }

        internal class CommandInfo
        {
            public string Name { get; set; } = "";
            public string RequestType { get; set; } = "";
            public string ResponseType { get; set; } = "";
            public string RequestParamName { get; set; } = "";
        }

        internal class EventInfo
        {
            public string MethodName { get; set; } = "";
            public string EventType { get; set; } = "";
            public string EventSimpleName { get; set; } = "";
        }
    }
}
