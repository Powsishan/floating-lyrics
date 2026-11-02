# Floating Lyrics

A native Windows lyrics overlay built with C#, WPF and .NET 10. It reads the current song from Windows media sessions, matches synchronized lyrics with LRCLIB, and displays the current line as transparent, click-through text.

**Status: Phase 1 MVP under validation.** This is a real desktop application with real GSMTC and HTTP integration. Apple Music/Spotify acceptance, mixed-DPI hardware and clean-machine distribution remain release gates. See [feasibility findings](docs/feasibility.md) and [player compatibility](docs/media-compatibility.md). Advanced display modes, automatic fullscreen hiding, local associations and an installer are later phases, not advertised as available.

## Run from source

Requirements: Windows 11 or a supported Windows 10 LTSC/Enterprise installation, and the .NET 10 SDK. The GSMTC API floor is Windows 10 1809. Current Microsoft runtime support does not cover every Windows 10 edition. Visual Studio 2026 with the .NET desktop workload is optional; `dotnet` builds the solution directly.

```powershell
dotnet restore FloatingLyrics.sln
dotnet build FloatingLyrics.sln -c Release --no-restore
dotnet test FloatingLyrics.sln -c Release --no-build
dotnet run --project src/FloatingLyrics.App -c Release --no-build
```

1. Set **LrcLibClientUrl** in Music & lyrics to `https://github.com/Powsishan/floating-lyrics`. It is deliberately unset in this source tree. The app shows a configuration message and makes no LRCLIB request without a valid URL.
2. Play music in an application that publishes Windows media sessions. The app selects a playing session automatically; the source picker permits manual selection and remembers that source as a preference.
3. Use **Retry lyrics** after correcting provider settings or a failed connection. Missing or uncertain matches leave the floating display clear.
4. Unlock the overlay to drag it. Locking removes the editing surface and passes mouse clicks to the application below.
5. Close settings to keep the app running in the tray. Double-click the tray icon to reopen settings; select **Exit** to quit.

Positive timing compensation shows lyrics earlier. Pausing freezes the clock; seeking follows the player's timeline. Players with missing timelines cannot drive synchronized lyrics. The app never invents word timing from line timing.

## Storage and portable operation

Settings and bounded logs normally live in `%LOCALAPPDATA%\FloatingLyrics`. Logs exclude track metadata and lyrics. There is no telemetry, login or API key. Current track title, artist, album and duration are sent to LRCLIB only when its client URL is configured. See [privacy](docs/privacy.md).

Use `FloatingLyrics.exe --portable` to keep settings in a `Data` directory beside the executable, or `--data-dir "D:\a-writable-folder"` to choose a directory. The directory must be writable. Windows startup registration is not included in this MVP.

Create an unpackaged folder build (requires .NET 10 Desktop Runtime on the destination):

```powershell
dotnet publish src/FloatingLyrics.App -c Release --no-self-contained -o artifacts/portable
```

For a self-contained x64 test build:

```powershell
dotnet publish src/FloatingLyrics.App -c Release -r win-x64 --self-contained true -o artifacts/win-x64
```

Copy the whole published directory, not just the `.exe`. No installer or signed production release is supplied yet. Supported Windows and clean-machine testing must precede a release.

## Development

The [architecture and backlog](docs/architecture.md), [development guide](docs/development.md), [provider rules](docs/lyrics-providers.md), and [troubleshooting guide](docs/troubleshooting.md) explain implementation boundaries and limitations. Run the independent media probe:

```powershell
dotnet run --project tools/FloatingLyrics.Diagnostics -- --duration 10
```

Metadata output is opt-in with `--include-metadata`; review it before sharing. Tests use synthetic, original text fixtures. No commercial lyrics, API credentials, artwork or font files are included.

Contributions are welcome under the [MIT license](LICENSE). Read [CONTRIBUTING](CONTRIBUTING.md) and [third-party notices](THIRD-PARTY-NOTICES.md). Project home: [Powsishan/floating-lyrics](https://github.com/Powsishan/floating-lyrics). No automatic update endpoint is configured.
