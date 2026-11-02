using FloatingLyrics.Media.WindowsSessions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Globalization;

var seconds = 5;
var measurePerformance = args.Contains("--performance", StringComparer.Ordinal);
var includeMetadata = args.Contains("--include-metadata", StringComparer.Ordinal);
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--duration" && i + 1 < args.Length && int.TryParse(args[++i], out var requested))
        seconds = Math.Clamp(requested, 1, 300);
}
Console.WriteLine("Floating Lyrics — read-only Windows media-session probe");
Console.WriteLine("Track metadata is redacted unless --include-metadata is passed. No playback controls are sent.");
await using var service = new WindowsMediaSessionService(NullLogger<WindowsMediaSessionService>.Instance);
service.SessionsChanged += (_, _) => Console.WriteLine($"Sessions: {service.Sessions.Count}; playing: {service.Sessions.Count(s => s.State == FloatingLyrics.Core.Models.PlaybackState.Playing)}");
service.SnapshotChanged += (_, snapshot) =>
{
    Console.WriteLine($"State={snapshot.State}; trackAvailable={snapshot.Track is not null}; timeline={snapshot.HasTimeline}; position={snapshot.Position.TotalSeconds:F1}s; duration={snapshot.Track?.Duration?.TotalSeconds:F1}s");
    if (includeMetadata && snapshot.Track is { } track)
        Console.WriteLine($"Source={snapshot.SourceId}; title={track.Title}; artist={track.Artist}; album={track.Album}");
};
service.Error += (_, error) => Console.Error.WriteLine(error);
try
{
    await service.StartAsync();
    using var process = Process.GetCurrentProcess();
    var startCpu = process.TotalProcessorTime;
    var elapsed = Stopwatch.StartNew();
    await Task.Delay(TimeSpan.FromSeconds(seconds));
    if (measurePerformance)
    {
        process.Refresh();
        var cpuSeconds = (process.TotalProcessorTime - startCpu).TotalSeconds;
        var wallSeconds = elapsed.Elapsed.TotalSeconds;
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Performance ({wallSeconds:F2}s interval): CPU {cpuSeconds:F3}s; one-core equivalent {100 * cpuSeconds / wallSeconds:F2}%; working set {process.WorkingSet64 / 1048576d:F2} MiB; managed heap {GC.GetTotalMemory(false) / 1048576d:F2} MiB."));
        Console.WriteLine("This measures the diagnostic process and media adapter, not WPF rendering or a complete workload benchmark.");
    }
    Console.WriteLine($"Completed {seconds}s observation. Final session count: {service.Sessions.Count}.");
    Console.WriteLine("A successful API probe does not certify any player: verify pause, resume, seek, and track changes with that player.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Windows media access failed: {exception.GetType().Name} (HRESULT {exception.HResult:X8}).");
    return 1;
}
