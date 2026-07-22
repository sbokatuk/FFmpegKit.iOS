#!/usr/bin/env python3
"""Print the FFmpegKit wrapper version embedded in a packed FFmpegKit.Net.*.iOS package.

Two different things are versioned in these packages. The number they are released under is the
FFmpeg version, because that is how upstream tags its builds. The FFmpegKit Objective-C wrapper
around it has a version of its own, and the two are unrelated - FFmpeg 8.1.2 currently ships
wrapped by FFmpegKit 6.0.

The release notes state both, so this reads the second one out of the artifact being published
rather than repeating it in prose, where it would quietly go stale the moment upstream rebuilds
against a newer wrapper.

Usage: wrapper-version.py [ARTIFACTS_DIR]        # defaults to ./artifacts
"""

from __future__ import annotations

import glob
import io
import os
import plistlib
import sys
import zipfile


def wrapper_version(package: str) -> str:
    """Read CFBundleShortVersionString from the ffmpegkit framework inside a package."""
    with zipfile.ZipFile(package) as outer:
        payloads = sorted(n for n in outer.namelist() if n.endswith(".resources.zip"))
        if not payloads:
            raise LookupError(f"{package} has no binding resource package")

        # Every target framework carries the same payload, so the first one is representative.
        with zipfile.ZipFile(io.BytesIO(outer.read(payloads[0]))) as inner:
            manifests = sorted(
                n for n in inner.namelist() if n.endswith("ffmpegkit.framework/Info.plist")
            )
            if not manifests:
                raise LookupError(f"{package} has no ffmpegkit.framework/Info.plist")

            plist = plistlib.loads(inner.read(manifests[0]))

    version = plist.get("CFBundleShortVersionString")
    if not version:
        raise LookupError(f"{package} declares no CFBundleShortVersionString")

    return str(version)


def main(argv: list[str]) -> int:
    directory = argv[1] if len(argv) > 1 else "artifacts"

    packages = sorted(
        p
        for p in glob.glob(os.path.join(directory, "FFmpegKit.Net.*.nupkg"))
        if not p.endswith(".snupkg")
    )
    if not packages:
        print(f"error: no packages found in {directory}", file=sys.stderr)
        return 1

    # Read one, then confirm the rest agree: a mismatch would mean variants were built from
    # different upstream drops, which is worth failing over rather than papering across.
    versions = {}
    for package in packages:
        try:
            versions.setdefault(wrapper_version(package), []).append(os.path.basename(package))
        except (LookupError, zipfile.BadZipFile) as error:
            print(f"error: {error}", file=sys.stderr)
            return 1

    if len(versions) > 1:
        print("error: packages disagree on the FFmpegKit wrapper version:", file=sys.stderr)
        for version, names in sorted(versions.items()):
            print(f"  {version}: {', '.join(names)}", file=sys.stderr)
        return 1

    print(next(iter(versions)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
