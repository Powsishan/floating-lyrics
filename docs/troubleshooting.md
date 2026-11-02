# Troubleshooting

**No media detected.** Start playback in a player that publishes Windows media sessions. Run the diagnostics tool to see whether Windows exposes it. A browser may require its media-session feature or site support. This app cannot create missing metadata/timelines for another player.

**Set up LRCLIB identification.** Set `LrcLibClientUrl` to the project's real public GitHub repository URL in settings. It is unset by design until a public repository exists. Do not insert a placeholder URL. The app can still inspect real media and edit overlay settings without contacting LRCLIB.

**No confident match / only unsynchronized lyrics.** A synchronized version of the exact recording may be absent. Live/remix/remaster editions and timing differences are intentionally screened. No lyrics is a normal state, not an application error.

**Wrong timing.** Pause/resume and seek to check whether the player publishes fresh positions. Adjust timing compensation (positive displays earlier). Players with stale or absent timelines remain a compatibility limitation. A seek is only detectable once Windows publishes it.

**Provider unavailable.** Check connectivity. HTTP throttling honors the server's cooldown; repeatedly pressing retry does not bypass it. The app's log records the error type without the request's track metadata. A timeout never blocks the UI.

**Cannot click or move the text.** Locked mode intentionally passes input through. Unlock via the tray or settings to show the editing surface. Drag it, set width in settings, and lock again. Use Move to bottom center to recover a lost placement. Monitor configuration changes also clamp the overlay to a connected work area.

**Overlay missing.** Enable Show lyrics overlay and always-on-top. Instrumental sections, unavailable lyrics, pre-roll and stopped media intentionally have no floating text. Exclusive fullscreen graphics can cover desktop HWND overlays. Automatic fullscreen hiding is not part of the MVP.

**Settings closed but process remains.** This is the tray lifecycle. Double-click the tray icon to reopen settings; choose Exit to stop. A second launch shows that the app is already running.

**Settings will not save.** Verify permissions for the data folder. Portable mode needs a writable folder beside the executable. Corrupt JSON falls back to defaults and records a warning; saving writes a temporary file and atomically replaces the previous settings.

**Report a bug.** Include Windows build/scales/monitor layout, app version, player version, exact steps and redacted diagnostics. State whether a real player test, automated test, or unverified expectation is involved.
