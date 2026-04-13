#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Build a combined Steam Tool bundle with:
- Shared SDK payload (SDKContent + SDKPackage) copied once
- Platform launchers in Launcher/win-x64, Launcher/osx-arm64, Launcher/osx-x64

Usage:
  ./scripts/build-steam-tool.sh [options]

Options:
  --sdk-source <path>        Source directory containing SDKContent and SDKPackage.
                             Default: auto-detects ../warlock/Build relatives.
  --output <path>            Output directory for the combined Steam bundle.
                             Default: Build/SteamTool (under this solution root)
  --configuration <name>     dotnet configuration (default: Release)
  --framework-dependent      Publish framework-dependent instead of self-contained
  --help                     Show this help
USAGE
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_FILE="$SOLUTION_ROOT/VSDK/VSDK.csproj"
SOLUTION_FILE="$SOLUTION_ROOT/VSDK.sln"

CONFIGURATION="Release"
SELF_CONTAINED="true"
OUTPUT_ROOT="$SOLUTION_ROOT/Build/SteamTool"
SDK_SOURCE_ROOT=""
RIDS=("win-x64" "osx-arm64" "osx-x64")

while [[ $# -gt 0 ]]; do
  case "$1" in
    --sdk-source)
      SDK_SOURCE_ROOT="${2:-}"
      shift 2
      ;;
    --output)
      OUTPUT_ROOT="${2:-}"
      shift 2
      ;;
    --configuration)
      CONFIGURATION="${2:-}"
      shift 2
      ;;
    --framework-dependent)
      SELF_CONTAINED="false"
      shift
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

detect_sdk_source() {
  local -a candidates=(
    "$SOLUTION_ROOT/../../warlock/Build"
    "$SOLUTION_ROOT/../warlock/Build"
    "$(pwd)/Build"
  )

  local candidate
  for candidate in "${candidates[@]}"; do
    if [[ -d "$candidate/SDKContent" && -d "$candidate/SDKPackage" ]]; then
      echo "$candidate"
      return 0
    fi
  done

  return 1
}

require_dir() {
  local path="$1"
  if [[ ! -d "$path" ]]; then
    echo "Required directory not found: $path" >&2
    exit 1
  fi
}

copy_tree() {
  local src="$1"
  local dst="$2"

  rm -rf "$dst"
  mkdir -p "$(dirname "$dst")"
  cp -R "$src" "$dst"
}

if [[ -z "$SDK_SOURCE_ROOT" ]]; then
  if ! SDK_SOURCE_ROOT="$(detect_sdk_source)"; then
    echo "Could not auto-detect SDK source root." >&2
    echo "Pass --sdk-source <path> where SDKContent and SDKPackage exist." >&2
    exit 1
  fi
fi

SDK_SOURCE_ROOT="$(cd "$SDK_SOURCE_ROOT" && pwd)"
OUTPUT_ROOT="$(mkdir -p "$OUTPUT_ROOT" && cd "$OUTPUT_ROOT" && pwd)"

require_dir "$SDK_SOURCE_ROOT/SDKContent"
require_dir "$SDK_SOURCE_ROOT/SDKPackage"
require_dir "$SOLUTION_ROOT/VSDK"

echo "==> Solution root: $SOLUTION_ROOT"
echo "==> SDK source:    $SDK_SOURCE_ROOT"
echo "==> Output root:   $OUTPUT_ROOT"
echo "==> Configuration: $CONFIGURATION"
echo "==> Self-contained: $SELF_CONTAINED"
echo

echo "==> Cleaning output root"
rm -rf "$OUTPUT_ROOT"
mkdir -p "$OUTPUT_ROOT/Launcher"

echo "==> Restoring solution"
dotnet restore "$SOLUTION_FILE" --nologo

for rid in "${RIDS[@]}"; do
  echo "==> Publishing launcher for $rid"
  dotnet publish "$PROJECT_FILE" \
    -c "$CONFIGURATION" \
    -r "$rid" \
    --self-contained "$SELF_CONTAINED" \
    -p:PublishSingleFile=false \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    --nologo

  publish_dir="$SOLUTION_ROOT/VSDK/bin/$CONFIGURATION/net10.0/$rid/publish"
  require_dir "$publish_dir"
  copy_tree "$publish_dir" "$OUTPUT_ROOT/Launcher/$rid"

  # Keep shipped output clean.
  find "$OUTPUT_ROOT/Launcher/$rid" -type f -name '*.pdb' -delete
done

echo "==> Copying shared SDK payload"
copy_tree "$SDK_SOURCE_ROOT/SDKContent" "$OUTPUT_ROOT/SDKContent"
copy_tree "$SDK_SOURCE_ROOT/SDKPackage" "$OUTPUT_ROOT/SDKPackage"

if [[ -d "$SDK_SOURCE_ROOT/Docs" ]]; then
  copy_tree "$SDK_SOURCE_ROOT/Docs" "$OUTPUT_ROOT/Docs"
fi

if [[ -f "$SDK_SOURCE_ROOT/README.txt" ]]; then
  cp "$SDK_SOURCE_ROOT/README.txt" "$OUTPUT_ROOT/README.txt"
elif [[ -f "$SDK_SOURCE_ROOT/README.md" ]]; then
  cp "$SDK_SOURCE_ROOT/README.md" "$OUTPUT_ROOT/README.md"
fi

cat > "$OUTPUT_ROOT/STEAM_NOTES.txt" <<EOF
Combined Steam Tool layout generated on $(date -u +"%Y-%m-%dT%H:%M:%SZ")

Launch executables:
- Windows: Launcher/win-x64/VSDK.exe
- macOS:   Launcher/osx-arm64/VSDK
- macOS:   Launcher/osx-x64/VSDK

Shared SDK payload (single copy):
- SDKPackage/
- SDKContent/

The launcher auto-detects SDK payload by walking parent directories, so this
shared layout works for both platform launchers.
EOF

echo
echo "==> Build complete"
echo "Output: $OUTPUT_ROOT"
echo
echo "Expected launchers:"
echo "  Windows -> $OUTPUT_ROOT/Launcher/win-x64/VSDK.exe"
echo "  macOS   -> $OUTPUT_ROOT/Launcher/osx-arm64/VSDK"
echo "  macOS   -> $OUTPUT_ROOT/Launcher/osx-x64/VSDK"
echo
du -sh "$OUTPUT_ROOT" || true
