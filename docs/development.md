# Development

Use Windows with .NET 10 SDK (`global.json` permits current .NET 10 feature bands). The WPF app, media adapter and platform tests target `net10.0-windows10.0.19041.0`; Core, Lyrics and Infrastructure are platform independent `net10.0` libraries. The supported Windows API floor is 1809. No Windows App SDK, browser runtime or workload installer is required for this unpackaged build.

Build with `dotnet build FloatingLyrics.sln -c Release`. Run all tests with `dotnet test FloatingLyrics.sln -c Release --no-build`. Native platform tests briefly create temporary WPF windows on an interactive desktop; keep the machine unlocked. Do not label them passed when omitted in a headless environment. Core/Lyrics/Integration tests can be run separately by project.

The application is single-instance per Windows session. Use the tray to exit before launching a rebuilt version. `--smoke-test --data-dir <directory>` starts the actual app, observes actual media for five seconds, saves settings, disposes subscriptions and exits. It contains no generated media session or demo lyrics.

Source ownership:

- `Core/Models` and `Core/Synchronization`: immutable domain state and monotonic timing.
- `Media/WindowsSessions`: read-only Windows Runtime APIs and session arbitration.
- `Lyrics/Parsers`, `Matching`, `Providers`: validated LRC, recording selection and serialized HTTP.
- `Platform`: layered HWND styles, physical-pixel placement and tray icon lifetime.
- `Infrastructure`: local persistence and asynchronous logging.
- `App`: bindings and dispatcher-only rendering/orchestration.

Never fetch lyrics in a render callback. Cancel and reject stale track lookups. Never write listening history to logs. Keep player-specific integrations behind the media adapter boundary and only use authorized APIs.

The diagnostics tool reports real observations; CPU/memory samples are specific to that process and observation period. To assess the app, use Task Manager, Visual Studio profiler or dotnet-counters during idle, playback, rapid seeking and rapid skips. Record hardware, build, time span and workload. No numerical resource guarantee has been established.

When selecting package updates, inspect exact NuGet license metadata, supported TFMs and security advisories. Pin reviewed versions. Keep tests on maintained xUnit v3; v2 has been deprecated. Build/test CI may be added independently of release publishing, but no automated signing/update endpoint should be fabricated.
