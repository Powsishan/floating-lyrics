using System.IO;
using System.Net.Http;
using System.Windows.Threading;
using FloatingLyrics.App.ViewModels;
using FloatingLyrics.App.Views;
using FloatingLyrics.Core.Interfaces;
using FloatingLyrics.Core.Models;
using FloatingLyrics.Core.Synchronization;
using FloatingLyrics.Infrastructure.Settings;
using FloatingLyrics.Lyrics.Providers;
using FloatingLyrics.Media.WindowsSessions;
using FloatingLyrics.Platform.Overlay;
using FloatingLyrics.Platform.Tray;
using Microsoft.Extensions.Logging;

namespace FloatingLyrics.App;

public sealed class ApplicationController : IAsyncDisposable
{
    private readonly WindowsMediaSessionService _media;
    private readonly JsonSettingsStore _store;
    private readonly HttpClient _http;
    private readonly ILogger<ApplicationController> _logger;
    private readonly PlaybackSynchronizer _synchronizer = new();
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _saveTimer;
    private readonly OverlayWindow _overlay;
    private readonly SettingsWindow _window;
    private readonly TrayIcon _tray;
    private readonly Dispatcher _dispatcher;
    private CancellationTokenSource? _lookup;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HashSet<Task> _searchTasks = [];
    private Task? _disposeTask;
    private (bool Enabled, bool Locked, string Status)? _trayState;
    private LyricsDocument? _lyrics;
    private PlaybackSnapshot? _snapshot;
    private string? _trackKey;
    private string _contact;
    private ILyricsProvider? _provider;
    private bool _updatingSessions;
    private bool _exiting;
    private bool _disposed;
    public SettingsViewModel ViewModel { get; }

    public ApplicationController(WindowsMediaSessionService media, JsonSettingsStore store, HttpClient http, ILogger<ApplicationController> logger, AppSettings settings)
    {
        _media = media; _store = store; _http = http; _logger = logger;
        _dispatcher = System.Windows.Application.Current.Dispatcher;
        // Initialize commands before any view binds; later replacement without notification can leave an inert button.
        ViewModel = new(settings)
        {
            RetryCommand = new(() => { BeginLookup(); Render(); }),
            CenterCommand = new(CenterOverlay),
            ExitCommand = new(() => _ = ExitAsync())
        };
        _contact = settings.LrcLibClientUrl;
        _media.PreferredSourceId = settings.PreferredSourceId;
        _overlay = new(ViewModel);
        _window = new() { DataContext = ViewModel };
        _tray = new(() => _window.Open(), () => ViewModel.OverlayEnabled = !ViewModel.OverlayEnabled, () => ViewModel.Locked = !ViewModel.Locked, () => _ = ExitAsync());
        _timer = new(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => Render(), _dispatcher);
        _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick += SaveTick;
        ViewModel.SettingsChanged += SettingsChanged;
        ViewModel.SessionSelectionChanged += SelectSession;
        _media.SnapshotChanged += SnapshotChanged;
        _media.SessionsChanged += SessionsChanged;
        _media.Error += MediaError;
    }

