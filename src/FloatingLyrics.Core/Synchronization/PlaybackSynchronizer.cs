using System.Diagnostics;
using FloatingLyrics.Core.Interfaces;
using FloatingLyrics.Core.Models;

namespace FloatingLyrics.Core.Synchronization;

/// <summary>
/// Thread-safe playback estimation. No network or rendering dependencies. Identical player samples do
/// not reset the monotonic clock; changed positions reconcile drift and backward positions seek immediately.
/// </summary>
public sealed class PlaybackSynchronizer(IMonotonicClock? clock = null)
{
    private readonly IMonotonicClock _clock = clock ?? new StopwatchClock();
    private readonly object _gate = new();
    private PlaybackSnapshot? _snapshot;
    private TimeSpan _anchorPosition;
    private TimeSpan _anchorTime;
    private double _rate = 1;
    private bool _advancing;

    public TimeSpan Position
    {
        get { lock (_gate) return GetPosition(); }
    }

    public bool HasTimeline
    {
        get { lock (_gate) return _snapshot?.HasTimeline == true; }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _snapshot = null;
            _anchorPosition = TimeSpan.Zero;
            _anchorTime = _clock.Elapsed;
            _rate = 1;
            _advancing = false;
        }
    }

    public void Observe(PlaybackSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            var trackChanged = _snapshot is null || _snapshot.SourceId != snapshot.SourceId ||
                _snapshot.Track?.Key != snapshot.Track?.Key;
            if (!trackChanged && snapshot.ObservedAt < _snapshot!.ObservedAt)
                return;

            var now = _clock.Elapsed;
            var predicted = trackChanged ? TimeSpan.Zero : GetPosition();
            var positionChanged = trackChanged || !_snapshot!.HasTimeline || snapshot.Position != _snapshot.Position ||
                snapshot.ObservedAt > _snapshot.ObservedAt;

            if (!snapshot.HasTimeline || snapshot.Track is null)
            {
                _anchorPosition = trackChanged ? TimeSpan.Zero : predicted;
            }
            else if (positionChanged || snapshot.State == PlaybackState.Stopped)
            {
                // A real change is authoritative. This includes seeks smaller than one lyric line.
                _anchorPosition = Clamp(snapshot.Position, snapshot.Track.Duration);
            }
            else
            {
                // Some players publish the same timeline timestamp/position for many seconds.
                // Preserve elapsed time on both polls and state transitions instead of freezing playback.
                _anchorPosition = predicted;
            }

            _anchorTime = now;
            _rate = double.IsFinite(snapshot.PlaybackRate) && snapshot.PlaybackRate > 0
                ? Math.Clamp(snapshot.PlaybackRate, 0.01, 16) : 1;
            _advancing = snapshot.State == PlaybackState.Playing && snapshot.HasTimeline && snapshot.Track is not null;
            _snapshot = snapshot;
        }
    }

    /// <summary>Positive user and document offsets show lyrics earlier. Only genuine word timestamps are returned.</summary>
    public LyricFrame GetFrame(LyricsDocument document, TimeSpan userOffset)
    {
        ArgumentNullException.ThrowIfNull(document);
        TimeSpan position;
        lock (_gate)
        {
            if (_snapshot?.HasTimeline != true || _snapshot.Track is null || document.IsInstrumental)
                return LyricFrame.Empty;
            position = GetPosition() + userOffset + document.Offset;
        }

        var lines = document.Lines;
        var current = FindLastAtOrBefore(lines.Count, index => lines[index].Start, position);
        if (current < 0)
            return lines.Count == 0 ? LyricFrame.Empty : new(-1, null, null, lines[0], -1);

        var line = lines[current];
        var word = line.Words is { Count: > 0 } words
            ? FindLastAtOrBefore(words.Count, index => words[index].Start, position) : -1;
        return new(current, current > 0 ? lines[current - 1] : null, line,
            current + 1 < lines.Count ? lines[current + 1] : null, word);
    }

    private TimeSpan GetPosition()
    {
        var elapsed = _advancing ? _clock.Elapsed - _anchorTime : TimeSpan.Zero;
        var ticks = Math.Max(0, elapsed.Ticks) * _rate;
        var available = TimeSpan.MaxValue.Ticks - Math.Max(0, _anchorPosition.Ticks);
        var position = _anchorPosition + TimeSpan.FromTicks((long)Math.Min(ticks, available));
        return Clamp(position, _snapshot?.Track?.Duration);
    }

    private static TimeSpan Clamp(TimeSpan position, TimeSpan? duration)
    {
        if (position < TimeSpan.Zero) return TimeSpan.Zero;
        return duration is { } limit && limit > TimeSpan.Zero && position > limit ? limit : position;
    }

    private static int FindLastAtOrBefore(int count, Func<int, TimeSpan> timestamp, TimeSpan position)
    {
        var low = 0;
        var high = count - 1;
        var found = -1;
        while (low <= high)
        {
            var mid = low + (high - low) / 2;
            if (timestamp(mid) <= position) { found = mid; low = mid + 1; }
            else high = mid - 1;
        }
        return found;
    }

    private sealed class StopwatchClock : IMonotonicClock
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        public TimeSpan Elapsed => _stopwatch.Elapsed;
    }
}
