using FloatingLyrics.Core.Models;
using FloatingLyrics.Media.WindowsSessions;

namespace FloatingLyrics.Media.Adapters;

/// <summary>Read-only player boundary for Windows sessions and future opt-in, authorized integrations.</summary>
public interface IPlayerAdapter : IAsyncDisposable
{
    event EventHandler<PlaybackSnapshot>? SnapshotChanged;
    event EventHandler? SessionsChanged;
    event EventHandler<string>? Error;
    IReadOnlyList<MediaSessionInfo> Sessions { get; }
    PlaybackSnapshot? CurrentSnapshot { get; }
    string? PreferredSourceId { get; set; }
    string? SelectedSessionId { get; set; }
    Task StartAsync(CancellationToken cancellationToken = default);
}