    public async Task StartAsync(bool smokeTest)
    {
        _overlay.Show();
        if (!ViewModel.StartInTray || smokeTest) _window.Open();
        Render();
        try { await _media.StartAsync(_lifetime.Token); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ViewModel.Status = "Windows media sessions unavailable";
            ViewModel.Detail = "Check the local log and run the diagnostics probe. Other settings remain available.";
            _logger.LogWarning("Media startup failed ({ErrorType}).", ex.GetType().Name);
        }
        if (_disposed || _exiting) return;
        UpdateSessions();
        if (smokeTest)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(5), _lifetime.Token); }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
            await ExitAsync();
        }
    }

    private void SnapshotChanged(object? sender, PlaybackSnapshot snapshot) => _dispatcher.BeginInvoke(() =>
    {
        if (_disposed || _exiting) return;
        _snapshot = snapshot;
        _synchronizer.Observe(snapshot);
        ViewModel.Track = snapshot.Track is { } track ? $"{track.Title}  ·  {track.Artist}" : "Play a song in a compatible music player";
        ViewModel.Source = string.IsNullOrWhiteSpace(snapshot.SourceId) ? "Windows media sessions" : snapshot.SourceId;
        if (_trackKey != snapshot.Track?.Key)
        {
            _trackKey = snapshot.Track?.Key;
            _lyrics = null;
            ViewModel.CurrentLyric = "";
            BeginLookup();
        }
        if (snapshot.Track is null) ViewModel.Status = "No media detected";
        Render();
    });
    private void SessionsChanged(object? sender, EventArgs args) => _dispatcher.BeginInvoke(UpdateSessions);
    private void MediaError(object? sender, string message) => _dispatcher.BeginInvoke(() =>
    {
        if (_disposed) return;
        _lookup?.Cancel();
        _lookup = null;
        _snapshot = null;
        _trackKey = null;
        _lyrics = null;
        _synchronizer.Reset();
        ViewModel.Status = "Media session temporarily unavailable";
        ViewModel.Detail = "Waiting for Windows to publish a usable media session.";
        Render();
    });

    private void UpdateSessions()
    {
        if (_disposed) return;
        var selectedId = ViewModel.SelectedSessionIndex > 0 && ViewModel.SelectedSessionIndex <= ViewModel.Sessions.Count ? ViewModel.Sessions[ViewModel.SelectedSessionIndex - 1].Id : null;
        _updatingSessions = true;
        ViewModel.Sessions.Clear(); ViewModel.SessionNames.Clear(); ViewModel.SessionNames.Add("Automatic · prefer playing");
        foreach (var source in _media.Sessions) { ViewModel.Sessions.Add(source); ViewModel.SessionNames.Add($"{source.DisplayName} · {source.State}"); }
        ViewModel.SelectedSessionIndex = selectedId is null ? 0 : Math.Max(0, ViewModel.Sessions.ToList().FindIndex(s => s.Id == selectedId) + 1);
        _updatingSessions = false;
        if (_media.Sessions.Count == 0 && _snapshot?.Track is null) ViewModel.Status = "No media detected";
    }

    private void SelectSession(object? sender, EventArgs e)
    {
        if (_updatingSessions) return;
        var index = ViewModel.SelectedSessionIndex - 1;
        var source = index >= 0 && index < ViewModel.Sessions.Count ? ViewModel.Sessions[index] : null;
        _media.SelectedSessionId = source?.Id;
        if (source is not null) { _media.PreferredSourceId = source.SourceId; ViewModel.PreferSource(source.SourceId); }
    }

    private void CenterOverlay()
    {
        OverlayInterop.Place(_overlay, null, null, bottomCenter: true);
        var (x, y) = OverlayInterop.GetPosition(_overlay);
        ViewModel.SavePosition(x, y);
    }

    private void BeginLookup()
    {
        if (_disposed || _exiting) return;
        _lookup?.Cancel();
        _lookup = null;
        _lyrics = null;
        ViewModel.CurrentLyric = "";
        if (_snapshot?.Track is not { } track) { ViewModel.Status = "No media detected"; ViewModel.Detail = ""; return; }
        if (!IsClientUrlValid(ViewModel.LrcLibClientUrl))
        {
            ViewModel.Status = "Set up LRCLIB identification";
            ViewModel.Detail = "LrcLibClientUrl is unset or invalid. Enter this project's public GitHub repository URL to enable online lookup.";
            return;
        }
        _provider ??= new LrcLibProvider(_http, $"FloatingLyrics/0.1.0 ({ViewModel.LrcLibClientUrl})");
        ViewModel.Status = "Searching for lyrics…";
        ViewModel.Detail = "Matching title, artist, album and recording duration with LRCLIB.";
        _lookup = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _searchTasks.RemoveWhere(task => task.IsCompletedSuccessfully || task.IsCanceled);
        _searchTasks.Add(RunLookupAsync(track, _provider, _lookup));
    }

    private async Task RunLookupAsync(TrackInfo track, ILyricsProvider provider, CancellationTokenSource lookup)
    {
        try { await SearchAsync(track, provider, lookup.Token); }
        finally
        {
            if (ReferenceEquals(_lookup, lookup)) _lookup = null;
            lookup.Dispose();
        }
    }

    private async Task SearchAsync(TrackInfo track, ILyricsProvider provider, CancellationToken cancellationToken)
    {
        try
        {
            var candidates = await provider.SearchAsync(track, cancellationToken);
            if (_disposed || cancellationToken.IsCancellationRequested || _trackKey != track.Key) return;
            _lyrics = candidates.OrderByDescending(l => l.Lines.Count > 0).ThenByDescending(l => l.Confidence).FirstOrDefault();
            ViewModel.Status = _lyrics is null ? "No confident lyric match" : _lyrics.IsInstrumental ? "Instrumental track" : _lyrics.Lines.Count == 0 ? "Only unsynchronized lyrics available" : "Lyrics available";
            ViewModel.Detail = _lyrics is { Lines.Count: > 0 } ? $"{_lyrics.Provider} · {_lyrics.Lines.Count} timed lines · match confidence {_lyrics.Confidence:P0}" : "The floating display stays clear when synchronized lyrics are unavailable.";
            Render();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException)
        {
            if (_disposed || cancellationToken.IsCancellationRequested || _trackKey != track.Key) return;
            ViewModel.Status = "Lyrics provider unavailable";
            ViewModel.Detail = "Check your connection and retry. Rate-limited requests wait before a further attempt.";
            _logger.LogWarning("Lyrics lookup failed ({ErrorType}).", ex.GetType().Name);
        }
    }

    private void Render()
    {
        if (_disposed) return;
        var active = _snapshot is { Track: not null, State: PlaybackState.Playing } && _lyrics is { Lines.Count: > 0 } && _synchronizer.HasTimeline;
        if (active)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            if (!_timer.IsEnabled) _timer.Start();
        }
        else _timer.Stop();
        var text = _snapshot is { Track: not null, State: not PlaybackState.Stopped } && _lyrics is { Lines.Count: > 0 } && _synchronizer.HasTimeline
            ? _synchronizer.GetFrame(_lyrics, TimeSpan.FromMilliseconds(ViewModel.TimingOffsetMs)).Current?.Text ?? "" : "";
        ViewModel.CurrentLyric = text;
        var opacity = ViewModel.OverlayEnabled && (!ViewModel.Locked || text.Length > 0) ? 1 : 0;
        if (_overlay.Opacity != opacity) _overlay.Opacity = opacity;
        _overlay.AnimateLine();
        var status = _snapshot?.Track is null ? ViewModel.Status : _snapshot.HasTimeline ? (_snapshot.State == PlaybackState.Paused && _lyrics is { Lines.Count: > 0 } ? "Paused · lyrics available" : ViewModel.Status) : "Player has no usable timeline";
        if (_snapshot?.Track is not null && !_snapshot.HasTimeline) ViewModel.Detail = "Timed lyrics are hidden because this player is not publishing a usable playback position.";
        var trayState = (Enabled: ViewModel.OverlayEnabled, Locked: ViewModel.Locked, Status: status);
        if (_trayState != trayState)
        {
            _tray.Update(trayState.Enabled, trayState.Locked, trayState.Status);
            _trayState = trayState;
        }
    }

    private void SettingsChanged(object? sender, EventArgs e)
    {
        _overlay.ApplySettings();
        if (_contact != ViewModel.LrcLibClientUrl) { _contact = ViewModel.LrcLibClientUrl; _provider = null; BeginLookup(); }
        _saveTimer.Stop(); _saveTimer.Start();
        Render();
    }
    private async void SaveTick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        await SaveAsync();
    }
    private async Task SaveAsync()
    {
        try { await _store.SaveAsync(ViewModel.Settings); ViewModel.SaveStatus = "Settings saved locally"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ViewModel.SaveStatus = "Settings could not be saved · check folder permissions";
            _logger.LogWarning("Settings save failed ({ErrorType}).", ex.GetType().Name);
        }
    }
    public static bool IsClientUrlValid(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.UserInfo.Length == 0 && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length >= 2 && uri.Query.Length == 0 && uri.Fragment.Length == 0;

    private async Task ExitAsync()
    {
        if (_exiting) return;
        _exiting = true;
        await SaveAsync();
        await ((App)System.Windows.Application.Current).ShutdownAsync();
    }
    public ValueTask DisposeAsync() => new(_disposeTask ??= DisposeCoreAsync());

    private async Task DisposeCoreAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop(); _saveTimer.Stop();
        _lifetime.Cancel(); _lookup?.Cancel();
        _media.SnapshotChanged -= SnapshotChanged; _media.SessionsChanged -= SessionsChanged; _media.Error -= MediaError;
        ViewModel.SettingsChanged -= SettingsChanged; ViewModel.SessionSelectionChanged -= SelectSession;
        try { await _media.DisposeAsync(); }
        finally
        {
            try { await Task.WhenAll(_searchTasks); }
            finally
            {
                _tray.Dispose(); _overlay.Close(); _window.AllowClose = true; _window.Close();
                _lookup = null;
                _lifetime.Dispose();
            }
        }
    }
}
