using FloatingLyrics.Core.Interfaces;
using FloatingLyrics.Core.Models;
using FloatingLyrics.Core.Synchronization;
using Xunit;

namespace FloatingLyrics.Core.Tests;

public sealed class PlaybackSynchronizerTests
{
    private readonly FakeClock _clock = new();
    private readonly TrackInfo _track = new("Title", "Artist", "Album", TimeSpan.FromMinutes(3));
    private static readonly DateTimeOffset Epoch = DateTimeOffset.UnixEpoch;

    private PlaybackSnapshot Snapshot(double seconds = 0, PlaybackState state = PlaybackState.Playing,
        double observed = 0, bool hasTimeline = true, double rate = 1) =>
        new("player", _track, state, TimeSpan.FromSeconds(seconds), Epoch.AddSeconds(observed), hasTimeline, rate);

    [Fact]
    public void PlayingAdvancesFromMonotonicClockAndCapsAtDuration()
    {
        var engine = new PlaybackSynchronizer(_clock);
        engine.Observe(Snapshot(5));
        _clock.Advance(2);
        Assert.Equal(TimeSpan.FromSeconds(7), engine.Position);
        _clock.Advance(1000);
        Assert.Equal(_track.Duration, engine.Position);
    }

    [Fact]
    public void PauseFreezesAndResumeContinuesWithoutCountingPausedTime()
    {
        var engine = new PlaybackSynchronizer(_clock);
        engine.Observe(Snapshot(10));
        _clock.Advance(3);
        engine.Observe(Snapshot(10, PlaybackState.Paused));
        _clock.Advance(20);
        Assert.Equal(TimeSpan.FromSeconds(13), engine.Position);
        engine.Observe(Snapshot(10));
        _clock.Advance(2);
        Assert.Equal(TimeSpan.FromSeconds(15), engine.Position);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(80)]
    public void SeekImmediatelySelectsAuthoritativeLine(double seekTo)
    {
        var engine = new PlaybackSynchronizer(_clock);
        engine.Observe(Snapshot(30));
        _clock.Advance(2);
        engine.Observe(Snapshot(seekTo, observed: 1));
        var document = new LyricsDocument(new[] { Line(0), Line(20), Line(60) });
        Assert.Equal(TimeSpan.FromSeconds(seekTo), engine.Position);
        Assert.Equal(seekTo < 20 ? 0 : 2, engine.GetFrame(document, TimeSpan.Zero).CurrentIndex);
    }

    [Fact]
    public void RepeatedStaleSamplesDoNotFreezePlayback()
    {
        var engine = new PlaybackSynchronizer(_clock);
        engine.Observe(Snapshot(5));
        for (var second = 1; second <= 5; second++)
        {
            _clock.Advance(1);
            engine.Observe(Snapshot(5));
        }
        Assert.Equal(TimeSpan.FromSeconds(10), engine.Position);
    }

    [Fact]
    public void NewTimelineTimestampCanSeekToSamePreviouslyReportedPosition()
    {
        var engine = new PlaybackSynchronizer(_clock);
        engine.Observe(Snapshot(5));
        _clock.Advance(4);
        engine.Observe(Snapshot(5, observed: 1));
        Assert.Equal(TimeSpan.FromSeconds(5), engine.Position);
    }

    [Fact]
    public void NewSamplesCorrectDriftAndOlderSamplesAreIgnored()
    {
        var engine = new PlaybackSynchronizer(_clock);
        engine.Observe(Snapshot(10, observed: 1));
        _clock.Advance(5);
        engine.Observe(Snapshot(14, observed: 2));
        Assert.Equal(TimeSpan.FromSeconds(14), engine.Position);
        engine.Observe(Snapshot(9, observed: 1));
        Assert.Equal(TimeSpan.FromSeconds(14), engine.Position);
    }

    [Fact]
    public void RateChangePreservesProgressAndUsesNewRate()
    {
        var engine = new PlaybackSynchronizer(_clock);
        engine.Observe(Snapshot(10));
        _clock.Advance(2);
        engine.Observe(Snapshot(10, rate: 2));
        _clock.Advance(2);
        Assert.Equal(TimeSpan.FromSeconds(16), engine.Position);
    }

