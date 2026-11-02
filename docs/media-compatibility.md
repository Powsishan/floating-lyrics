# Media sessions and compatibility

Floating Lyrics reads Windows Global System Media Transport Controls (GSMTC). A player is compatible only when it publishes usable session metadata and a timeline. There are no private player endpoints, browser scraping, process injection, or playback-control calls.

Microsoft documents GSMTC as available from Windows 10 version 1809, with the `globalMediaControl` capability listed for packaged applications. The project targets the Windows 10 2004 SDK surface. The supported operating-system matrix for the chosen .NET runtime is a separate constraint; consult the feasibility report before making a support commitment for an older Windows edition.

## Evidence collected on 2 November 2026

The application was launched against the actual Windows desktop. An Apple Music media session and actual track metadata were visible in settings. This establishes real session enumeration and metadata retrieval for the installed Apple Music instance. It does not establish lyric accuracy or timing correctness. Track names and artist names are intentionally excluded from this repository.

No playback controls were sent. Pause/resume, seeking, next-track transitions, and end-to-end network lyric retrieval have not been certified through this observation. The diagnostic CLI is read-only and prints metadata only when explicitly invoked with `--include-metadata`.

A five-second read-only diagnostic probe subsequently reported one playing session, real track metadata, and a usable 219-second timeline. Repeated observations contained whole-second corrections, including a one-second backward correction, while this task sent no playback controls. The player timeline therefore cannot be treated as perfectly precise. This finding led to bounded reconciliation of small timing drift and a regression test using the observed quantization pattern. No user's listening metadata is included here.

The same 5.01-second diagnostic interval measured 0.250 seconds of process CPU time (4.99% of one core), a 52.86 MiB working set, and a 1.07 MiB managed heap. It included event processing and console output and did not measure the WPF application. The short interval is an observation, not an idle-CPU or production-performance claim.

### Player status

- Apple Music: actual session and track metadata detected on this machine. Pause, resume, seeks, skip, timeline accuracy, and lyric matching still require the manual checks below.
- Spotify: not manually verified.
- TIDAL: not manually verified.
- YouTube Music in Chrome or Edge: not manually verified. Browser media-session policy and the website determine what Windows exposes.
- Windows Media Player: not manually verified.
- VLC: not manually verified. A build or integration exposing GSMTC may be required.
- Other desktop or browser players: not manually verified. They use the same adapter if they expose GSMTC.

## Selection policy

A manually selected live session wins even if another player is playing. If that session closes, the service falls back to automatic selection. Automatic selection considers playing sessions before paused or stopped sessions, keeps the previous choice when multiple sessions remain eligible, then considers the user's preferred source, Windows' current session, and a stable source ordering. No player-specific names are hardcoded. Session IDs are runtime identities; source preferences may be persisted by the application.

`IPlayerAdapter` defines the read-only adapter contract. The production Windows implementation implements it. Specialized adapters may be added only against documented, authorized APIs; no placeholder integrations are shipped.

## Timing and event handling

The adapter subscribes to manager session/current-session events and session media-property/playback/timeline events. It refreshes once per second while the selected session is playing and once every five seconds while idle, with events waking it immediately. Media properties are read only when marked dirty. Removed sessions and application shutdown detach event handlers.

Windows documents `Position` as valid at `LastUpdatedTime`, rather than at the time the getter is called. The adapter compensates the age of a new timeline observation and uses a monotonic clock between repeated samples. A stale repeated sample freezes on pause and resumes without counting the paused interval. While continuously playing the same track, a correction smaller than two seconds is reconciled by changing estimated progression speed by at most 15%, avoiding backward jumps from coarse player timestamps. Larger discontinuities, track changes, and pause/stop samples replace the anchor immediately. A genuine seek smaller than two seconds cannot always be distinguished from timestamp quantization and may be reconciled gradually. The UI-independent core then extrapolates from the adapter's as-of-now snapshot.

Missing timelines disable synchronized rendering. Empty titles produce no track. Unreadable session properties are retried; an unreadable session is temporarily treated as stopped. Stale or incorrect positions published by a player cannot always be distinguished from deliberate seeks; per-player offsets and manual validation remain necessary. A position that never changes cannot reveal an unreported seek.

## Reproducible read-only probe

```powershell
dotnet run --project tools/FloatingLyrics.Diagnostics -- --duration 5 --performance
```

Use a longer interval, for example `--duration 60`, while manually exercising a player. The default output includes session counts, playback state, timeline availability, position, and duration. Add `--include-metadata` only if sharing track and application names is intended.

`--performance` measures process CPU time over the observation interval, one-core equivalent CPU percentage, working set, and managed heap. These measurements cover the diagnostic process and media adapter, not WPF rendering. They are a development diagnostic, not a guarantee of application performance. Measure the release application separately while idle, playing, seeking, and switching tracks; record hardware and observation duration with results.

## Manual acceptance checks

Repeat these steps separately for every claimed player and record its version, Windows build, and result:

1. Start with no player; verify the tray/settings report no media without lyric text.
2. Open the player and start a track. Confirm title, artist, album, source, and duration agree with the player.
3. Confirm a real synchronized lyric result is selected, with no unrelated recording.
4. Pause for at least ten seconds; position and lyric line must stop. Resume; progression must continue without including the pause.
5. Seek forward and backward, including within the same line. The active line should change immediately after Windows publishes the update.
6. Skip tracks rapidly; no previous-track lyric result may appear after a new track is selected.
7. Play an instrumental, an unknown song, and a track without a useful timeline; each state must degrade without crashing.
8. Open a second player. Verify playing priority, stable simultaneous playback selection, manual session choice, and fallback after closing that session.
9. Close and reopen the player. Suspend/resume Windows. Verify the service recovers and does not accumulate duplicate events.
10. Test offline operation and provider failure independently from media detection.

Overlay focus, click-through, fullscreen handling, mixed-DPI placement, and monitor removal require the separate overlay acceptance checks. Desktop overlays cannot be guaranteed above exclusive fullscreen games; no graphics hooking is used.

## Primary API references

- [GSMTC session manager and Windows requirements](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager)
- [Timeline properties and LastUpdatedTime semantics](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessiontimelineproperties)
- [Session media, playback, and timeline events](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession)
