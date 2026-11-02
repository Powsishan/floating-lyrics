# Feasibility evidence — 2 November 2026

## Decision

Proceed with native WPF/.NET 10. The workspace began empty and now contains a compiled Phase 1 application, independent media probe and automated test suite. The media and native window paths run on this Windows machine. Full online playback acceptance is still gated on the actual public repository URL and a deliberate player test matrix; the MVP is not certified production-ready.

## Environment and observed results

- Windows x64 build 26300; SDK 10.0.400; installed .NET/WindowsDesktop runtime 10.0.11. Framework support was checked against Microsoft's current support policy before implementation. Installed runtimes must be serviced before shipping self-contained releases.
- `dotnet restore FloatingLyrics.sln --disable-parallel` succeeded after granting NuGet network access. Sandbox-only MSBuild/test hosts failed with empty diagnostics or DNS errors; validation was rerun outside the sandbox.
- `dotnet build FloatingLyrics.sln --no-restore --configuration Release`: success, zero warnings and zero errors.
- Release xUnit tests: **75 passed, zero failures, zero skipped**: Core 16, Lyrics 38, Media 12, Integration 8, Platform 1. Raw TRX output is under `artifacts/TestResults` (generated, excluded from source control).
- Native interactive Windows test created actual layered WPF HWNDs. `WindowFromPoint` reached the underlying HWND when locked and the overlay HWND when unlocked. Topmost toggling, tool-window/no-activate/layered styles and off-screen recovery also passed. This is an automated native test, not a claim of manual mixed-DPI coverage.
- The actual WPF settings window was launched and inspected through Windows computer-use capture. It displayed real Apple Music track metadata and the unset-URL configuration state. Contrast issues found during inspection were corrected. No demo lyrics or synthetic media were injected into the app.
- A Release `--smoke-test` using an isolated workspace data directory successfully started, observed media, saved settings (including an empty `LrcLibClientUrl`) and exited with code 0.
- A framework-dependent folder build was created under `artifacts/portable`. Its packaged executable completed a separate smoke test; the file log recorded startup and completed shutdown, and settings remained local with `LrcLibClientUrl` empty. This is a local validation build requiring .NET 10 Desktop Runtime, not a clean-machine-tested installer or production release.

## Live media observations

The read-only GSMTC probe found one playing session with track metadata and a usable timeline, corresponding to the Apple Music source shown by the app. In a five-second observation it reported a 219-second track and coarse whole-second positions, including a one-second backward correction. That is evidence of an imperfect timeline, not evidence of accurately synchronized audible lyrics.

The media estimator now slews small continuous-play drift by at most 15%, preserving monotonic progression, and snaps discontinuities of two seconds or more. Regression tests cover that quantization pattern. Paused observations and track changes remain authoritative. A backward seek smaller than two seconds can be indistinguishable from coarse metadata and may reconcile gradually. An additional probe observed a paused session at 85 seconds of a 192-second track. No playback controls were sent to the player's session.

The first adapter-only probe used 0.250 CPU seconds over 5.01 wall seconds (4.99% of one core), 52.86 MiB working set and 1.07 MiB managed heap. The paused probe used 0.125 CPU seconds over 5.00 seconds, 51.55 MiB working set and 0.96 MiB managed heap. These short observations include initialization, events and console output. They do **not** measure WPF rendering, idle steady-state resource use, or establish a performance guarantee. Longer profiling remains a release task.

## Provider evidence and configuration gate

Current LRCLIB documentation was read before implementation. It requires an application name/version and real project URL or email, serialized requests with spacing, and honoring Retry-After with jitter. Following the user's direction, this project exposes `LrcLibClientUrl`, defaults it to unset, and does not issue online lyric requests until it contains the real public GitHub repository URL. The app does not fabricate a repository or substitute a personal contact.

Provider tests exercise real production HTTP-client code with controlled handlers: metadata query escaping, identification, exact/search behavior, incomplete signatures, conservative matching, ambiguous recordings, 404, malformed JSON, bounded responses, timeout/cancellation, 429 and retained Retry-After deadlines. Those tests are not live provider validation. No live LRCLIB lyric-retrieval success is claimed, and no fetched commercial lyrics are stored in this repository.

## Release gates still open

1. Assign the public project URL and perform live LRCLIB retrieval against legitimate known metadata; inspect the exact recording and sync accuracy during actual playback.
2. Exercise Apple Music pause/resume, forward/backward seek, rapid skips and player close/reopen. Observe Spotify and browser playback separately; their compatibility is currently unverified.
3. Test multiple real players concurrently, simultaneous playback, preferred/manual source recovery, stale/missing timelines and sleep/resume.
4. Manually verify drag/lock through another process, monitor disconnection, mixed DPI, long Unicode/RTL lyrics, high contrast and reduced motion. Native style/hit-test automation covers only part of this surface.
5. Profile long-running idle/playback CPU, memory and event-subscription retention. Confirm tray recovery after Explorer restart.
6. Only after MVP stability, advance to Phase 2. Advanced modes, presets, shortcuts, automatic fullscreen hiding, cache/local associations, installer, signing, update publishing and clean-machine releases remain backlog items.

## Compatibility and licensing sources

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy) and [supported Windows editions](https://learn.microsoft.com/en-us/dotnet/core/install/windows).
- [GSMTC API contract](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager) and [timeline properties](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessiontimelineproperties).
- [Layered window input behavior](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows).
- [LRCLIB API rules](https://lrclib.net/docs.txt).
- Exact restored NuGet `.nuspec` licenses were inspected: Microsoft.Extensions packages 10.0.0 are MIT; xUnit v3/runner are Apache-2.0; Microsoft.NET.Test.Sdk is MIT. The Windows SDK targeting pack references the Windows SDK license and is not labeled MIT. The project's MIT license applies to its own source; release distribution must retain applicable dependency/runtime notices.
