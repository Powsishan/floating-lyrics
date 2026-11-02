using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FloatingLyrics.Core.Models;
using FloatingLyrics.Lyrics.Providers;
using Xunit;

namespace FloatingLyrics.Lyrics.Tests;

public sealed class LrcLibProviderTests
{
    private static readonly TrackInfo Track = new("Test Song", "Test Artist", "Test Album", TimeSpan.FromSeconds(180));
    // Synthetic test text, never copied from published lyrics.
    private const string Record = """
        {"id":7,"trackName":"Test Song","artistName":"Test Artist","albumName":"Test Album","duration":180,"instrumental":false,"plainLyrics":"one synthetic line","syncedLyrics":"[00:01]one synthetic line"}
        """;

    [Fact]
    public async Task ExactMatchUsesMetadataAndIdentification()
    {
        using var handler = new StubHandler((request, _) =>
        {
            Assert.Contains("track_name=Test%20Song", request.RequestUri!.Query);
            Assert.Contains("duration=180", request.RequestUri.Query);
            Assert.Contains("album_name=Test%20Album", request.RequestUri.Query);
            Assert.Contains("FloatingLyricsTests/1.0", request.Headers.UserAgent.ToString());
            return Task.FromResult(Json(Record));
        });
        using var client = new HttpClient(handler);
        using var provider = NewProvider(client);
        var document = Assert.Single(await provider.SearchAsync(Track, TestContext.Current.CancellationToken));
        Assert.Equal("one synthetic line", Assert.Single(document.Lines).Text);
        Assert.Equal(1, handler.Count);
        Assert.Equal(1, document.Confidence);
    }

    [Fact]
    public async Task MissingExactMatchSearchesCandidates()
    {
        using var handler = new StubHandler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/get", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.NotFound) : Json("[" + Record + "]")));
        using var client = new HttpClient(handler);
        using var provider = NewProvider(client);
        Assert.Single(await provider.SearchAsync(Track, TestContext.Current.CancellationToken));
        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task MissingLyricsReturnEmptyCandidates()
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var client = new HttpClient(handler);
        using var provider = NewProvider(client);
        Assert.Empty(await provider.SearchAsync(Track, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InstrumentalIsARealResult()
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(Json(Record.Replace("\"instrumental\":false", "\"instrumental\":true", StringComparison.Ordinal))));
        using var client = new HttpClient(handler);
        using var provider = NewProvider(client);
        var document = Assert.Single(await provider.SearchAsync(Track, TestContext.Current.CancellationToken));
        Assert.True(document.IsInstrumental);
        Assert.Empty(document.Lines);
    }

    [Fact]
    public async Task WrongRecordingIsNeverSelected()
    {
        var wrong = Record.Replace("Test Song", "Test Song - Live", StringComparison.Ordinal);
        using var handler = new StubHandler((request, _) => Task.FromResult(Json(request.RequestUri!.AbsolutePath.EndsWith("/get", StringComparison.Ordinal) ? wrong : "[" + wrong + "]")));
        using var client = new HttpClient(handler);
        using var provider = NewProvider(client);
        Assert.Empty(await provider.SearchAsync(Track, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LongRetryAfterFailsWithoutRetryingEarlyAndPersistsAcrossTracks()
    {
        using var handler = new StubHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(5));
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);
        using var provider = NewProvider(client);
        var error = await Assert.ThrowsAsync<LyricsRateLimitException>(() => provider.SearchAsync(Track, TestContext.Current.CancellationToken));
        Assert.True(error.RetryAfter >= TimeSpan.FromMinutes(5));
        await Assert.ThrowsAsync<LyricsRateLimitException>(() => provider.SearchAsync(Track with { Title = "Next" }, TestContext.Current.CancellationToken));
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task ShortRateLimitRetriesOnlyOnce()
    {
        using var handler = new StubHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);
        using var provider = NewProvider(client);
        await Assert.ThrowsAsync<LyricsRateLimitException>(() => provider.SearchAsync(Track, TestContext.Current.CancellationToken));
        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task InvalidJsonIsARecoverableProviderError()
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(Json("{broken")));
        using var client = new HttpClient(handler);
        using var provider = NewProvider(client);
        await Assert.ThrowsAsync<HttpRequestException>(() => provider.SearchAsync(Track, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HtmlResponseIsRejected()
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("<html>maintenance</html>", Encoding.UTF8, "text/html") }));
        using var client = new HttpClient(handler);
        using var provider = NewProvider(client);
        await Assert.ThrowsAsync<HttpRequestException>(() => provider.SearchAsync(Track, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RapidTrackChangeCancelsInflightRequest()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new StubHandler(async (_, cancellationToken) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Json(Record);
        });
        using var client = new HttpClient(handler);
        using var provider = NewProvider(client);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = provider.SearchAsync(Track, cancellation.Token);
        await started.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task TimeoutPropagatesWithoutAnAggressiveRetry()
    {
        using var handler = new StubHandler((_, _) => throw new TaskCanceledException("Synthetic timeout"));
        using var client = new HttpClient(handler);
        using var provider = NewProvider(client);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.SearchAsync(Track, TestContext.Current.CancellationToken));
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task EmptyMetadataDoesNotContactProvider()
    {
        using var handler = new StubHandler((_, _) => throw new InvalidOperationException("Should not send"));
        using var client = new HttpClient(handler);
        using var provider = NewProvider(client);
        Assert.Empty(await provider.SearchAsync(Track with { Artist = "" }, TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IncompleteSignatureUsesSearchDirectly(bool missingDuration)
    {
        using var handler = new StubHandler((request, _) =>
        {
            Assert.EndsWith("/search", request.RequestUri!.AbsolutePath);
            return Task.FromResult(Json("[" + Record + "]"));
        });
        using var client = new HttpClient(handler);
        using var provider = NewProvider(client);
        var track = missingDuration ? Track with { Duration = null } : Track with { Album = "" };
        Assert.Single(await provider.SearchAsync(track, TestContext.Current.CancellationToken));
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task AmbiguousEquallyPlausibleRecordingsAreNotAutomaticallySelected()
    {
        var second = Record.Replace("\"id\":7", "\"id\":8", StringComparison.Ordinal)
            .Replace("[00:01]", "[00:05]", StringComparison.Ordinal);
        using var handler = new StubHandler((_, _) => Task.FromResult(Json("[" + Record + "," + second + "]")));
        using var client = new HttpClient(handler);
        using var provider = NewProvider(client);
        Assert.Empty(await provider.SearchAsync(Track with { Duration = null }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SynchronizedSearchCandidateIsPreferredToPlainExactMatch()
    {
        var plain = Record.Replace("\"syncedLyrics\":\"[00:01]one synthetic line\"", "\"syncedLyrics\":null", StringComparison.Ordinal);
        var synchronized = Record.Replace("\"id\":7", "\"id\":8", StringComparison.Ordinal);
        using var handler = new StubHandler((request, _) => Task.FromResult(Json(request.RequestUri!.AbsolutePath.EndsWith("/get", StringComparison.Ordinal)
            ? plain : "[" + plain + "," + synchronized + "]")));
        using var client = new HttpClient(handler);
        using var provider = NewProvider(client);
        var candidates = await provider.SearchAsync(Track, TestContext.Current.CancellationToken);
        Assert.Equal("one synthetic line", Assert.Single(candidates[0].Lines).Text);
    }

    private static LrcLibProvider NewProvider(HttpClient client) => new(client, "FloatingLyricsTests/1.0 (https://example.org/)");
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Count { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            return send(request, cancellationToken);
        }
    }
}
