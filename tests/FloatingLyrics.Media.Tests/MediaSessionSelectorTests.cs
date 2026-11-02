using FloatingLyrics.Core.Models;
using FloatingLyrics.Media.WindowsSessions;
using Xunit;

namespace FloatingLyrics.Media.Tests;

public sealed class MediaSessionSelectorTests
{
    private static MediaSessionInfo Session(string id, PlaybackState state, string? source = null)
        => new(id, source ?? id, id, state, false);

    [Fact]
    public void PlayingSessionWinsOverPausedStickySession()
    {
        var sessions = new[] { Session("a", PlaybackState.Paused), Session("b", PlaybackState.Playing) };
        Assert.Equal("b", MediaSessionSelector.Select(sessions, null, "a", "a", "a"));
    }

    [Fact]
    public void SimultaneousPlaybackKeepsExistingChoice()
    {
        var sessions = new[] { Session("a", PlaybackState.Playing), Session("b", PlaybackState.Playing) };
        Assert.Equal("a", MediaSessionSelector.Select(sessions, null, "b", "a", "b"));
    }

    [Fact]
    public void ManualChoiceWinsEvenWhenPaused()
    {
        var sessions = new[] { Session("a", PlaybackState.Playing), Session("b", PlaybackState.Paused) };
        Assert.Equal("b", MediaSessionSelector.Select(sessions, "b", "a", "a", "a"));
    }

    [Fact]
    public void ClosedManualChoiceFallsBackToPreferredPlayingSource()
    {
        var sessions = new[] { Session("a", PlaybackState.Playing), Session("b", PlaybackState.Playing, "preferred") };
        Assert.Equal("b", MediaSessionSelector.Select(sessions, "closed", "preferred", "closed", "a"));
    }

    [Fact]
    public void EmptyListHasNoSelection() => Assert.Null(MediaSessionSelector.Select([], null, null, null, null));

    [Fact]
    public void FallbackIsIndependentOfEnumerationOrder()
    {
        var a = Session("a", PlaybackState.Playing);
        var b = Session("b", PlaybackState.Playing);
        Assert.Equal(MediaSessionSelector.Select([a, b], null, null, null, null),
            MediaSessionSelector.Select([b, a], null, null, null, null));
    }
}
