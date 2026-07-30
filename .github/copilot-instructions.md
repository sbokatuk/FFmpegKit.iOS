# FFmpegKit.iOS — repository instructions

## Overview

- .NET for iOS / .NET MAUI **binding** for the native FFmpegKit library. One project, `src/FFmpegKit.iOS`, produces all eight packages `FFmpegKit.Net.<Variant>.iOS` (`Audio|Full|FullGpl|Https|HttpsGpl|Min|MinGpl|Video`), selected with the `FFmpegKitBuildType` MSBuild property.
- The natives are **not** built here and **not** committed. `arthenica/ffmpeg-kit` is archived with every release stripped of assets, `ffmpeg-kit-next` is source-only, and `ffmpegkit-maintained/ffmpeg` ships Android `.aar` only — so `build/FetchXcFrameworks.sh` downloads the xcframeworks from **`sk3llo/ffmpeg_kit_flutter`** releases (tag `<version>-<variant>`, e.g. `8.1.2-full-gpl`), the only source still publishing all eight iOS variants, and verifies every archive against that release's `checksums.json`.
- Version = `<ffmpeg version>.<binding revision>` — `FFmpegKitNativeVersion` (8.1.2) + `FFmpegKitBindingRevision` (3) in `Directory.Build.props` → `8.1.2.3`. The first three components are what `FetchXcFrameworks.sh` downloads and what upstream tags. Not comparable with the Android repository's revisions.
- Family: this is the iOS platform layer under `sbokatuk/FFMpegKit.Net`; `sbokatuk/FFmpegKit.Mac` is its mirror image over the same downloads; `sbokatuk/FFmpegKit.Android` binds a *different* native source.

## Build and verify

macOS + Xcode, the .NET 9 **and** 10 SDKs each with the `ios` workload (install each from a directory pinned to that band — see the README), and Python 3.

```sh
./build/FetchXcFrameworks.sh                 # all eight variants, ~1 GB; version from Directory.Build.props
./build/FetchXcFrameworks.sh 8.1.2 Video     # ...or one variant of one line
./build/BuildNugets.sh                       # packs all eight into ./artifacts (net9 pass + net10 pass, then merged)
dotnet test tests/FFmpegKit.iOS.PackageTests
```

Single variant, one band:

```sh
dotnet pack src/FFmpegKit.iOS/FFmpegKit.iOS.csproj -c Release \
    -p:FFmpegKitBuildType=Video -p:FFmpegKitSdkBand=net9 -o artifacts
```

- `FFmpegKitSdkBand` must match the SDK actually running the build: `net9` → `net8.0-ios18.0;net9.0-ios18.0`, `net10` → `net10.0-ios26.0`. `global.json` pins 9.0.100, so a `net10` pack runs from a scratch directory with its own `global.json` (this is what `BuildNugets.sh` does), and `build/merge-packages.py` merges the two passes into one package per variant.
- `ValidateFFmpegKitLibs` fails the build when `src/FFmpegKit.iOS/libs/<Variant>/` is empty — run the fetch script first.

## Layout

