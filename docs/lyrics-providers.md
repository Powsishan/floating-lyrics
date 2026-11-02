# Lyrics providers

`ILyricsProvider` is independent of media sessions. The MVP ships one implementation: LRCLIB. It uses exact metadata lookup, then bounded search, rejects mismatched recording titles/artists and durations more than three seconds apart, and requires confidence >= 0.90. Punctuation/case normalization is permitted; live/remix/acoustic/instrumental/remaster mismatches are rejected. Conservative matching can leave a valid song unmatched; that is preferable to silently showing the wrong recording.

Configure `LrcLibClientUrl` with the actual public GitHub repository URL. The default is the empty string. The UI disables online resolution with a clear message while the setting is invalid or unset. The User-Agent includes FloatingLyrics, its version and that configured URL. No invented homepage, personal email, account, API key or optional registration is required.

Requests are sequential with a 250 ms spacing deadline. HTTP 429/503 observes Retry-After (delta or date) plus jitter. A long server cooldown is retained and reported instead of sleeping indefinitely or retrying early. Cancellation of a track search does not remove the cooldown. Automatic retry is bounded. Errors and timeouts leave the overlay clear and expose a manual retry.

The parser accepts standard LRC, repeated line timestamps, millisecond fractions, explicit offset and validated enhanced LRC `<mm:ss.xx>` words. It preserves empty timed lines for instrumental gaps, Unicode and RTL text. Partially invalid word schedules fall back to line timing. The MVP renders a single current line, including when word times exist. No karaoke timing is inferred.

LRCLIB's current documentation also describes word data in a raw `lyricsfile` YAML field. This YAML format is not consumed by the MVP; the separate later-phase word-rendering feature requires format validation and genuine word data. Unsynchronized text and instrumental records are recognized, but unsynchronized lyrics are not scrolled against invented timestamps.

Caching, local file associations, candidate selection and configurable provider priority remain Phase 2/3 work. No QQ Music/NetEase unofficial endpoints, scraping or proprietary extraction is implemented. Do not distribute fetched lyrics or use provider API availability as proof of lyric copyright permission.

Sources: [LRCLIB API requirements](https://lrclib.net/docs.txt), [LRCLIB project](https://github.com/tranxuanthang/lrclib). API rules should be reviewed again before release. The runtime provider is tested through controlled HTTP handlers; live access remains gated on an actual project URL.
