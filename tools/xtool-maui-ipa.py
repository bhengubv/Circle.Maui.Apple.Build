#!/usr/bin/env python3
"""Check a MAUI iOS device IPA and optionally pass it to xtool."""

import argparse
import plistlib
import shutil
import subprocess
from pathlib import Path, PurePosixPath
from zipfile import BadZipFile, ZipFile


def inspect_ipa(ipa, bundle_id):
    if ipa.suffix.lower() != ".ipa":
        raise ValueError("Expected an .ipa file.")
    with ZipFile(ipa) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)):
            raise ValueError("IPA contains duplicate archive paths.")
        for name in names:
            path = PurePosixPath(name)
            if path.is_absolute() or ".." in path.parts or "\\" in name:
                raise ValueError("IPA contains an unsafe archive path.")
        manifests = []
        for name in names:
            parts = PurePosixPath(name).parts
            if (len(parts) == 3 and parts[0] == "Payload"
                    and parts[1].endswith(".app") and parts[2] == "Info.plist"):
                manifests.append(name)
        if len(manifests) != 1:
            raise ValueError("Expected exactly one main app in Payload.")
        manifest = manifests[0]
        info = plistlib.loads(archive.read(manifest))
        if not isinstance(info, dict):
            raise ValueError("Expected a dictionary in the app's Info.plist.")
        actual_id = info.get("CFBundleIdentifier")
        if actual_id != bundle_id:
            raise ValueError(f"Expected bundle ID {bundle_id}, found {actual_id}.")
        if info.get("CFBundleSupportedPlatforms") != ["iPhoneOS"]:
            raise ValueError("Expected an iPhoneOS device build.")
        executable = info.get("CFBundleExecutable")
        app = manifest.rsplit("/", 1)[0]
        if (not isinstance(executable, str) or executable in {"", ".", ".."}
                or "/" in executable or "\\" in executable):
            raise ValueError("Invalid CFBundleExecutable.")
        executable_path = f"{app}/{executable}"
        if executable_path not in names or archive.getinfo(executable_path).file_size == 0:
            raise ValueError("The app executable is missing or empty.")
        damaged_entry = archive.testzip()
        if damaged_entry is not None:
            raise ValueError(f"IPA failed its archive integrity check at {damaged_entry}.")
        return info


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("ipa", type=Path)
    parser.add_argument("--bundle-id", required=True)
    parser.add_argument("--install-on", metavar="UDID", help="Opt in to xtool development signing and USB installation.")
    args = parser.parse_args()

    try:
        info = inspect_ipa(args.ipa, args.bundle_id)
        version = info.get("CFBundleShortVersionString", "?")
        build = info.get("CFBundleVersion", "?")
        print(f"IPA archive and device metadata passed: {args.bundle_id} {version} ({build})", flush=True)
        if args.install_on:
            xtool = shutil.which("xtool")
            if xtool is None:
                raise ValueError("xtool is missing from PATH. On Windows, run this command in WSL.")
            return subprocess.run(
                [xtool, "install", "--usb", "--udid", args.install_on, str(args.ipa.resolve())],
                check=False,
            ).returncode
    except (ValueError, OSError, BadZipFile, plistlib.InvalidFileException) as exc:
        parser.exit(1, f"error: {exc}\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
