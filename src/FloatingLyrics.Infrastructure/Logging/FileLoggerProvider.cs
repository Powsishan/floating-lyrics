using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace FloatingLyrics.Infrastructure.Logging;

// Bounded queue keeps disk I/O off media callbacks and the dispatcher. Never log lyric content or track metadata.
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly Channel<string> _queue = Channel.CreateBounded<string>(new BoundedChannelOptions(512) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly Task _writer;

    public FileLoggerProvider(string directory)
    {
        _writer = Task.Run(async () =>
        {
            try
            {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "application.log");
                if (File.Exists(path) && new FileInfo(path).Length > 2_000_000) File.Move(path, path + ".1", true);
                await using var stream = new StreamWriter(path, append: true) { AutoFlush = true };
                await foreach (var entry in _queue.Reader.ReadAllAsync())
                {
                    // Bound long-running sessions as well as startup logs.
                    if (stream.BaseStream.Length > 2_000_000) { stream.BaseStream.SetLength(0); stream.BaseStream.Position = 0; }
                    await stream.WriteLineAsync(entry);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { System.Diagnostics.Debug.WriteLine(ex.GetType().Name); }
        });
    }
    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _queue.Writer);
    public void Dispose() { _queue.Writer.TryComplete(); _writer.GetAwaiter().GetResult(); }

    private sealed class FileLogger(string category, ChannelWriter<string> queue) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(level)) queue.TryWrite($"{DateTimeOffset.UtcNow:O} {level} {category}: {formatter(state, null)}{(exception is null ? "" : " [" + exception.GetType().Name + "]")}");
        }
    }
}
