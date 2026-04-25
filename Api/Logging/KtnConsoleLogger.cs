using System;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace KTNLocation.Api.Logging;

public static class KtnConsoleLogger
{
    public static LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    public static void WriteLog(string source, string level, string message)
    {
        var normalizedLevel = string.IsNullOrWhiteSpace(level)
            ? "INFO"
            : level.Trim().ToUpperInvariant();

        var parsedLevel = normalizedLevel switch
        {
            "TRACE" => LogLevel.Trace,
            "DEBUG" => LogLevel.Debug,
            "INFO" => LogLevel.Information,
            "WARN" => LogLevel.Warning,
            "ERROR" => LogLevel.Error,
            "CRIT" => LogLevel.Critical,
            _ => LogLevel.Information
        };

        if (parsedLevel < MinimumLevel)
        {
            return;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var safeSource = Markup.Escape(source);
        var safeMessage = Markup.Escape(message);

        var levelColor = normalizedLevel switch
        {
            "DEBUG" => "dodgerblue1",
            "INFO" => "green",
            "WARN" => "yellow",
            "ERROR" => "red",
            _ => "silver"
        };

        AnsiConsole.MarkupLine($"[grey][[{timestamp}]][/] [deepskyblue1][[{safeSource}]][/] [{levelColor}][[{normalizedLevel}]][/] {safeMessage}");
    }
}

public sealed class KtnConsoleLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new KtnLogger(categoryName);
    }

    public void Dispose() { }

    private sealed class KtnLogger : ILogger
    {
        private readonly string _categoryName;

        public KtnLogger(string categoryName)
        {
            var lastDot = categoryName.LastIndexOf('.');
            _categoryName = lastDot >= 0 ? categoryName.Substring(lastDot + 1) : categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var levelStr = logLevel switch
            {
                LogLevel.Trace => "TRACE",
                LogLevel.Debug => "DEBUG",
                LogLevel.Information => "INFO",
                LogLevel.Warning => "WARN",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "CRIT",
                _ => "INFO"
            };

            var message = formatter(state, exception);
            if (exception != null)
            {
                message += $"\n{exception}";
            }

            if (string.IsNullOrWhiteSpace(message)) return;

            KtnConsoleLogger.WriteLog(_categoryName, levelStr, message);
        }
    }
}
