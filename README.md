# FFmpegKit.iOS

.NET (`net8.0-ios`) binding of the native [FFmpegKit](https://github.com/arthenica/ffmpeg-kit) iOS SDK (FullGpl flavor — includes GPL-licensed codecs such as libx264), consumable from .NET MAUI / net8.0-ios apps via a normal `PackageReference`.

## Project layout

- `FFmpegKit.iOS/` — the binding project (`Microsoft.NET.Sdk`, `IsBindingProject=true`, target `net8.0-ios`).
  - `ApiDefinition.cs` / `Structs.cs` — Objective Sharpie-generated (then hand-trimmed) C# binding of the native Objective-C API.
  - `libs/` — vendored `.xcframework`s (not committed, see below).
- `FFmpegKit.iOS.Example/` — .NET MAUI (iOS) demo app exercising the binding: resize, grayscale, and audio-extraction on a bundled sample video, referencing the locally built nuget directly (see its `nuget.config`).
- `Nugets/FFmpegKit.FullGpl.iOS/` — nuspec + packaged output for the binding.
- `Headers/` — public headers extracted from the vendored xcframework, staged for `sharpie bind` (not committed, regenerate as needed — see below).

## 1. Getting the native libraries

The binding needs 8 `.xcframework`s: `ffmpegkit`, `libavcodec`, `libavdevice`, `libavfilter`, `libavformat`, `libavutil`, `libswresample`, `libswscale`.

Get a prebuilt set (FullGpl/GPL flavor, FFmpeg 6.0) from [`ffmpegkit-maintained/ffmpeg-kit-ios-full`](https://github.com/ffmpegkit-maintained/ffmpeg-kit-ios-full):

```
git clone https://github.com/ffmpegkit-maintained/ffmpeg-kit-ios-full.git
cp -R ffmpeg-kit-ios-full/ffmpeg-kit-ios-full/*.xcframework FFmpegKit.iOS/libs/
```

This is a GPL build (bundles libx264 etc.) — any app you ship it in must be licensed under the GPL as well. If you need a non-GPL/LGPL build instead, either build `arthenica/ffmpeg-kit`/`arthenica/ffmpeg-kit-next` yourself with only non-GPL `--enable-*` libraries, or source a prebuilt LGPL flavor elsewhere; swap the contents of `libs/` and rebuild.

## 2. Building the binding

Requires the `ios` .NET workload:

```
sudo dotnet workload install ios
```

Then:

```
cd FFmpegKit.iOS
dotnet build -c Release
```

The native `.xcframework` resources are packaged as a `FFmpegKit.iOS.resources.zip` alongside `FFmpegKit.iOS.dll` in the build output — both files must ship together (the nuspec below already handles this).

## 3. Packing the NuGet

```
cd Nugets/FFmpegKit.FullGpl.iOS
mkdir -p lib/net8.0-ios18.0
cp ../../FFmpegKit.iOS/bin/Release/net8.0-ios/FFmpegKit.iOS.{dll,pdb,resources.zip} lib/net8.0-ios18.0/
nuget pack FFmpegKit.FullGpl.iOS.nuspec -OutputDirectory .
```

## 4. Updating ApiDefinition.cs / Structs.cs

Only needed when bumping to a newer native FFmpegKit version. Extract the public headers from the vendored xcframework into `Headers/`, then run Objective Sharpie:

```
cp -R FFmpegKit.iOS/libs/ffmpegkit.xcframework/ios-arm64/ffmpegkit.framework/Headers/* Headers/
sharpie bind -output Binding -sdk iphoneos -scope Headers Headers/FFmpegKit.h -c
```

Diff the generated `Binding/ApiDefinitions.cs` / `Binding/StructsAndEnums.cs` against the existing `ApiDefinition.cs` / `Structs.cs` for real signature changes (as opposed to `sharpie`'s cosmetic `[Verify]` suggestions) before merging anything in.

## 5. Running the example app

```
cd FFmpegKit.iOS.Example
dotnet build -f net8.0-ios -t:Run -p:_DeviceName=:v2:udid=<simulator-udid>
```

The example references the nuget straight from `../Nugets/FFmpegKit.FullGpl.iOS` via a local package source in its `nuget.config` — rebuild and repack the nuget (steps 2–3) after any binding change, then rebuild the example.
