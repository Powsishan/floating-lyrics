using FloatingLyrics.Core.Models;
using FloatingLyrics.Media.Adapters;
using Microsoft.Extensions.Logging;
using Windows.Media.Control;

namespace FloatingLyrics.Media.WindowsSessions;

/// <summary>
/// Read-only Windows media-session adapter. No player-specific APIs or playback control.
/// Events are delivered on a worker thread; callers that render UI must dispatch them.
/// </summary>
public sealed class WindowsMediaSessionService(ILogger<WindowsMediaSessionService> logger) : IPlayerAdapter
{
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly Dictionary<GlobalSystemMediaTransportControlsSession, SessionEntry> _entries = [];
    private readonly object _startLock = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private CancellationTokenSource? _lifetime;
    private Task? _worker;
    private MediaSessionInfo[] _sessions = [];
    private PlaybackSnapshot? _currentSnapshot;
    private string? _activeId;
    private string? _preferredSourceId;
    private string? _selectedSessionId;
    private int _nextId;
    private bool _disposed;
    private string? _lastError;

    public event EventHandler<PlaybackSnapshot>? SnapshotChanged;
    public event EventHandler? SessionsChanged;
    public event EventHandler<string>? Error;

    public IReadOnlyList<MediaSessionInfo> Sessions => Volatile.Read(ref _sessions);
    public PlaybackSnapshot? CurrentSnapshot => Volatile.Read(ref _currentSnapshot);
    public string? PreferredSourceId
    {
        get => Volatile.Read(ref _preferredSourceId);
        set { Volatile.Write(ref _preferredSourceId, value); Wake(); }
    }
    public string? SelectedSessionId
    {
        get => Volatile.Read(ref _selectedSessionId);
        set { Volatile.Write(ref _selectedSessionId, value); Wake(); }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        CancellationToken lifetimeToken;
        lock (_startLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_lifetime is not null) throw new InvalidOperationException("The media service has already started.");
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lifetimeToken = _lifetime.Token;
        }

        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(lifetimeToken).ConfigureAwait(false);
        lock (_startLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            lifetimeToken.ThrowIfCancellationRequested();
            _manager = manager;
            _manager.SessionsChanged += OnSessionsChanged;
            _manager.CurrentSessionChanged += OnCurrentSessionChanged;
            _worker = Task.Run(() => RunAsync(lifetimeToken), CancellationToken.None);
            Wake();
        }
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args) => Wake();
    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) => Wake();
    private void Wake()
    {
        if (_disposed) return;
        try { _wake.Release(); }
        catch (SemaphoreFullException) { }
        catch (ObjectDisposedException) { }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _wake.WaitAsync(CurrentSnapshot?.State == PlaybackState.Playing
                    ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                try
                {
                    await RefreshAsync(cancellationToken).ConfigureAwait(false);
                    _lastError = null;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                catch (Exception exception)
                {
                    // Do not include track metadata, source identifiers, or listening history in logs.
                    logger.LogWarning("Media refresh failed ({ExceptionType}, HRESULT {HResult}).", exception.GetType().Name, exception.HResult);
                    if (_lastError != exception.GetType().Name)
                    {
                        _lastError = exception.GetType().Name;
                        Error?.Invoke(this, "Windows media information is temporarily unavailable. Retrying automatically.");
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var available = _manager!.GetSessions().ToArray();
        foreach (var removed in _entries.Keys.Where(s => !available.Contains(s)).ToArray())
        {
            _entries[removed].Unsubscribe();
            _entries.Remove(removed);
        }
        foreach (var session in available)
        {
            if (!_entries.ContainsKey(session))
            {
                var entry = new SessionEntry(session, $"session-{++_nextId}", Wake);
                _entries.Add(session, entry);
            }
        }

        foreach (var entry in _entries.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                entry.PlaybackInfo = entry.Session.GetPlaybackInfo();
                entry.State = MapState(entry.PlaybackInfo.PlaybackStatus);
                entry.Timeline = entry.Session.GetTimelineProperties();
                if (Interlocked.Exchange(ref entry.MetadataDirty, 0) == 1)
                {
                    try
                    {
                        var metadata = await entry.Session.TryGetMediaPropertiesAsync().AsTask(cancellationToken).ConfigureAwait(false);
                        entry.Title = metadata.Title?.Trim() ?? "";
                        entry.Artist = metadata.Artist?.Trim() ?? "";
                        entry.Album = metadata.AlbumTitle?.Trim() ?? "";
                    }
                    catch
                    {
                        Volatile.Write(ref entry.MetadataDirty, 1);
                        throw;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                entry.State = PlaybackState.Stopped;
                entry.Timeline = null;
                entry.Title = "";
                Volatile.Write(ref entry.MetadataDirty, 1);
                logger.LogDebug("A media session could not be read ({ExceptionType}).", exception.GetType().Name);
            }
        }

        var system = _manager.GetCurrentSession();
        var systemId = system is not null && _entries.TryGetValue(system, out var systemEntry) ? systemEntry.Id : null;
        var choices = _entries.Values.Select(e => new MediaSessionInfo(e.Id, e.SourceId, e.SourceId, e.State, false)).ToArray();
        _activeId = MediaSessionSelector.Select(choices, SelectedSessionId, PreferredSourceId, _activeId, systemId);
        var updated = choices.Select(s => s with { IsSelected = s.Id == _activeId }).ToArray();
        if (!_sessions.SequenceEqual(updated))
        {
            Volatile.Write(ref _sessions, updated);
            SessionsChanged?.Invoke(this, EventArgs.Empty);
        }

        var active = _entries.Values.FirstOrDefault(e => e.Id == _activeId);
        var snapshot = active is null
            ? new PlaybackSnapshot("", null, PlaybackState.Stopped, TimeSpan.Zero, DateTimeOffset.UtcNow, false)
            : BuildSnapshot(active);
        Volatile.Write(ref _currentSnapshot, snapshot);
        SnapshotChanged?.Invoke(this, snapshot);
    }

    private static PlaybackSnapshot BuildSnapshot(SessionEntry entry)
    {
        var now = DateTimeOffset.UtcNow;
        var timeline = entry.Timeline;
        var duration = timeline is not null && timeline.EndTime > timeline.StartTime
            ? timeline.EndTime - timeline.StartTime : (TimeSpan?)null;
        var track = string.IsNullOrWhiteSpace(entry.Title) ? null : new TrackInfo(entry.Title, entry.Artist, entry.Album, duration);
        var hasTimeline = timeline is not null && timeline.LastUpdatedTime != default
            && (duration > TimeSpan.Zero || timeline.Position > TimeSpan.Zero);
        if (hasTimeline && entry.State == PlaybackState.Playing)
        {
            var age = now - timeline!.LastUpdatedTime;
            if (age < TimeSpan.FromSeconds(-2) || age >= TimeSpan.FromDays(1)) hasTimeline = false;
        }
        var position = timeline is null ? TimeSpan.Zero : timeline.Position - timeline.StartTime;
        var rate = entry.PlaybackInfo?.PlaybackRate ?? 1;
        if (!double.IsFinite(rate) || rate <= 0 || rate > 8) rate = 1;
        // A stale timeline must continue monotonically, freeze on pause, and resume without including time spent paused.
        position = entry.PositionEstimator.Observe(track?.Key, position, timeline?.LastUpdatedTime ?? default,
            entry.State, rate, duration);
        return new PlaybackSnapshot(entry.SourceId, track, entry.State, position, now, hasTimeline,
            entry.PositionEstimator.EffectivePlaybackRate);
    }

    private static PlaybackState MapState(GlobalSystemMediaTransportControlsSessionPlaybackStatus state) => state switch
    {
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackState.Playing,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackState.Paused,
        _ => PlaybackState.Stopped
    };

    public async ValueTask DisposeAsync()
    {
        lock (_startLock)
        {
            if (_disposed) return;
            _disposed = true;
            _lifetime?.Cancel();
        }
        if (_manager is not null)
        {
            _manager.SessionsChanged -= OnSessionsChanged;
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
        }
        if (_worker is not null) await _worker.ConfigureAwait(false);
        foreach (var entry in _entries.Values) entry.Unsubscribe();
        _entries.Clear();
        _lifetime?.Dispose();
        _wake.Dispose();
    }

    private sealed class SessionEntry
    {
        private readonly Action _wake;
        public SessionEntry(GlobalSystemMediaTransportControlsSession session, string id, Action wake)
        {
            Session = session;
            Id = id;
            SourceId = session.SourceAppUserModelId;
            _wake = wake;
            session.MediaPropertiesChanged += MediaChanged;
            session.PlaybackInfoChanged += PlaybackChanged;
            session.TimelinePropertiesChanged += TimelineChanged;
        }
        public GlobalSystemMediaTransportControlsSession Session { get; }
        public string Id { get; }
        public string SourceId { get; }
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Album { get; set; } = "";
        public int MetadataDirty = 1;
        public TimelinePositionEstimator PositionEstimator { get; } = new();
        public PlaybackState State { get; set; }
        public GlobalSystemMediaTransportControlsSessionTimelineProperties? Timeline { get; set; }
        public GlobalSystemMediaTransportControlsSessionPlaybackInfo? PlaybackInfo { get; set; }
        private void MediaChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            Volatile.Write(ref MetadataDirty, 1);
            _wake();
        }
        private void PlaybackChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => _wake();
        private void TimelineChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args) => _wake();
        public void Unsubscribe()
        {
            Session.MediaPropertiesChanged -= MediaChanged;
            Session.PlaybackInfoChanged -= PlaybackChanged;
            Session.TimelinePropertiesChanged -= TimelineChanged;
        }
    }
}
