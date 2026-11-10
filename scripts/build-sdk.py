#!/usr/bin/env python3
"""Compose VSDK and the matching Vertex editors into one reusable SDK toolkit."""
import argparse
import json
from pathlib import Path
import plistlib
import shutil
import subprocess
import tempfile

RUNTIMES = ("win-x64", "osx-arm64", "osx-x64")


def validate_vertex(root):
    metadata = json.loads((root / "vertex-build.json").read_text(encoding="utf-8"))
    if set(metadata["runtimes"]) != set(RUNTIMES) or not metadata.get("revision"):
        raise ValueError("Vertex must provide one matching build for every SDK platform.")
    for runtime in RUNTIMES:
        editor = root / "Editor" / runtime
        executable = editor / ("Vertex.exe" if runtime == "win-x64" else "Vertex.app/Contents/MacOS/Vertex")
        if not executable.is_file() or not executable.stat().st_size:
            raise ValueError(f"Missing Vertex executable: {executable}")
        if runtime.startswith("osx-"):
            contents = editor / "Vertex.app/Contents"
            info = plistlib.loads((contents / "Info.plist").read_bytes())
            if (info.get("CFBundleIdentifier") != "com.vellocet.vertex" or info.get("CFBundleExecutable") != "Vertex"
                    or info.get("CFBundleIconFile") != "Vertex.icns" or not (contents / "Resources/Vertex.icns").is_file()):
                raise ValueError(f"Missing Vertex application identity or icon: {editor}")
    if any(path.is_symlink() for path in (root / "Editor").rglob("*")):
        raise ValueError("Vertex SDK inputs must not contain symbolic links.")
    for name in ("Vertex.Core.dll", "Vertex.Contracts.dll", "Vertex.VSig.dll", "Vertex.Engine.Unity.dll"):
        library = root / "Libraries" / name
        if not library.is_file() or not library.stat().st_size or library.is_symlink():
            raise ValueError(f"Missing or linked Vertex library: {library}")
    return metadata


def publish_launcher(root, destination, runtime):
    windows = runtime == "win-x64"
    application = destination / "VSDK.app"
    publish = destination if windows else application / "Contents/MacOS"
    subprocess.run([
        "dotnet", "publish", "src/VSDK/VSDK.csproj", "-c", "Release", "-r", runtime,
        "--self-contained", "true", "-p:DebugType=None", "-p:PublishSingleFile=true", "-o", str(publish)
    ], cwd=root, check=True)
    for symbol in publish.rglob("*.pdb"):
        symbol.unlink()
    executable = publish / ("VSDK.exe" if windows else "VSDK")
    assert executable.is_file() and executable.stat().st_size > 1_000_000, "Expected a bundled VSDK application."
    if windows:
        assert {path.name for path in publish.iterdir()} == {"VSDK.exe"}, "Loose launcher dependencies remain."
        return
    assert not list(publish.rglob("*.dll")), "Managed launcher assemblies must be embedded in the executable."
    for name in ("libAvaloniaNative.dylib", "libSkiaSharp.dylib", "libHarfBuzzSharp.dylib"):
        assert (publish / name).is_file() and (publish / name).stat().st_size, f"Missing {name}"
    executable.chmod(0o755)
    resources = application / "Contents/Resources"
    resources.mkdir()
    shutil.copy2(root / "src/VSDK/Assets/vellocet-logo.icns", resources / "VSDK.icns")
    with (application / "Contents/Info.plist").open("wb") as output:
        plistlib.dump({
            "CFBundleIdentifier": "com.vellocet.vsdk", "CFBundleName": "VSDK",
            "CFBundleDisplayName": "VSDK", "CFBundleExecutable": "VSDK",
            "CFBundlePackageType": "APPL", "CFBundleIconFile": "VSDK.icns",
            "CFBundleShortVersionString": "1.0.0", "CFBundleVersion": "1.0.0",
            "NSHighResolutionCapable": True, "NSPrincipalClass": "NSApplication"
        }, output)
    subprocess.run(["codesign", "--force", "--deep", "--sign", "-", str(application)], check=True)
    subprocess.run(["codesign", "--verify", "--deep", "--strict", str(application)], check=True)


def main():
    root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--vertex", required=True, type=Path, help="Vertex SDK inputs containing Editor/ and vertex-build.json")
    args = parser.parse_args()
    vertex = validate_vertex(args.vertex)
    artifacts = root / "artifacts"
    artifacts.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="sdk-", dir=artifacts) as temporary:
        stage = Path(temporary) / "tools"
        shutil.copytree(args.vertex / "Editor", stage / "Editor")
        shutil.copytree(args.vertex / "Libraries", stage / "Libraries")
        for runtime in RUNTIMES:
            publish_launcher(root, stage / "Launcher" / runtime, runtime)
            if runtime.startswith("osx-"):
                (stage / "Editor" / runtime / "Vertex.app/Contents/MacOS/Vertex").chmod(0o755)
        revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip()
        (stage / "toolchain.json").write_text(json.dumps({
            "vsdk": revision, "vertex": vertex["revision"], "runtimes": RUNTIMES,
            "dirty": vertex.get("dirty", False) or bool(subprocess.check_output(
                ["git", "status", "--porcelain"], cwd=root, text=True))
        }, indent=2) + "\n", encoding="utf-8")
        shutil.copy2(root / "LICENSE.txt", stage / "LICENSE.txt")
        destination = artifacts / "tools"
        previous = Path(temporary) / "previous"
        if destination.exists():
            destination.rename(previous)
        try:
            stage.rename(destination)
        except OSError:
            if previous.exists():
                previous.rename(destination)
            raise
    print(f"SDK toolkit ready: {destination}")


if __name__ == "__main__":
    main()
