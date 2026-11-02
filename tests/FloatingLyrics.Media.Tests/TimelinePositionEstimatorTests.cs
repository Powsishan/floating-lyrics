using FloatingLyrics.Core.Models;
using FloatingLyrics.Media.WindowsSessions;
using Xunit;

namespace FloatingLyrics.Media.Tests;

public sealed class TimelinePositionEstimatorTests
{
    [Fact]
    public void FirstSampleCompensatesTimestampAgeThenUsesMonotonicTime()
    {
        var time = new TestTime();
        var estimate = new TimelinePositionEstimator(time);
        var update = time.GetUtcNow() - TimeSpan.FromSeconds(2);
        Assert.Equal(TimeSpan.FromSeconds(12), estimate.Observe("track", TimeSpan.FromSeconds(10), update, PlaybackState.Playing, 1, null));
        time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(TimeSpan.FromSeconds(15), estimate.Observe("track", TimeSpan.FromSeconds(10), update, PlaybackState.Playing, 1, null));
    }

    [Fact]
    public void RepeatedTimelineDoesNotJumpBackwardOnPauseOrAdvanceThroughPause()
    {
        var time = new TestTime();
        var estimate = new TimelinePositionEstimator(time);
        var update = time.GetUtcNow();
        estimate.Observe("track", TimeSpan.FromSeconds(10), update, PlaybackState.Playing, 1, null);
        time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(TimeSpan.FromSeconds(13), estimate.Observe("track", TimeSpan.FromSeconds(10), update, PlaybackState.Paused, 1, null));
        time.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(TimeSpan.FromSeconds(13), estimate.Observe("track", TimeSpan.FromSeconds(10), update, PlaybackState.Playing, 1, null));
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromSeconds(15), estimate.Observe("track", TimeSpan.FromSeconds(10), update, PlaybackState.Playing, 1, null));
    }

    [Fact]
    public void SeekAndTrackSwitchReplacePreviousAnchor()
    {
        var time = new TestTime();
        var estimate = new TimelinePositionEstimator(time);
        estimate.Observe("first", TimeSpan.FromSeconds(90), time.GetUtcNow(), PlaybackState.Playing, 1, null);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(20), estimate.Observe("first", TimeSpan.FromSeconds(20), time.GetUtcNow(), PlaybackState.Playing, 1, null));
        Assert.Equal(TimeSpan.Zero, estimate.Observe("second", TimeSpan.Zero, time.GetUtcNow(), PlaybackState.Playing, 1, null));
    }

    [Fact]
    public void QuantizedFreshTimelineDoesNotRewindContinuousPlayback()
    {
        var time = new TestTime();
        var estimate = new TimelinePositionEstimator(time);
        var previous = estimate.Observe("track", TimeSpan.Zero, time.GetUtcNow(), PlaybackState.Playing, 1, null);
        var observations = new[] { 0, 0, 1, 1, 2, 1, 2, 2, 2, 3, 3, 3 };
        foreach (var wholeSeconds in observations)
        {
            time.Advance(TimeSpan.FromMilliseconds(300));
            var position = estimate.Observe("track", TimeSpan.FromSeconds(wholeSeconds), time.GetUtcNow(), PlaybackState.Playing, 1, null);
            Assert.True(position >= previous, $"Timeline rewound from {previous} to {position}.");
            Assert.InRange(estimate.EffectivePlaybackRate, 0.85, 1.15);
            previous = position;
        }
    }

    [Fact]
    public void LargeBackwardSeekIsStillAppliedImmediately()
    {
        var time = new TestTime();
        var estimate = new TimelinePositionEstimator(time);
        estimate.Observe("track", TimeSpan.FromSeconds(10), time.GetUtcNow(), PlaybackState.Playing, 1, null);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(3), estimate.Observe("track", TimeSpan.FromSeconds(3), time.GetUtcNow(), PlaybackState.Playing, 1, null));
    }

    [Fact]
    public void ThirtySecondSeeksAndAuthoritativePausedPositionsApplyImmediately()
    {
        var time = new TestTime();
        var estimate = new TimelinePositionEstimator(time);
        estimate.Observe("track", TimeSpan.FromSeconds(50), time.GetUtcNow(), PlaybackState.Playing, 1, null);
        Assert.Equal(TimeSpan.FromSeconds(80), estimate.Observe("track", TimeSpan.FromSeconds(80), time.GetUtcNow(), PlaybackState.Playing, 1, null));
        Assert.Equal(TimeSpan.FromSeconds(50), estimate.Observe("track", TimeSpan.FromSeconds(50), time.GetUtcNow(), PlaybackState.Playing, 1, null));
        time.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(TimeSpan.FromSeconds(50), estimate.Observe("track", TimeSpan.FromSeconds(50), time.GetUtcNow(), PlaybackState.Paused, 1, null));
    }

    private sealed class TestTime : TimeProvider
    {
        private DateTimeOffset _utc = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public override DateTimeOffset GetUtcNow() => _utc;
        public void Advance(TimeSpan by) { _utc += by; _ticks += by.Ticks; }
    }
}
