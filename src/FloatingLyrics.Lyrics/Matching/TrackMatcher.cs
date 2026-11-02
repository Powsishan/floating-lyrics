using System.Text;
using System.Text.RegularExpressions;
using FloatingLyrics.Core.Models;

namespace FloatingLyrics.Lyrics.Matching;

public static partial class TrackMatcher
{
    [GeneratedRegex(@"[^\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex Separators();
    [GeneratedRegex(@"\b(live|remaster(?:ed)?|acoustic|instrumental|karaoke|remix|demo|radio edit|sped up|slowed)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionTags();

    public static double Confidence(TrackInfo requested, string? title, string? artist, string? album, double durationSeconds)
    {
        var requestedTitle = Normalize(requested.Title);
        var candidateTitle = Normalize(title);
        var requestedArtist = Normalize(requested.Artist);
        var candidateArtist = Normalize(artist);
        if (requestedTitle.Length == 0 || candidateTitle.Length == 0 || requestedArtist.Length == 0 || candidateArtist.Length == 0)
            return 0;
        // Never silently substitute a live, remixed or other explicitly different recording.
        var requestedVersions = VersionTags().Matches(requestedTitle).Select(m => CanonicalVersion(m.Value)).Distinct().Order().ToArray();
        var candidateVersions = VersionTags().Matches(candidateTitle).Select(m => CanonicalVersion(m.Value)).Distinct().Order().ToArray();
        if (!requestedVersions.SequenceEqual(candidateVersions)) return 0;
        if (requestedTitle != candidateTitle || requestedArtist != candidateArtist) return 0;

        var confidence = .90;
        if (requested.Duration is { } duration && duration > TimeSpan.Zero)
        {
            if (!double.IsFinite(durationSeconds) || durationSeconds <= 0) return 0;
            var delta = Math.Abs(duration.TotalSeconds - durationSeconds);
            if (delta > 3) return 0;
            confidence += delta <= 2 ? .07 : .02;
        }
        if (Normalize(requested.Album) is { Length: > 0 } requestedAlbum && requestedAlbum == Normalize(album)) confidence += .03;
        return Math.Min(confidence, 1);
    }

    private static string CanonicalVersion(string value) => value.StartsWith("remaster", StringComparison.Ordinal) ? "remaster" : value;
    private static string Normalize(string? value) => Separators().Replace((value ?? "").Normalize(NormalizationForm.FormKC).ToLowerInvariant(), " ").Trim();
}
