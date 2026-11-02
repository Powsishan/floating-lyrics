using FloatingLyrics.Core.Models;
using FloatingLyrics.Lyrics.Matching;
using Xunit;

namespace FloatingLyrics.Lyrics.Tests;

public sealed class TrackMatcherTests
{
    private static readonly TrackInfo Track = new("Original Song", "An Artist", "Album", TimeSpan.FromSeconds(180));

    [Fact]
    public void ExactMetadataScoresOne() => Assert.Equal(1, TrackMatcher.Confidence(Track, "Original Song", "An Artist", "Album", 180));
    [Fact]
    public void TypographyAndCaseVariationsMatch() => Assert.True(TrackMatcher.Confidence(Track, "original-song", "AN ARTIST", "album", 180) >= .97);
    [Theory]
    [InlineData("Original Song - Live", "An Artist", 180)]
    [InlineData("Original Song - Remastered", "An Artist", 180)]
    [InlineData("Original Song", "Other Artist", 180)]
    [InlineData("Original Song", "An Artist", 190)]
    public void RejectsDifferentRecordingsArtistsAndDurations(string title, string artist, double duration)
        => Assert.Equal(0, TrackMatcher.Confidence(Track, title, artist, "Album", duration));
    [Fact]
    public void MissingDurationAllowsConservativeMetadataMatch()
        => Assert.True(TrackMatcher.Confidence(Track with { Duration = null }, Track.Title, Track.Artist, "", 180) >= .9);
}
