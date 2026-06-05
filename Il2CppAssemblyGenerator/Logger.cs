
using System.IO.Compression;
using Microsoft.Extensions.Logging;

public class Logger : ILogger
{
    public static void LogInfo(object message)
    {
        Console.WriteLine(message);
    }

    public static void LogDebug(string message)
    {
        Console.WriteLine("Debug: " + message);
    }

    public static void LogWarning(string message)
    {
        Console.WriteLine("Warning: " + message);
    }

    public static void LogError(string message)
    {
        Console.WriteLine("Error: " + message);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Console.WriteLine(formatter(state, exception));
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        throw new NotImplementedException();
    }
}