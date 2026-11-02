using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FloatingLyrics.Core.Models;

public sealed record TrackInfo(string Title, string Artist, string Album, TimeSpan? Duration)
{
    /// <summary>A metadata identity, never a filesystem path or raw listening-history entry.</summary>
    public string Key
    {
        get
        {
            static string Normalize(string value) => string.Join(' ',
                value.Normalize(NormalizationForm.FormKC).ToUpperInvariant()
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

            var seconds = Duration is { } duration
                ? Math.Round(duration.TotalSeconds, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)
                : "unknown";
            var values = new[] { Normalize(Title), Normalize(Artist), Normalize(Album), seconds };
            // Length prefixes avoid collisions when metadata contains separators or newlines.
            var identity = string.Concat(values.Select(value => $"{value.Length}:{value}"));
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        }
    }
}
