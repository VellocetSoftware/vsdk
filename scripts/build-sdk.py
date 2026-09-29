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
    return metadata


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
        for runtime in RUNTIMES:
            subprocess.run([
                "dotnet", "publish", "src/VSDK/VSDK.csproj", "-c", "Release", "-r", runtime,
                "--self-contained", "true", "-p:DebugType=None", "-o", str(stage / "Launcher" / runtime)
            ], cwd=root, check=True)
            launcher = stage / "Launcher" / runtime / ("VSDK.exe" if runtime == "win-x64" else "VSDK")
            if not launcher.is_file() or not launcher.stat().st_size:
                raise ValueError(f"Missing launcher: {launcher}")
            if runtime.startswith("osx-"):
                launcher.chmod(0o755)
                (stage / "Editor" / runtime / "Vertex.app/Contents/MacOS/Vertex").chmod(0o755)
        for symbol in stage.rglob("*.pdb"):
            symbol.unlink()
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
