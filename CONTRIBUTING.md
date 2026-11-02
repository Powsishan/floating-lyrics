# Contributing

Build and test using docs/development.md. Keep issues and pull requests narrowly scoped; explain a concrete trigger, behavior and validation. Create issues and pull requests at https://github.com/Powsishan/floating-lyrics.

Core domain and timing logic must remain independent of WPF. Use cancellation for I/O, unsubscribe events, and marshal UI changes to the dispatcher. Add regression tests for parser, clock, recording matching and provider behavior changes. Use original synthetic text in fixtures; never add commercial lyrics, credentials or copied proprietary assets.

For player reports, record the actual tested player/Windows versions and the metadata/timeline observations. Do not mark all GSMTC-capable players as tested. Release gates include real Apple Music/Spotify seek/pause tests, mixed DPI, display removal, sleep/resume, Explorer restart and clean-machine deployment.

Respect LRCLIB identification and throttling. Additional providers need verified authorization and published API terms. Unsupported scraping and app-content extraction are out of scope. Changes adding network destinations or persistent data must update privacy documentation.

Contributions are licensed under the repository's MIT license. Preserve dependency notices and review licenses before adding a package or asset.
