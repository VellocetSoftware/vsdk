#!/usr/bin/env bash
# Copyright (c) 2026 Vellocet Corporation. All rights reserved.
# SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

set -euo pipefail

usage() {
  cat <<'USAGE'
Build launcher binaries for Steam Tool distribution:
- Launcher/win-x64
- Launcher/osx-arm64
- Launcher/osx-x64

Usage:
  ./scripts/build-steam-tool.sh [options]

Options:
  --output <path>            Output directory for staged launcher binaries.
                             Default: Build/Launcher (under this solution root)
  --configuration <name>     dotnet configuration (default: Release)
  --framework-dependent      Publish framework-dependent instead of self-contained
  --help                     Show this help
USAGE
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
REPOSITORY_ROOT="$(cd "$SOLUTION_ROOT/.." && pwd)"
PROJECT_FILE="$SOLUTION_ROOT/VSDK/VSDK.csproj"
SOLUTION_FILE="$SOLUTION_ROOT/VSDK.sln"
LICENSE_FILE="$REPOSITORY_ROOT/LICENSE.txt"

CONFIGURATION="Release"
SELF_CONTAINED="true"
OUTPUT_ROOT="$SOLUTION_ROOT/Build/Launcher"
RIDS=("win-x64" "osx-arm64" "osx-x64")

while [[ $# -gt 0 ]]; do
  case "$1" in
    --output)
      if [[ $# -lt 2 || -z "${2:-}" ]]; then
        echo "--output requires a non-empty path." >&2
        exit 1
      fi
      OUTPUT_ROOT="${2:-}"
      shift 2
      ;;
    --configuration)
      if [[ $# -lt 2 || -z "${2:-}" ]]; then
        echo "--configuration requires a non-empty value." >&2
        exit 1
      fi
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

if [[ ! "$CONFIGURATION" =~ ^[A-Za-z0-9._-]+$ ]]; then
  echo "Invalid configuration name: $CONFIGURATION" >&2
  exit 1
fi

require_dir() {
  local path="$1"
  if [[ ! -d "$path" ]]; then
    echo "Required directory not found: $path" >&2
    exit 1
  fi
}

require_file() {
  local path="$1"
  if [[ ! -f "$path" ]]; then
    echo "Required file not found: $path" >&2
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

output_parent="$(dirname "$OUTPUT_ROOT")"
output_name="$(basename "$OUTPUT_ROOT")"
if [[ -z "$output_name" || "$output_name" == "." || "$output_name" == ".." || "$output_name" == "/" ]]; then
  echo "Refusing unsafe output path: $OUTPUT_ROOT" >&2
  exit 1
fi

if [[ -L "$OUTPUT_ROOT" ]]; then
  echo "Refusing a symbolic link as the output directory: $OUTPUT_ROOT" >&2
  exit 1
fi

mkdir -p "$output_parent"
output_parent="$(cd "$output_parent" && pwd)"
OUTPUT_ROOT="$output_parent/$output_name"

if [[ "$OUTPUT_ROOT" == "$SOLUTION_ROOT" || "$OUTPUT_ROOT" == "$REPOSITORY_ROOT" ]]; then
  echo "Refusing to use a repository root as the output directory: $OUTPUT_ROOT" >&2
  exit 1
fi

mkdir -p "$OUTPUT_ROOT"

require_dir "$SOLUTION_ROOT/VSDK"
require_file "$LICENSE_FILE"

echo "==> Solution root: $SOLUTION_ROOT"
echo "==> Output root:   $OUTPUT_ROOT"
echo "==> Configuration: $CONFIGURATION"
echo "==> Self-contained: $SELF_CONTAINED"
echo

echo "==> Cleaning output root"
rm -rf "$OUTPUT_ROOT/Launcher"
rm -f "$OUTPUT_ROOT/LAUNCHER_NOTES.txt" "$OUTPUT_ROOT/vsdk-build-metadata.json" "$OUTPUT_ROOT/LICENSE.txt"

unexpected_entry="$(find "$OUTPUT_ROOT" -mindepth 1 -maxdepth 1 -print -quit)"
if [[ -n "$unexpected_entry" ]]; then
  echo "Refusing to mix the launcher artifact with unrelated output: $unexpected_entry" >&2
  echo "Choose an empty, dedicated --output directory." >&2
  exit 1
fi

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

require_file "$OUTPUT_ROOT/Launcher/win-x64/VSDK.exe"
require_file "$OUTPUT_ROOT/Launcher/osx-arm64/VSDK"
require_file "$OUTPUT_ROOT/Launcher/osx-x64/VSDK"

cp "$LICENSE_FILE" "$OUTPUT_ROOT/LICENSE.txt"

cat > "$OUTPUT_ROOT/LAUNCHER_NOTES.txt" <<EOF
Launcher-only bundle generated on $(date -u +"%Y-%m-%dT%H:%M:%SZ")

Launch executables:
- Windows: Launcher/win-x64/VSDK.exe
- macOS:   Launcher/osx-arm64/VSDK
- macOS:   Launcher/osx-x64/VSDK

This is the launcher artifact only. The product build pipeline composes this with SDKPackage/ and SDKContent/ for Steam.

Expected composed Steam tool layout:
- Launcher/
- SDKPackage/
- SDKContent/
- LICENSE.txt
EOF

cat > "$OUTPUT_ROOT/vsdk-build-metadata.json" <<EOF
{
  "artifact": "vsdk-launcher",
  "createdUtc": "$(date -u +"%Y-%m-%dT%H:%M:%SZ")",
  "configuration": "$CONFIGURATION",
  "selfContained": $SELF_CONTAINED,
  "rids": [
$(for i in "${!RIDS[@]}"; do
  suffix=","
  if [[ "$i" == "$((${#RIDS[@]} - 1))" ]]; then
    suffix=""
  fi
  printf '    "%s"%s\n' "${RIDS[$i]}" "$suffix"
done)
  ]
}
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
