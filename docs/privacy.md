# Privacy

Floating Lyrics stores settings and diagnostic logs locally. It does not collect telemetry, maintain a listening-history database, require an account, or communicate with an update service.

When LRCLIB is configured, the current track title, artist, album and duration are sent over HTTPS to `lrclib.net`. Its client identification includes the app name/version and the configured public repository URL. No request is made while `LrcLibClientUrl` is unset or invalid. LRCLIB receives normal connection information such as the requesting IP address under its own service policies.

Settings include source preference, overlay position/style and timing compensation. They do not store the current track or lyrics. Default storage is `%LOCALAPPDATA%\FloatingLyrics`; portable/data-directory flags can change it. Logs are bounded and record states/error types, never full provider URLs, track metadata or lyric text. There are no persistent lyric caches in the MVP.

The diagnostics tool hides metadata by default. `--include-metadata` intentionally prints the current media metadata to your console; do not share that output without reviewing it. Screenshots can reveal the current song. Reports should redact personal information.
