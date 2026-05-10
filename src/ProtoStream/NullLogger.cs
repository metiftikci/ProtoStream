using Microsoft.Extensions.Logging;

namespace ProtoStream;

public sealed class NullLogger : ILogger
{
    public static readonly ILogger Instance = new NullLogger();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => false;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
    }
}
