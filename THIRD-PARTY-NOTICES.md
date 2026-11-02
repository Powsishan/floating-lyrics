# Third-party notices

The project source is MIT licensed. It requests system fonts without bundling them and uses a Windows system application icon without redistributing an icon asset. No lyric catalog or commercial recording is included.

Runtime direct dependencies:

- Microsoft.Extensions.DependencyInjection 10.0.0 — MIT; https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/10.0.0
- Microsoft.Extensions.Logging 10.0.0 — MIT; https://www.nuget.org/packages/Microsoft.Extensions.Logging/10.0.0
- Microsoft.Extensions.Logging.Abstractions 10.0.0 — MIT; https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions/10.0.0
- .NET, WPF and Windows Forms — MIT source licenses; see the runtime's distributed notices in self-contained builds. Windows APIs depend on the Windows operating system and its license.
- Windows SDK .NET targeting pack — Microsoft.Windows.SDK.NET.Ref 10.0.19041.57. Its NuGet metadata points to the [Windows SDK license](https://aka.ms/WinSDKLicenseURL) and requires license acceptance; this SDK package is **not labeled MIT**. The targeting pack is a build dependency; its redistributable components remain subject to the SDK terms. Review those terms and retain runtime notices when preparing distribution.

Test dependencies (not shipped with the application): xunit.v3 3.2.2 and xunit.runner.visualstudio 3.1.1 (Apache-2.0), Microsoft.NET.Test.Sdk 17.14.1 (MIT). Transitive test runner components carry their own package notices.

Before shipping, inventory all resolved direct/transitive packages and include their actual license and notice files with the distribution. Preserve the `LICENSE.txt`/`THIRD-PARTY-NOTICES.txt` files in any self-contained runtime. This inventory is development documentation, not a substitute for those license texts.

LRCLIB is an external service, not bundled software. Provider API access and lyric copyright are separate. See docs/lyrics-providers.md and the current https://lrclib.net/docs.txt requirements.