- `src/FFmpegKit.iOS/` — `ApiDefinition.cs`, `Structs.cs` (the generated binding), `Additions/` (`FFmpegKit.Async.cs`, `FFprobeKit.Async.cs`, `Ergonomics.cs` — the hand-written C# ergonomics), `libs/<Variant>/` (fetched, untracked).
- `build/` — `FetchXcFrameworks.sh`, `BuildNugets.sh`, `merge-packages.py`, `buildTransitive/Registrar.targets`, `check-upstream.sh` + `upstream.tsv` (what the drift watcher watches).
- `tests/FFmpegKit.iOS.PackageTests`, `tests/FFmpegKit.iOS.DeviceTests`, `samples/FFmpegKit.iOS.Example` (deliberately **not** in `FFmpegKit.sln`), `docs/release-notes/`, `licenses/`, `artifacts/` (local NuGet feed, see `NuGet.config`).

## Conventions

- The root namespace is **`Ffmpegkit.Ios`** and stays that way: a namespace rooted at `FFmpegKit` makes `FFmpegKit.Execute(...)` resolve the namespace instead of the class. `AssemblyName`/`PackageId` are `FFmpegKit.Net.<Variant>.iOS`; each variant gets its own `obj/<Variant>/` and `bin/<Variant>/`.
- The csproj, scripts and workflows are heavily commented with *why* — preserve those comments and add one when you make a non-obvious choice.
- British spelling in prose ("licence"), matching the README; SPDX identifiers and MSBuild property names stay as they are.
- `CompressBindingResourcePackage=true` (avoids NU5123 and Windows `MAX_PATH`) and `CheckEolTargetFramework=false` (net8.0-ios ships deliberately) are intentional.
- Native linkage lives in the `NativeReference` items: `ForceLoad`/`IsCxx` on `ffmpegkit`, frameworks `AudioToolbox AVFoundation CoreMedia VideoToolbox`, linker flags `-lc++ -liconv -lbz2 -lz`. Keep them in step with upstream's integration instructions.
- Regenerating the binding: see `.github/instructions/binding.instructions.md`.

## CI and release flow

- `pr.yml` → `build.yml` (`verify: true`): packs all eight as `<version>-beta.<pr>.<run>`, runs package tests, sample builds and the simulator smoke tests, then publishes the betas to nuget.org. Forked PRs build and test but skip publishing. **Betas cannot be deleted from nuget.org, only unlisted.**
- Merging `docs/release-notes/<version>.md` into `main` **is** the release: `auto-release.yml` tags the merge `v<version>` and dispatches `release.yml`, whose `guard` job proves the commit is an ancestor of the default branch before anything is packed (`verify: false` — the PR already verified it).
- The tag chooses the FFmpeg line: `v7.1.1.1` binds FFmpeg `7.1.1`, `v8.1.2.6` binds `8.1.2`, and a prerelease suffix is ignored. No branch or `Directory.Build.props` edit is needed to release an older line.
- `upstream-drift.yml` runs daily off `build/upstream.tsv`; run it locally with `DRIFT_DIR=/tmp/d ./build/check-upstream.sh`.

## Testing

- Run `dotnet test tests/FFmpegKit.iOS.PackageTests` before any PR that touches packaging or the binding — it asserts per-TFM assemblies, all eight xcframeworks with iOS device+simulator slices only (by *shape*, never by slice name: upstream changed `ios-arm64_arm64e` to `ios-arm64` within 8.1.2), manifest/slice consistency, the licence split and nuspec metadata. Narrow it with `FFMPEGKIT_VARIANTS=Video`.
- Run the simulator smoke tests when touching `NativeReference`s, `Additions/` or the registrar targets — they are the only proof the frameworks link and load:
  ```sh
  ./.github/scripts/run-simulator-tests.sh Video 8.1.2.3 net10.0-ios26.0
  ```
  They consume the packed `.nupkg` from `./artifacts`, so pack first, and pass the version actually packed. **A green simulator is not a green device** — it does not reproduce the device registrar crash (dotnet/macios#22071).

## Hard rules

- Never commit xcframeworks or anything under `src/FFmpegKit.iOS/libs/`. Never skip or weaken the `checksums.json` verification, and never keep the macOS slice — `FetchXcFrameworks.sh` strips it and rewrites each xcframework's `Info.plist` because it is unreachable from a `net*-ios` binding yet embedded once per target framework.
- Never rename the `Ffmpegkit.Ios` namespace, and never root a namespace at `FFmpegKit`.
- Never remove or alter the `buildTransitive` `Registrar.targets` packing without proof on a real device. It defaults consuming apps to `Registrar=dynamic` because .NET 9's managed-static device default crashes with a missing `ObjCRuntime.__Registrar__` (dotnet/macios#22071); `partial-static` — the Mac repository's fix — produces duplicate-symbol link errors on iOS, and NativeAOT apps must choose their own registrar.
- Never let the licence split drift: `-Gpl` variants are `MIT AND GPL-3.0-only` (x264/x265/xvid/vidstab), the rest `MIT AND LGPL-3.0-only`, and both texts ship under `licenses/`. PackageTests enforce it by checking for x264 in each variant's `libavcodec`.
- Never unpin the TFM platform versions (they name `lib/<tfm>/`) and never pack a band with the wrong SDK. Never hand-edit a merged package — fix `build/merge-packages.py`.
- Watch package size: `FullGpl` packs to ~230 MB against nuget.org's 250 MB limit. Anything that grows the payload needs the README's mitigations (drop `net8.0-ios`, or thin the simulator slice) considered first.
- Do not bypass the release `guard` job, and do not publish from a commit that is not in `main`'s history.

## References

- [arthenica/ffmpeg-kit wiki](https://github.com/arthenica/ffmpeg-kit/wiki/iOS) — archived, but still the reference for the Objective-C API these bindings expose.
- [sk3llo/ffmpeg_kit_flutter releases](https://github.com/sk3llo/ffmpeg_kit_flutter/releases) — where the xcframeworks come from.
- Siblings: [FFmpegKit.Android](https://github.com/sbokatuk/FFmpegKit.Android) (different native source), [FFmpegKit.Mac](https://github.com/sbokatuk/FFmpegKit.Mac) (mirror image), [FFMpegKit.Net](https://github.com/sbokatuk/FFMpegKit.Net) (umbrella — re-pin it after releasing here).

Trust these instructions; search the codebase only when something here is incomplete or wrong.
