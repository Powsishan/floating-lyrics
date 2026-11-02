using System.Globalization;
using System.Text.RegularExpressions;
using FloatingLyrics.Core.Models;

namespace FloatingLyrics.Lyrics.Parsers;

/// <summary>Parses line LRC and the enhanced LRC &lt;mm:ss.xx&gt; word convention.</summary>
public static partial class LrcParser
{
    // Bound timestamps to a day so malformed input cannot overflow TimeSpan.
    [GeneratedRegex(@"\[(?<m>\d{1,4}):(?<s>\d{2})(?:[.:](?<f>\d{1,3}))?\]", RegexOptions.CultureInvariant)]
    private static partial Regex LineStamp();
    [GeneratedRegex(@"<(?<m>\d{1,4}):(?<s>\d{2})(?:[.:](?<f>\d{1,3}))?>", RegexOptions.CultureInvariant)]
    private static partial Regex WordStamp();
    [GeneratedRegex(@"^\[offset:\s*(?<v>[+-]?\d+)\s*\]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OffsetTag();
    [GeneratedRegex(@"^\[[a-zA-Z]+:.*\]$", RegexOptions.CultureInvariant)]
    private static partial Regex MetadataTag();

    public static LyricsDocument Parse(string? content, string provider = "Local", double confidence = 1)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new LyricsDocument([], Provider: provider, Confidence: confidence);

        var lines = new List<LyricLine>();
        var plain = new List<string>();
        var offset = TimeSpan.Zero;
        foreach (var raw in content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var text = raw.Trim('\uFEFF', ' ', '\t');
            var offsetMatch = OffsetTag().Match(text);
            if (offsetMatch.Success)
            {
                if (long.TryParse(offsetMatch.Groups["v"].Value, CultureInfo.InvariantCulture, out var milliseconds)
                    && milliseconds is >= -86400000 and <= 86400000)
                    offset = TimeSpan.FromMilliseconds(milliseconds);
                continue;
            }

            var stamps = new List<TimeSpan>();
            var prefixEnd = 0;
            foreach (Match match in LineStamp().Matches(text))
            {
                // Only leading, adjacent tags are timestamps. Literal brackets in lyrics remain text.
                if (match.Index != prefixEnd) break;
                prefixEnd = match.Index + match.Length;
                if (TryTimestamp(match, out var start)) stamps.Add(start);
            }

            if (stamps.Count == 0)
            {
                if (prefixEnd == 0 && !MetadataTag().IsMatch(text) && text.Length > 0 && !text.StartsWith('['))
                    plain.Add(text);
                continue;
            }

            var body = text[prefixEnd..].Trim();
            var (lineText, words) = ParseWords(body, stamps[0]);
            foreach (var start in stamps)
            {
                // Repeated line tags repeat the word schedule with the same relative timing.
                var shiftedWords = words?.Select(w => w with { Start = w.Start + start - stamps[0] }).ToArray();
                lines.Add(new LyricLine(start, lineText, shiftedWords));
            }
        }

        var sorted = lines.OrderBy(l => l.Start).ToArray();
        return new LyricsDocument(sorted, sorted.Length == 0 && plain.Count > 0 ? string.Join(Environment.NewLine, plain) : null,
            Offset: offset, Provider: provider, Confidence: confidence);
    }

    private static (string Text, IReadOnlyList<LyricWord>? Words) ParseWords(string body, TimeSpan lineStart)
    {
        var matches = WordStamp().Matches(body);
        if (matches.Count == 0) return (body, null);
        var clean = WordStamp().Replace(body, "");
        // Partially timed lines must fall back to line-level timing rather than invent word times.
        if (matches[0].Index > 0 && !string.IsNullOrWhiteSpace(body[..matches[0].Index])) return (clean, null);
        var words = new List<LyricWord>();
        var last = lineStart;
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            if (!TryTimestamp(match, out var start) || start < last) return (clean, null);
            var end = i + 1 < matches.Count ? matches[i + 1].Index : body.Length;
            var word = body[(match.Index + match.Length)..end];
            if (word.Length > 0) words.Add(new LyricWord(start, word));
            last = start;
        }
        return (clean, words.Count > 0 ? words : null);
    }

    private static bool TryTimestamp(Match match, out TimeSpan result)
    {
        result = default;
        if (!int.TryParse(match.Groups["m"].Value, CultureInfo.InvariantCulture, out var minutes) || minutes >= 1440
            || !int.TryParse(match.Groups["s"].Value, CultureInfo.InvariantCulture, out var seconds) || seconds >= 60)
            return false;
        var fraction = match.Groups["f"].Value;
        var milliseconds = fraction.Length == 0 ? 0 : int.Parse(fraction.PadRight(3, '0'), CultureInfo.InvariantCulture);
        result = TimeSpan.FromMilliseconds((minutes * 60L + seconds) * 1000 + milliseconds);
        return true;
    }
}
