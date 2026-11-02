using FloatingLyrics.Core.Models;
using Xunit;

namespace FloatingLyrics.Core.Tests;

public sealed class TrackInfoTests
{
    [Fact]
    public void IdentityNormalizesWhitespaceCaseUnicodeAndSubsecondDuration()
    {
        var track = new TrackInfo("  Ｓｏｎｇ  Name ", "artist", "album", TimeSpan.FromSeconds(60.1));
        var equivalent = new TrackInfo("song\tname", "ARTIST", "Album", TimeSpan.FromSeconds(60.2));
        Assert.Equal(track.Key, equivalent.Key);
        Assert.Equal(64, track.Key.Length);
    }

    [Fact]
    public void DifferentRecordingsAndFieldBoundariesHaveDifferentKeys()
    {
        var track = new TrackInfo("Title", "Artist", "Album", TimeSpan.FromSeconds(60));
        Assert.NotEqual(track.Key, (track with { Duration = TimeSpan.FromSeconds(80) }).Key);
        Assert.NotEqual(new TrackInfo("ab", "c", "", null).Key, new TrackInfo("a", "bc", "", null).Key);
        Assert.NotEqual((track with { Duration = null }).Key, (track with { Duration = TimeSpan.Zero }).Key);
    }
}
