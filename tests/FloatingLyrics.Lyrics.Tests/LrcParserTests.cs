using FloatingLyrics.Lyrics.Parsers;
using Xunit;

namespace FloatingLyrics.Lyrics.Tests;

public sealed class LrcParserTests
{
    [Theory]
    [InlineData("[01:02]line", 62000)]
    [InlineData("[01:02.3]line", 62300)]
    [InlineData("[01:02.34]line", 62340)]
    [InlineData("[01:02.345]line", 62345)]
    public void ReadsTimestampPrecision(string input, double milliseconds)
    {
        var line = Assert.Single(LrcParser.Parse(input).Lines);
        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), line.Start);
        Assert.Equal("line", line.Text);
        Assert.Null(line.Words);
    }

    [Fact]
    public void MultipleTagsRepeatAndSortLines()
    {
        var document = LrcParser.Parse("[00:20][00:10]repeat\n[00:15]middle");
        Assert.Equal(new[] { 10d, 15d, 20d }, document.Lines.Select(l => l.Start.TotalSeconds));
        Assert.Equal(new[] { "repeat", "middle", "repeat" }, document.Lines.Select(l => l.Text));
    }

    [Fact]
    public void ReadsOffsetAndMetadataWithoutDisplayingTags()
    {
        var document = LrcParser.Parse("[ar:An Artist]\n[offset:+250]\n[00:05]original text");
        Assert.Equal(TimeSpan.FromMilliseconds(250), document.Offset);
        Assert.Equal(TimeSpan.FromSeconds(5), Assert.Single(document.Lines).Start);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[00:99]bad\n[9999:00]bad\n[xx:yy]bad")]
    public void InvalidOrEmptyInputHasNoTimedLines(string input) => Assert.Empty(LrcParser.Parse(input).Lines);

    [Fact]
    public void UnicodeAndRightToLeftRemainIntact()
    {
        var lines = LrcParser.Parse("[00:01]مرحبا بالعالم\n[00:02]සිංහල 日本語").Lines;
        Assert.Equal("مرحبا بالعالم", lines[0].Text);
        Assert.Equal("සිංහල 日本語", lines[1].Text);
    }

    [Fact]
    public void EnhancedLrcUsesGenuineWordTimestamps()
    {
        var line = Assert.Single(LrcParser.Parse("[00:01]<00:01.20>hello <00:01.75>world<00:02.00>").Lines);
        Assert.Equal("hello world", line.Text);
        Assert.NotNull(line.Words);
        Assert.Equal(new[] { 1.2, 1.75 }, line.Words.Select(w => w.Start.TotalSeconds));
        Assert.Equal(new[] { "hello ", "world" }, line.Words.Select(w => w.Text));
    }

    [Theory]
    [InlineData("[00:01]untimed <00:02>timed")]
    [InlineData("[00:01]<00:03>one <00:02>two")]
    [InlineData("[00:02]<00:01>too early")]
    public void PartiallyTimedOrDescendingWordsFallBackToLineTiming(string input)
        => Assert.Null(Assert.Single(LrcParser.Parse(input).Lines).Words);

    [Fact]
    public void EmptyTimedLineMarksInstrumentalGap()
    {
        var lines = LrcParser.Parse("[00:01]line\n[00:10]\n[00:20]line").Lines;
        Assert.Equal("", lines[1].Text);
    }

    [Fact]
    public void PlainTextDoesNotInventTiming()
    {
        var document = LrcParser.Parse("first line\nsecond line");
        Assert.Empty(document.Lines);
        Assert.Contains("first line", document.PlainText);
    }
}
