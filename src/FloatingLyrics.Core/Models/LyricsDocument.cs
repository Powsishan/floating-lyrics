namespace FloatingLyrics.Core.Models;

public sealed record LyricWord(TimeSpan Start, string Text);

public sealed record LyricLine(TimeSpan Start, string Text, IReadOnlyList<LyricWord>? Words = null);

/// <param name="Offset">LRC offset. A positive value advances lyric selection.</param>
public sealed record LyricsDocument(
    IReadOnlyList<LyricLine> Lines,
    string? PlainText = null,
    bool IsInstrumental = false,
    TimeSpan Offset = default,
    string Provider = "",
    double Confidence = 1);

public sealed record LyricFrame(
    int CurrentIndex,
    LyricLine? Previous,
    LyricLine? Current,
    LyricLine? Next,
    int WordIndex)
{
    public static LyricFrame Empty { get; } = new(-1, null, null, null, -1);
}