    [Fact]
    public void SwitchingTrackClearsOldProgressAndResetClearsTimeline()
    {
        var engine = new PlaybackSynchronizer(_clock);
        engine.Observe(Snapshot(60));
        engine.Observe(Snapshot() with { Track = new("Next", "Artist", "Album", TimeSpan.FromSeconds(90)) });
        Assert.Equal(TimeSpan.Zero, engine.Position);
        engine.Reset();
        _clock.Advance(10);
        Assert.False(engine.HasTimeline);
        Assert.Equal(TimeSpan.Zero, engine.Position);
    }

    [Fact]
    public void MissingTimelineDoesNotInventSynchronization()
    {
        var engine = new PlaybackSynchronizer(_clock);
        engine.Observe(Snapshot(20, hasTimeline: false));
        _clock.Advance(20);
        Assert.Equal(TimeSpan.Zero, engine.Position);
        Assert.Equal(LyricFrame.Empty, engine.GetFrame(new(new[] { Line(0) }), TimeSpan.Zero));
    }

    [Fact]
    public void OffsetsAdvanceSelectionAndBlankLinesRepresentGaps()
    {
        var engine = new PlaybackSynchronizer(_clock);
        engine.Observe(Snapshot(9, PlaybackState.Paused));
        var document = new LyricsDocument(new[] { Line(0), new LyricLine(TimeSpan.FromSeconds(10), ""), Line(20) },
            Offset: TimeSpan.FromMilliseconds(500));
        var frame = engine.GetFrame(document, TimeSpan.FromMilliseconds(500));
        Assert.Equal(1, frame.CurrentIndex);
        Assert.Equal("", frame.Current!.Text);
        Assert.Equal("0", frame.Previous!.Text);
        Assert.Equal("20", frame.Next!.Text);
    }

    [Fact]
    public void WordSelectionRequiresActualWordTiming()
    {
        var engine = new PlaybackSynchronizer(_clock);
        engine.Observe(Snapshot(2.5, PlaybackState.Paused));
        var words = new[] { new LyricWord(TimeSpan.FromSeconds(1), "one "), new LyricWord(TimeSpan.FromSeconds(2), "two") };
        Assert.Equal(1, engine.GetFrame(new(new[] { new LyricLine(TimeSpan.Zero, "one two", words) }), TimeSpan.Zero).WordIndex);
        Assert.Equal(-1, engine.GetFrame(new(new[] { Line(0) }), TimeSpan.Zero).WordIndex);
    }

    [Fact]
    public void BeforeFirstLineAndInstrumentalHaveNoCurrentLyric()
    {
        var engine = new PlaybackSynchronizer(_clock);
        engine.Observe(Snapshot(1, PlaybackState.Paused));
        var doc = new LyricsDocument(new[] { Line(10) });
        var frame = engine.GetFrame(doc, TimeSpan.Zero);
        Assert.Null(frame.Current);
        Assert.Equal(doc.Lines[0], frame.Next);
        Assert.Equal(LyricFrame.Empty, engine.GetFrame(doc with { IsInstrumental = true }, TimeSpan.Zero));
    }

    [Fact]
    public void StopAndMissingTrackStopExtrapolation()
    {
        var engine = new PlaybackSynchronizer(_clock);
        engine.Observe(Snapshot(10));
        _clock.Advance(1);
        engine.Observe(Snapshot(0, PlaybackState.Stopped));
        _clock.Advance(4);
        Assert.Equal(TimeSpan.Zero, engine.Position);
        engine.Observe(Snapshot() with { Track = null });
        _clock.Advance(4);
        Assert.Equal(TimeSpan.Zero, engine.Position);
    }

    private static LyricLine Line(double seconds) => new(TimeSpan.FromSeconds(seconds), seconds.ToString());
    private sealed class FakeClock : IMonotonicClock
    {
        public TimeSpan Elapsed { get; private set; }
        public void Advance(double seconds) => Elapsed += TimeSpan.FromSeconds(seconds);
    }
}
