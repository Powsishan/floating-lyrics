using FloatingLyrics.Core.Interfaces;
using FloatingLyrics.Core.Models;
using FloatingLyrics.Core.Synchronization;
using FloatingLyrics.Lyrics.Parsers;
using Xunit;

namespace FloatingLyrics.IntegrationTests;

public sealed class LyricsPlaybackPipelineTests
{
    private static readonly TrackInfo Track = new("Synthetic Fixture", "Test Author", "Test Album", TimeSpan.FromSeconds(60));
    private static readonly DateTimeOffset SampleTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ParsedLyricsFollowPlayPauseResumeAndBothSeekDirections()
    {
        var lyrics = LrcParser.Parse("[00:01]opening fixture\n[00:05]middle fixture\n[00:10]\n[00:15]closing fixture");
        var clock = new FakeClock();
        var sync = new PlaybackSynchronizer(clock);
        sync.Observe(Sample(0, PlaybackState.Playing));
        clock.Elapsed = TimeSpan.FromSeconds(6);
        Assert.Equal("middle fixture", sync.GetFrame(lyrics, TimeSpan.Zero).Current?.Text);

        sync.Observe(Sample(6, PlaybackState.Paused, 1));
        clock.Elapsed += TimeSpan.FromSeconds(20);
        Assert.Equal("middle fixture", sync.GetFrame(lyrics, TimeSpan.Zero).Current?.Text);
        sync.Observe(Sample(6, PlaybackState.Playing, 2));
        clock.Elapsed += TimeSpan.FromSeconds(5);
        Assert.Equal("", sync.GetFrame(lyrics, TimeSpan.Zero).Current?.Text);

        sync.Observe(Sample(16, PlaybackState.Playing, 3));
        Assert.Equal("closing fixture", sync.GetFrame(lyrics, TimeSpan.Zero).Current?.Text);
        sync.Observe(Sample(2, PlaybackState.Playing, 4));
        Assert.Equal("opening fixture", sync.GetFrame(lyrics, TimeSpan.Zero).Current?.Text);
    }

    [Fact]
    public void DocumentAndUserOffsetsCombineWithoutInventingWordTiming()
    {
        var lyrics = LrcParser.Parse("[offset:250]\n[00:01]fixture line");
        var sync = new PlaybackSynchronizer(new FakeClock());
        sync.Observe(Sample(.5, PlaybackState.Paused));
        var frame = sync.GetFrame(lyrics, TimeSpan.FromMilliseconds(250));
        Assert.Equal("fixture line", frame.Current?.Text);
        Assert.Equal(-1, frame.WordIndex);
    }

    [Fact]
    public void TrackSwitchAndMissingTimelineNeverCarryPreviousTrackPosition()
    {
        var lyrics = LrcParser.Parse("[00:01]new track fixture");
        var sync = new PlaybackSynchronizer(new FakeClock());
        sync.Observe(Sample(45, PlaybackState.Playing));
        sync.Observe(Sample(0, PlaybackState.Playing, 1) with { Track = Track with { Title = "Second Fixture" } });
        Assert.Null(sync.GetFrame(lyrics, TimeSpan.Zero).Current);
        sync.Observe(Sample(30, PlaybackState.Playing, 2) with { HasTimeline = false });
        Assert.Null(sync.GetFrame(lyrics, TimeSpan.Zero).Current);
    }

    private static PlaybackSnapshot Sample(double seconds, PlaybackState state, int revision = 0)
        => new("synthetic-test-player", Track, state, TimeSpan.FromSeconds(seconds), SampleTime.AddSeconds(revision));
    private sealed class FakeClock : IMonotonicClock { public TimeSpan Elapsed { get; set; } }
}
