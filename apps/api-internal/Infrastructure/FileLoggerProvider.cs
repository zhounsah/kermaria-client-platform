using System.Collections.Concurrent;
using System.Text.Json;

namespace Kermaria.ApiInternal.Infrastructure;

public sealed class FileLoggerOptions
{
    public string Directory { get; init; } = "logs";
    public int RetentionDays { get; init; } = 30;
    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;
}

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly FileLoggerOptions _options;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly object _writerLock = new();
    private StreamWriter? _writer;
    private string _currentDate = string.Empty;

    public FileLoggerProvider(FileLoggerOptions options)
    {
        _options = options;
        System.IO.Directory.CreateDirectory(options.Directory);
        PurgeOldLogs();
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(
            categoryName,
            name => new FileLogger(name, _options, Write));

    public void Dispose()
    {
        foreach (var logger in _loggers.Values)
        {
            logger.Dispose();
        }

        lock (_writerLock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void Write(string line)
    {
        lock (_writerLock)
        {
            EnsureWriter();
            _writer?.WriteLine(line);
            _writer?.Flush();
        }
    }

    private void EnsureWriter()
    {
        var today = KermariaTimeZone.Now.ToString("yyyy-MM-dd");
        if (_currentDate == today && _writer is not null)
        {
            return;
        }

        _writer?.Dispose();
        _currentDate = today;
        var path = Path.Combine(
            _options.Directory,
            $"api-internal-{today}.log");
        _writer = new StreamWriter(
            new FileStream(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read),
            System.Text.Encoding.UTF8)
        {
            AutoFlush = false
        };
    }

    private void PurgeOldLogs()
    {
        if (_options.RetentionDays <= 0)
        {
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
        foreach (var file in System.IO.Directory.EnumerateFiles(
            _options.Directory,
            "api-internal-*.log"))
        {
            try
            {
                if (File.GetCreationTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // best-effort — don't crash startup on purge failure
            }
        }
    }
}

public sealed class FileLogger : ILogger, IDisposable
{
    private readonly string _category;
    private readonly FileLoggerOptions _options;
    private readonly Action<string> _write;

    public FileLogger(
        string category,
        FileLoggerOptions options,
        Action<string> write)
    {
        _category = category;
        _options = options;
        _write = write;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel)
        => logLevel >= _options.MinimumLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var entry = new
        {
            Timestamp = TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow,
                KermariaTimeZone.TimeZone).ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
            LogLevel = logLevel.ToString(),
            Category = _category,
            Message = formatter(state, exception),
            Exception = exception?.ToString()
        };

        try
        {
            _write(JsonSerializer.Serialize(entry));
        }
        catch
        {
            // best-effort — never crash the app on log failure
        }
    }

    public void Dispose() { }
}
