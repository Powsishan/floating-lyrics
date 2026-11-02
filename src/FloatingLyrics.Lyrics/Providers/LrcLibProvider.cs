using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FloatingLyrics.Core.Interfaces;
using FloatingLyrics.Core.Models;
using FloatingLyrics.Lyrics.Matching;
using FloatingLyrics.Lyrics.Parsers;

namespace FloatingLyrics.Lyrics.Providers;

/// <summary>Sequential, cancellable LRCLIB client. Never scrapes player applications.</summary>
public sealed class LrcLibProvider(HttpClient client, string clientIdentification) : ILyricsProvider, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim requestGate = new(1, 1);
    private DateTimeOffset notBefore = DateTimeOffset.MinValue;
    private static readonly TimeSpan MaximumAutomaticWait = TimeSpan.FromSeconds(30);

    public async Task<IReadOnlyList<LyricsDocument>> SearchAsync(TrackInfo track, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientIdentification);
        if (string.IsNullOrWhiteSpace(track.Title) || string.IsNullOrWhiteSpace(track.Artist)) return [];
        // Players sometimes omit albums or timelines. Search handles an incomplete signature
        // without relying on /get's stricter matching and deployment-specific validation.
        if (string.IsNullOrWhiteSpace(track.Album) || track.Duration is not { TotalSeconds: >= 1 and <= 3600 })
            return await SearchCandidatesAsync(track, cancellationToken).ConfigureAwait(false);
        var signature = $"track_name={Escape(track.Title)}&artist_name={Escape(track.Artist)}";
        if (!string.IsNullOrWhiteSpace(track.Album)) signature += $"&album_name={Escape(track.Album)}";
        if (track.Duration is { TotalSeconds: >= 1 and <= 3600 } duration)
            signature += "&duration=" + duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);

        using var exact = await GetAsync("get?" + signature, cancellationToken).ConfigureAwait(false);
        if (exact is not null)
        {
            var record = await ReadAsync<LrcLibRecord>(exact, cancellationToken).ConfigureAwait(false);
            var document = Convert(track, record);
            if (document is not null && (document.IsInstrumental || document.Lines.Count > 0)) return [document];
            // Search may provide a synchronized version even if the exact record is plain text.
            var candidates = await SearchCandidatesAsync(track, cancellationToken).ConfigureAwait(false);
            if (document is not null && !candidates.Any(c => c.Provider == document.Provider)) candidates.Add(document);
            return candidates.OrderByDescending(d => d.Lines.Count > 0).ThenByDescending(d => d.Confidence).ToArray();
        }
        return await SearchCandidatesAsync(track, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<LyricsDocument>> SearchCandidatesAsync(TrackInfo track, CancellationToken cancellationToken)
    {
        using var response = await GetAsync($"search?track_name={Escape(track.Title)}&artist_name={Escape(track.Artist)}", cancellationToken).ConfigureAwait(false);
        if (response is null) return [];
        var records = await ReadAsync<LrcLibRecord[]>(response, cancellationToken).ConfigureAwait(false) ?? [];
        var candidates = records.Take(20).Select(record => Convert(track, record)).OfType<LyricsDocument>()
            .OrderByDescending(d => d.Lines.Count > 0 || d.IsInstrumental).ThenByDescending(d => d.Confidence).ToList();
        if (candidates.Count > 1 && (candidates[0].Lines.Count > 0 || candidates[0].IsInstrumental) == (candidates[1].Lines.Count > 0 || candidates[1].IsInstrumental)
            && Math.Abs(candidates[0].Confidence - candidates[1].Confidence) < .01
            && !Equivalent(candidates[0], candidates[1]))
            return []; // Phase 1 has no candidate picker: uncertainty must not become arbitrary lyrics.
        return candidates;
    }

    private static bool Equivalent(LyricsDocument first, LyricsDocument second)
        => first.IsInstrumental == second.IsInstrumental && first.PlainText == second.PlainText
            && first.Offset == second.Offset && first.Lines.Count == second.Lines.Count
            && first.Lines.Zip(second.Lines).All(pair => pair.First.Start == pair.Second.Start && pair.First.Text == pair.Second.Text);

    private static LyricsDocument? Convert(TrackInfo track, LrcLibRecord? record)
    {
        if (record is null) return null;
        var confidence = TrackMatcher.Confidence(track, record.TrackName, record.ArtistName, record.AlbumName, record.Duration);
        if (confidence < .90) return null;
        var providerName = $"LRCLIB · {record.Id}";
        if (record.Instrumental) return new LyricsDocument([], IsInstrumental: true, Provider: providerName, Confidence: confidence);
        var parsed = LrcParser.Parse(record.SyncedLyrics, providerName, confidence);
        if (parsed.Lines.Count == 0 && string.IsNullOrWhiteSpace(record.PlainLyrics)) return null;
        return parsed with { PlainText = record.PlainLyrics };
    }

    private async Task<HttpResponseMessage?> GetAsync(string path, CancellationToken cancellationToken)
    {
        await requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var wait = notBefore - DateTimeOffset.UtcNow;
                if (wait > MaximumAutomaticWait) throw new LyricsRateLimitException(wait);
                if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://lrclib.net/api/" + path);
                request.Headers.TryAddWithoutValidation("User-Agent", clientIdentification);
                request.Headers.Accept.ParseAdd("application/json");
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                notBefore = DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(250);
                if (response.StatusCode == HttpStatusCode.NotFound) { response.Dispose(); return null; }
                if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
                {
                    var retry = response.Headers.RetryAfter;
                    var delay = retry?.Delta ?? (retry?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromSeconds(5);
                    if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
                    // Retain this deadline across calls; cancelling an old track must not bypass a server cooldown.
                    delay += TimeSpan.FromMilliseconds(100 + Random.Shared.Next(100));
                    notBefore = DateTimeOffset.UtcNow + delay;
                    response.Dispose();
                    if (attempt == 1 || delay > MaximumAutomaticWait) throw new LyricsRateLimitException(delay);
                    continue;
                }
                try
                {
                    response.EnsureSuccessStatusCode();
                    // Bound even chunked responses before JSON parsing and include body download in the timeout.
                    await response.Content.LoadIntoBufferAsync(2 * 1024 * 1024, timeout.Token).ConfigureAwait(false);
                }
                catch { response.Dispose(); throw; }
                return response;
            }
            throw new HttpRequestException("LRCLIB is temporarily unavailable.");
        }
        finally { requestGate.Release(); }
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentType?.MediaType is not ("application/json" or "application/problem+json"))
            throw new HttpRequestException("LRCLIB returned an unsupported response format.");
        if (response.Content.Headers.ContentLength > 2 * 1024 * 1024)
            throw new HttpRequestException("LRCLIB returned a response larger than the permitted limit.");
        try { return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false); }
        catch (JsonException ex) { throw new HttpRequestException("LRCLIB returned an invalid lyrics response.", ex); }
    }

    private static string Escape(string text) => Uri.EscapeDataString(text.Trim());
    public void Dispose() => requestGate.Dispose();

    private sealed record LrcLibRecord(long Id, string? TrackName, string? ArtistName, string? AlbumName,
        double Duration, bool Instrumental, string? PlainLyrics, string? SyncedLyrics);
}

public sealed class LyricsRateLimitException(TimeSpan retryAfter)
    : HttpRequestException("LRCLIB has requested a cooldown. Try again later.", null, HttpStatusCode.TooManyRequests)
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}
