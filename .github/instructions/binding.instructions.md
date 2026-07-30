---
applyTo: "src/FFmpegKit.iOS/ApiDefinition.cs, src/FFmpegKit.iOS/Structs.cs"
---

# Binding definitions

These two files are **generated**, not written by hand. Regenerate them only when moving to a newer native FFmpegKit version; otherwise edit them surgically and keep the generated shape.

- Generate with Objective Sharpie against an **umbrella header** built from the fetched device slice's public headers (`src/FFmpegKit.iOS/libs/<Variant>/ffmpegkit.xcframework/ios-*/ffmpegkit.framework/Headers`, excluding `fftools` and `ffmpegkit_exception`) — see the README's "Regenerating the binding". Binding `FFmpegKit.h` alone is how the predecessor lost `FFmpegKitConfig`, `FFprobeKit` and the `MediaInformation` types.
- Review and **remove every `[Verify]`** sharpie emits — they fail the build on purpose, and each one is a decision to make, not noise to delete blindly.
- `Level` must be declared `long`, not sharpie's `ulong`: it is `NS_ENUM(NSUInteger, Level)` upstream but has negative members.
- Keep the namespace `Ffmpegkit.Ios`. `ApiDefinition.cs` is `ObjcBindingApiDefinition`, `Structs.cs` is `ObjcBindingCoreSource` — do not move types between them.
- Put C# ergonomics (async wrappers, convenience overloads) in `Additions/`, never in the generated files, so the next regeneration does not discard them.
- Any signature change is consumer-visible: a regeneration once silently turned `session.ReturnCode()`/`State()` into properties and nothing noticed. Pack, run the package tests, and build the sample or the simulator smoke tests before proposing one.
