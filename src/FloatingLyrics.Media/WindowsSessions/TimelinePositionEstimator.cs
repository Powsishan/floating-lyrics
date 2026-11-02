using FloatingLyrics.Core.Models;

namespace FloatingLyrics.Media.WindowsSessions;

/// <summary>Converts WinRT positions as-of LastUpdatedTime into positions as-of-now, then uses a monotonic clock.</summary>
public sealed class TimelinePositionEstimator(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private bool _initialized;
    private string? _trackKey;
    private DateTimeOffset _lastUpdated;
    private TimeSpan _rawPosition;
    private TimeSpan _anchorPosition;
    private long _anchorTimestamp;
    private PlaybackState _state;
    private double _rate = 1;
    public double EffectivePlaybackRate => _rate;

    public TimeSpan Observe(string? trackKey, TimeSpan rawPosition, DateTimeOffset lastUpdated,
        PlaybackState state, double rate, TimeSpan? duration)
    {
        var timestamp = _time.GetTimestamp();
        var sameTrack = _initialized && _trackKey == trackKey;
        var changed = !sameTrack || _lastUpdated != lastUpdated || _rawPosition != rawPosition;
        var predicted = _anchorPosition;
        if (_initialized && _state == PlaybackState.Playing)
            predicted += _time.GetElapsedTime(_anchorTimestamp, timestamp) * _rate;
        if (changed)
        {
            var authoritative = rawPosition;
            var age = _time.GetUtcNow() - lastUpdated;
            if (state == PlaybackState.Playing && lastUpdated != default && age >= TimeSpan.Zero && age < TimeSpan.FromDays(1))
                authoritative += age * rate;
            var error = authoritative - predicted;
            if (sameTrack && _state == PlaybackState.Playing && state == PlaybackState.Playing && Math.Abs(error.TotalSeconds) < 2)
            {
                // Coarse player timestamps commonly alternate between whole-second values.
                // Slew small drift through speed instead of rewinding visible lyrics on each event.
                rate *= 1 + Math.Clamp(error.TotalSeconds * 0.1, -0.15, 0.15);
            }
            else
            {
                // Large discontinuities, track changes, and stopped/paused observations are authoritative.
                predicted = authoritative;
            }
        }
        if (predicted < TimeSpan.Zero) predicted = TimeSpan.Zero;
        if (duration is { } limit && limit > TimeSpan.Zero && predicted > limit) predicted = limit;
        _initialized = true;
        _trackKey = trackKey;
        _lastUpdated = lastUpdated;
        _rawPosition = rawPosition;
        _anchorPosition = predicted;
        _anchorTimestamp = timestamp;
        _state = state;
        _rate = rate;
        return predicted;
    }
}
