# Architecture and execution plan

## Initial feasibility assessment — 2 November 2026

The workspace was empty. A native, unpackaged WPF application is feasible. .NET 10 is the current LTS and a 10.0.400 SDK is installed. Its API floor is Windows 10 1809 for GSMTC; official runtime support depends on Windows edition/lifecycle. Windows 11 and supported Windows 10 LTSC/Enterprise installations are the release targets. Other Windows 10 editions require separate compatibility testing and are not claimed as vendor-supported.

WPF offers mature text layout, per-monitor DPI and a layered transparent HWND. WinUI 3 would introduce Windows App SDK deployment and additional transparent-window interop. WinForms has simpler tray integration but less suitable text composition and binding. WPF with the built-in Windows Forms NotifyIcon avoids another tray package. No browser engine is required.

The principal unknown is the information each player actually publishes: GSMTC is a contract, not a guarantee of correct metadata or an accurate clock. Exclusive fullscreen applications can prevent desktop overlays. LRCLIB may have no matching timed recording. None of these conditions should terminate the overlay process.

## Boundaries

- Core: immutable track/lyrics/playback models, monotonic playback estimator, line selection; no Windows or WPF dependencies.
- Media: Windows Runtime session subscriptions, deterministic session selection and adaptive reconciliation; consumes no lyric provider.
- Lyrics: LRC parsing, conservative LRCLIB matching and cancellable HTTP. No scraping or proprietary extraction.
- Infrastructure: validated local JSON settings and bounded asynchronous file logging.
- Platform: HWND input styles, monitor recovery and system tray lifecycle.
- App: WPF views, MVVM settings and orchestration. Media callbacks marshal to the dispatcher; the render timer never performs I/O.
- Diagnostics: a separate real GSMTC console probe with opt-in metadata output.

On a new track the app cancels the old lookup, clears old lines, resets timing and resolves lyrics asynchronously. A generation check rejects late responses. The UI changes text only when the selected lyric changes. Paused playback stops monotonic extrapolation. Missing timelines disable timed display rather than inventing progress.

## Prioritized backlog and gates

1. P0: build/test parser and synchronization; compile native media probe; exercise actual Windows sessions and provider HTTP.
2. P0: connect live media → LRCLIB → monotonic timing → transparent overlay; handle pause, seek, track changes and provider failures.
3. P0: prove click-through, unlock/drag, topmost, tray lifetime, position recovery and settings persistence.
4. P0 release gate: manually test Apple Music, Spotify and a browser with real playback. Record pause/seek accuracy, missing timelines and network failures. This gate cannot be replaced by unit tests.
5. P1 after stable MVP: presets, multiple modes, global shortcuts, per-player offsets, richer monitor placement, cache and local association.
6. P2: genuine word timings, candidate selection and authorized provider extensions. No unofficial QQ/NetEase endpoints.
7. Release: clean-machine portable/installer validation, CI, third-party notices, screenshots of real use, signing strategy. No production-ready claim until these gates pass.

## Dependencies and licensing

Runtime packages are Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging and Microsoft.Extensions.Logging.Abstractions 10.0.0 (MIT). Windows SDK .NET projections are provided through the Windows target framework; Microsoft.Windows.SDK.NET.Ref 10.0.19041.57 references the Windows SDK license and requires license acceptance, rather than declaring MIT. WPF/WinForms/.NET runtime are MIT; Windows APIs themselves require Windows. Tests use xUnit v3 3.2.2 (Apache-2.0), the Visual Studio xUnit runner 3.1.1 (Apache-2.0), and Microsoft.NET.Test.Sdk 17.14.1 (MIT). NuGet license metadata and exact resolved versions must be retained in release review. No external artwork, fonts, or commercial lyric fixtures are bundled. Segoe UI is requested from the operating system, not redistributed.

An MIT project license is compatible with these code dependencies. Provider access is separate from copyright permission for lyrics: LRCLIB identification/rate rules must be followed; fetched lyrics are not a bundled project asset.

## Sources

- [.NET lifecycle](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [Supported Windows editions](https://learn.microsoft.com/en-us/dotnet/core/install/windows)
- [GSMTC manager](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager)
- [Layered window hit testing](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows)
- [Extended HWND styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles)
- [LRCLIB API](https://lrclib.net/docs.txt)

Measured findings and unresolved gates belong in feasibility.md; this file describes the design, not a claim that hardware/player tests have passed.
