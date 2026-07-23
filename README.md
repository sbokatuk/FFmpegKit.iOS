# FFmpegKit.iOS

[![NuGet](https://img.shields.io/nuget/v/FFmpegKit.Net.Video.iOS?label=nuget)](https://www.nuget.org/packages/FFmpegKit.Net.Video.iOS)
[![release](https://github.com/sbokatuk/FFmpegKit.iOS/actions/workflows/release.yml/badge.svg)](https://github.com/sbokatuk/FFmpegKit.iOS/actions/workflows/release.yml)
[![Targets: net8.0 | net9.0 | net10.0](https://img.shields.io/badge/targets-net8.0%20%7C%20net9.0%20%7C%20net10.0-512BD4)](#packages)
[![ffmpeg 8.1.2](https://img.shields.io/badge/ffmpeg-8.1.2-632CA6)](#about)
[![Licence: MIT AND LGPL-3.0 or GPL-3.0](https://img.shields.io/badge/licence-MIT%20AND%20LGPL--3.0%20or%20GPL--3.0-orange)](#licence)

.NET for iOS and .NET MAUI bindings for the native **FFmpegKit** library.

> GitHub reports this repository as MIT because that is what it contains: binding source only, no native binaries. **The published packages are not MIT** — they embed native FFmpeg builds and are additionally covered by LGPL-3.0, or GPL-3.0 for the `-gpl` variants. See [License](#license).

Built against the prebuilt Apple binaries from **[sk3llo/ffmpeg_kit_flutter](https://github.com/sk3llo/ffmpeg_kit_flutter)** — see [Where the native binaries come from](#where-the-native-binaries-come-from) for why that fork and not the original.

## About

This repository contains .NET bindings on top of the FFmpegKit `.xcframework` build for iOS. One project, `src/FFmpegKit.iOS`, produces all eight package variants — the variant is selected with the `FFmpegKitBuildType` MSBuild property.

Packages target `net8.0-ios18.0`, `net9.0-ios18.0` and `net10.0-ios26.0`.

Each .NET SDK's iOS workload supports only two target frameworks — the .NET 9 band ships `net8` and `net9`, the .NET 10 band ships `net9` and `net10` — so no single `dotnet pack` can produce all three. [`BuildNugets.sh`](build/BuildNugets.sh) packs once per band and [`build/merge-packages.py`](build/merge-packages.py) merges the `lib/` trees and nuspec dependency groups into one package per variant.

### Where the native binaries come from

FFmpegKit has several relevant repositories, and only one of them still ships usable **iOS** binaries:

| Repository | State | Prebuilt iOS `.xcframework` |
| --- | --- | --- |
| [`arthenica/ffmpeg-kit`](https://github.com/arthenica/ffmpeg-kit) | archived | none — every release now carries **zero assets** |
| [`arthenica/ffmpeg-kit-next`](https://github.com/arthenica/ffmpeg-kit-next) | active, the official continuation | none — source only |
| [`ffmpegkit-maintained/ffmpeg`](https://github.com/ffmpegkit-maintained/ffmpeg) | active community fork | none — **Android `.aar` only**, nothing for Apple platforms |
| [`ffmpegkit-maintained/ffmpeg-kit-ios-full`](https://github.com/ffmpegkit-maintained/ffmpeg-kit-ios-full) | stale | yes, but FFmpeg **6.0**, `full-gpl` only, no releases or tags to pin |
| [`sk3llo/ffmpeg_kit_flutter`](https://github.com/sk3llo/ffmpeg_kit_flutter) | active | **yes** — all eight variants, currently `8.1.2` |

Note that this is *not* the same source as the Android bindings use: [`FFmpegKit.Android`](https://github.com/sbokatuk/FFmpegKit.Android) takes its `.aar` files from `ffmpegkit-maintained/ffmpeg` via Maven Central, and that fork does not build for Apple platforms.

Releases there are tagged `<version>-<variant>` and carry each xcframework as a separate zip plus a `checksums.json`. [`FetchXcFrameworks.sh`](build/FetchXcFrameworks.sh) downloads all eight and verifies every one against that manifest — these are tens of megabytes of native code that gets linked into your app, so a truncated or substituted archive fails the build rather than shipping.

The version is set by `FFmpegKitNativeVersion` in [`Directory.Build.props`](Directory.Build.props), which `FetchXcFrameworks.sh` reads, so the download and the frameworks the project expects cannot drift apart.

The fork currently publishes four FFmpeg lines, each with all eight variants:

| FFmpeg |
| --- |
| `7.1.1` |
| `8.0.0` |
| `8.1.1` |
| `8.1.2` |

Each carries a device slice and a simulator slice. The exact architectures are upstream's to decide and have changed within a version: `8.1.2` shipped a device slice of `arm64 + arm64e` and was later rebuilt as `arm64` alone. The package tests therefore assert the *shape* — one device slice, one simulator slice, both iOS, no macOS — rather than specific names.

Upstream also ships a macOS slice in each xcframework. It is stripped on download: it cannot be reached from a `net*-ios` binding, but it would still be embedded in the package once per target framework. Keeping it would push the `FullGpl` package past nuget.org's 250 MB limit. If you need macOS or Mac Catalyst, note that **no Mac Catalyst slice is published at all**, so that would need a different source.

### Versioning

Package versions are **`<ffmpeg version>.<binding revision>`**:

```
8.1.2.1
└───┬─┘ └┬┘
    │    └─ binding revision — this repository
    └────── FFmpeg version — the native build inside the package
```

The first three components name the FFmpeg build the package contains, which is also the version [`FetchXcFrameworks.sh`](build/FetchXcFrameworks.sh) downloads and what upstream tags its releases with. The fourth belongs to this repository and increments whenever the bindings or packaging change while the native binaries stay put — `8.1.2.1` and `8.1.2.2` are the same FFmpeg with different bindings.

A floating range such as `8.1.2.*` therefore always resolves to the newest bindings for that exact FFmpeg build and never crosses onto another one. Pin an exact version instead if you would rather approve every binding update yourself.

> The [Android bindings](https://github.com/sbokatuk/FFmpegKit.Android) use the same scheme, and currently track the same FFmpeg line — `8.1.2` on both. The **binding revisions advance independently**, so the fourth component will differ between the two. They also wrap different upstream FFmpegKit builds, so the APIs are not identical.

### Releasing an older line

The tag selects the FFmpeg line: the first three components of **`v7.1.1.1`** are the FFmpeg version to build against, so that tag binds FFmpeg `7.1.1` and publishes `7.1.1.1` packages. The fourth component is the binding revision and does not affect which native build is fetched (`v8.1.2.6` → FFmpeg `8.1.2`), and a prerelease suffix is ignored too (`v8.1.2.1-beta.1` → FFmpeg `8.1.2`). No branch or `Directory.Build.props` edit is needed.

Locally, pass the native version as the second argument:

```sh
./build/FetchXcFrameworks.sh 7.1.1     # fetch that line's xcframeworks
./build/BuildNugets.sh 7.1.1 7.1.1     # package version, native version
```

## License

> This section describes what the upstream project states. It is not legal advice — if the distinction matters for your product, get it reviewed.

The C# binding code in this repository is [MIT](LICENSE). **The published NuGet packages are not**, because each one embeds native FFmpeg binaries that carry their own copyleft terms. Each package therefore declares `MIT AND <native license>`:

| Package | Native license | SPDX expression |
| --- | --- | --- |
| `FFmpegKit.Net.Audio.iOS` | LGPL-3.0 | `MIT AND LGPL-3.0-only` |
| `FFmpegKit.Net.Full.iOS` | LGPL-3.0 | `MIT AND LGPL-3.0-only` |
| `FFmpegKit.Net.Https.iOS` | LGPL-3.0 | `MIT AND LGPL-3.0-only` |
| `FFmpegKit.Net.Min.iOS` | LGPL-3.0 | `MIT AND LGPL-3.0-only` |
| `FFmpegKit.Net.Video.iOS` | LGPL-3.0 | `MIT AND LGPL-3.0-only` |
| `FFmpegKit.Net.FullGpl.iOS` | **GPL-3.0** | `MIT AND GPL-3.0-only` |
| `FFmpegKit.Net.HttpsGpl.iOS` | **GPL-3.0** | `MIT AND GPL-3.0-only` |
| `FFmpegKit.Net.MinGpl.iOS` | **GPL-3.0** | `MIT AND GPL-3.0-only` |

The `-gpl` variants enable `x264`, `x265`, `xvid` and `vidstab`, which are GPL — upstream keeps them as separate artifacts specifically so they never contaminate the LGPL ones. Upstream's guidance is direct: **if your app is closed-source, use a non-GPL variant.**

Upstream states version 3.0 with no "or later" wording, hence the `-only` SPDX identifiers.

Every package ships the texts it is covered by under `licenses/` — `LICENSE` (MIT, the bindings) and `LGPL-3.0.txt` or `GPL-3.0.txt` (the native binaries). The same texts are in this repository under [`licenses/`](licenses).

The package tests assert this rather than trusting it: every variant is checked for the presence or absence of x264 in its `libavcodec`, so a GPL build packed under an LGPL licence expression fails the build.

## Installation

Install the package via NuGet. There are various packages depending on what you plan to use and if you require a GPL compatible package or not. These package variants match the different variants built in the FFmpegKit repository. The `-gpl` variants are GPL-3.0 — see [License](#license) before choosing one.

| Package | Link |
|------------|-----|
| FFmpegKit.Net.Audio.iOS | [![NuGet](https://img.shields.io/nuget/v/FFmpegKit.Net.Audio.iOS.svg?label=NuGet)](https://www.nuget.org/packages/FFmpegKit.Net.Audio.iOS) |
| FFmpegKit.Net.Full.iOS | [![NuGet](https://img.shields.io/nuget/v/FFmpegKit.Net.Full.iOS.svg?label=NuGet)](https://www.nuget.org/packages/FFmpegKit.Net.Full.iOS) |
| FFmpegKit.Net.FullGpl.iOS | [![NuGet](https://img.shields.io/nuget/v/FFmpegKit.Net.FullGpl.iOS.svg?label=NuGet)](https://www.nuget.org/packages/FFmpegKit.Net.FullGpl.iOS) |
| FFmpegKit.Net.Https.iOS | [![NuGet](https://img.shields.io/nuget/v/FFmpegKit.Net.Https.iOS.svg?label=NuGet)](https://www.nuget.org/packages/FFmpegKit.Net.Https.iOS) |
| FFmpegKit.Net.HttpsGpl.iOS | [![NuGet](https://img.shields.io/nuget/v/FFmpegKit.Net.HttpsGpl.iOS.svg?label=NuGet)](https://www.nuget.org/packages/FFmpegKit.Net.HttpsGpl.iOS) |
| FFmpegKit.Net.Min.iOS | [![NuGet](https://img.shields.io/nuget/v/FFmpegKit.Net.Min.iOS.svg?label=NuGet)](https://www.nuget.org/packages/FFmpegKit.Net.Min.iOS) |
| FFmpegKit.Net.MinGpl.iOS | [![NuGet](https://img.shields.io/nuget/v/FFmpegKit.Net.MinGpl.iOS.svg?label=NuGet)](https://www.nuget.org/packages/FFmpegKit.Net.MinGpl.iOS) |
| FFmpegKit.Net.Video.iOS | [![NuGet](https://img.shields.io/nuget/v/FFmpegKit.Net.Video.iOS.svg?label=NuGet)](https://www.nuget.org/packages/FFmpegKit.Net.Video.iOS) |

A package version is its FFmpeg version plus a binding revision — see [Versioning](#versioning). `8.1.2.*` floats to the newest bindings for FFmpeg `8.1.2` without ever crossing onto another FFmpeg build.

### Migrating from `FFmpegKit.FullGpl.iOS` / `FFmpegKit.Video.iOS`

These packages replace the older `FFmpegKit.<Variant>.iOS` ones. Change the package id:

```diff
-<PackageReference Include="FFmpegKit.FullGpl.iOS" Version="4.5.1-beta2" />
+<PackageReference Include="FFmpegKit.Net.FullGpl.iOS" Version="8.1.2" />
```

**The `Ffmpegkit.Ios` namespace is unchanged**, so your `using` directives and calls stay as they are. It deliberately does not follow the package name: a namespace rooted at `FFmpegKit` containing a type also called `FFmpegKit` makes `FFmpegKit.Execute(...)` resolve the namespace instead of the class and fail to compile.

The assembly is now `FFmpegKit.Net.<Variant>.iOS`, which matters only if you reference it by assembly name or use reflection.

Two things to be aware of when upgrading:

- The old packages declared the version as `4.5.1` but actually contained an FFmpeg **6.0** build, and declared their licence as `MIT` while shipping GPL binaries. Both are corrected here — see [License](#license). Your obligations have not changed; the metadata was simply wrong.
- The bound API surface is much larger now. The previous binding was generated from `FFmpegKit.h` alone, which does not transitively include most of the API, so `FFmpegKitConfig`, `FFprobeKit`, `MediaInformation` and friends were missing entirely.

## Usage

Include the `Ffmpegkit.Ios` namespace:

```c#
using Ffmpegkit.Ios;
```

Execute your FFmpeg command:

```c#
var session = await FFmpegKit.ExecuteAsync("-i input.mov -c:v libx264 output.mp4");

if (session.Succeeded())
    Console.WriteLine("done");
```

`ExecuteAsync` wraps FFmpegKit's own asynchronous path, so nothing blocks the calling thread. Pass a `CancellationToken` to stop a running command — the session then completes with a cancelled return code rather than throwing. A synchronous `FFmpegKit.Execute` is also bound, but it blocks for the whole transcode, which on the UI thread means a frozen app.

Probing works the same way:

```c#
var probe = await FFprobeKit.GetMediaInformationAsync(path);
Console.WriteLine(probe.MediaInformation?.Format);
```

More examples and usage can be found in the [original FFmpegKit wiki](https://github.com/arthenica/ffmpeg-kit/wiki/iOS). That repository is archived, but the Objective-C API it documents is the one these bindings expose, so it remains the reference.

## Building

### Prerequisites

macOS with Xcode, and the .NET 9 and 10 SDKs each with the iOS workload installed — every band supplies a different reference pack. The SDK is chosen by the `global.json` in the *working directory*, so install each band from a directory pinned to it:

```sh
for major in 9 10; do
  dir=$(mktemp -d) && cd "$dir"
  dotnet new globaljson --sdk-version "$(dotnet --list-sdks | grep "^${major}\." | tail -1 | cut -d' ' -f1)" --force
  dotnet workload install ios
done
```

Python 3 is also needed, for the xcframework slice stripping and the package merge step.

### All variants

```sh
./build/FetchXcFrameworks.sh          # downloads the xcframeworks (~1 GB for all eight)
./build/BuildNugets.sh                # packs all 8 variants into ./artifacts
./build/BuildNugets.sh 8.1.2-rc.1     # ...or with an explicit package version
```

`FetchXcFrameworks.sh` reads the FFmpegKit version from `FFmpegKitNativeVersion` in `Directory.Build.props`, the same property the `.csproj` uses to locate the frameworks, so the two cannot drift apart. Pass a version to override it, and a variant to fetch just one:

```sh
./build/FetchXcFrameworks.sh 8.1.2 Video
```

### A single variant

```sh
# net8 + net9 assets (.NET 9 SDK, per global.json)
dotnet pack src/FFmpegKit.iOS/FFmpegKit.iOS.csproj \
    -c Release -p:FFmpegKitBuildType=Video -p:FFmpegKitSdkBand=net9 -o artifacts
```

`FFmpegKitBuildType` is one of `Audio`, `Full`, `FullGpl`, `Https`, `HttpsGpl`, `Min`, `MinGpl`, `Video`. `FFmpegKitSdkBand` is `net9` or `net10` and must match the SDK actually running the build. Each variant builds into its own `obj/` and `bin/` subdirectory, so they can be built in sequence without interfering with each other.

### Regenerating the binding

Only needed when bumping to a newer native FFmpegKit version. The binding is generated with [Objective Sharpie](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/maui/) from the vendored frameworks' public headers:

```sh
# Stage the public headers from the device slice
mkdir -p Headers
cp -R src/FFmpegKit.iOS/libs/Video/ffmpegkit.xcframework/ios-arm64_arm64e/ffmpegkit.framework/Headers/* Headers/

# Sharpie must be pointed at an umbrella header. FFmpegKit.h alone only pulls in a fraction of
# the API - binding just that is how the previous binding ended up missing FFmpegKitConfig,
# FFprobeKit and the MediaInformation types.
ls Headers/*.h | grep -v fftools | grep -v ffmpegkit_exception \
  | sed 's|Headers/|#import "|; s|$|"|' > Headers/FFmpegKitUmbrella.h

sharpie bind -output Binding -sdk iphoneos26.5 -scope Headers Headers/FFmpegKitUmbrella.h -c -I Headers
```

Then reconcile `Binding/ApiDefinitions.cs` and `Binding/StructsAndEnums.cs` into `src/FFmpegKit.iOS/ApiDefinition.cs` and `src/FFmpegKit.iOS/Structs.cs`. Every `[Verify]` attribute sharpie emits must be reviewed and removed — they intentionally cause build failures. Note that sharpie emits the `Level` enum as `ulong` despite its negative members; it has to be `long`.

## Tests

**Package tests** run anywhere and inspect the packed `.nupkg` files — assembly present for every target framework, all eight xcframeworks with iOS device and simulator slices only, manifests consistent with the slices actually shipped, the GPL/LGPL split matching what the binaries contain, and nuspec metadata:

```sh
./build/BuildNugets.sh                       # produce ./artifacts first
dotnet test tests/FFmpegKit.iOS.PackageTests
FFMPEGKIT_VARIANTS=Video dotnet test tests/FFmpegKit.iOS.PackageTests   # ...or just one variant
```

**Simulator smoke tests** build an app against the packed package and run real FFmpeg commands on a booted simulator, which is the only way to prove the native frameworks actually link and load:

```sh
./.github/scripts/run-simulator-tests.sh Video 8.1.2 net10.0-ios26.0
```

## CI

| Workflow | Trigger | What it does |
| --- | --- | --- |
| [`pr.yml`](.github/workflows/pr.yml) | pull request | Builds and packs all 8 variants as `<version>-beta.<pr>.<run>`, runs package tests and the simulator smoke test, then publishes the betas to nuget.org. Forked PRs build and test but skip publishing, since they cannot read secrets. |
| [`release.yml`](.github/workflows/release.yml) | tag `v*` | Same build and tests at the tag's version, publishes to nuget.org, then creates a GitHub release with the changelog since the previous tag and links to every package. |

Both call the reusable [`build.yml`](.github/workflows/build.yml), which runs on macOS — iOS builds have no cross-platform path, unlike the Android bindings.

### Publishing credentials

Publishing uses [nuget.org Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) — no long-lived API key. Each publish job requests a GitHub OIDC token (`id-token: write`), exchanges it via `NuGet/login@v1` for an API key valid for one hour, and pushes with that.

Setup on nuget.org (**Account → Trusted Publishing**): a policy binds to exactly **one** workflow file, so this repository needs **two**, identical apart from the workflow file name:

| Field | Value |
| --- | --- |
| Package Owner | `s.bokatuk` |
| Repository Owner | `sbokatuk` |
| Repository | `FFmpegKit.iOS` — the name only, not a URL |
| Workflow File | `pr.yml` for one policy, `release.yml` for the other |
| Environment | `nuget.org` — must match `environment:` on the publish job |

Set a `NUGET_USER` secret if the nuget.org profile name ever changes.

Note that prereleases pushed to nuget.org cannot be deleted, only unlisted — every pull request push publishes eight packages.

### Package size

The `-gpl` variants are large: `FullGpl` packs to roughly 230 MB against nuget.org's **250 MB** limit, because the native payload is embedded once per target framework. If a future FFmpegKit release grows the binaries enough to cross that line, the options are to drop the (end-of-life) `net8.0-ios` target, or to thin the simulator slice to `arm64` only — which costs iOS Simulator support for anyone still on an Intel Mac.
