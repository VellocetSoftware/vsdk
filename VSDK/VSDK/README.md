# VSDK Setup Guide

Minimal launcher intended for a Steam Tool app that distributes the Unity SDK.

## What it does

- No buttons or branching UI; it is a read-only setup guide
- Auto-detects SDK distribution root (supports both direct layout and nested `Build/`)
- Verifies required artifacts for setup:
    - SDK package (`SDKPackage/*.unitypackage` or `UnityPackage/*.unitypackage`)
    - SDK content (`SDKContent` or `VellocetSDKContent`) with `sdk-content-manifest.json`
- Shows a single guided setup flow for Unity:
    - import package
    - link SDK content (`Tools > Vellocet > SDK > Link SDK Content`)
    - open SDK editor (`Tools > Vellocet > SDK > Editor`)
- Auto-refreshes status while open

## Build

From the solution root:

```bash
dotnet build VSDK.sln -c Release
```

## Run locally

```bash
dotnet run --project VSDK/VSDK.csproj
```

## Publish

Windows x64:

```bash
dotnet publish VSDK/VSDK.csproj -c Release -r win-x64 --self-contained false
```

macOS Apple Silicon:

```bash
dotnet publish VSDK/VSDK.csproj -c Release -r osx-arm64 --self-contained false
```

macOS Intel:

```bash
dotnet publish VSDK/VSDK.csproj -c Release -r osx-x64 --self-contained false
```

Self-contained example (larger output, no runtime prerequisite):

```bash
dotnet publish VSDK/VSDK.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Published output path:

`VSDK/bin/Release/net10.0/<RID>/publish/`

## Combined Steam Bundle (Windows + macOS ARM64 + macOS Intel)

From the solution root (`VSDK/`):

```bash
chmod +x scripts/build-steam-tool.sh
./scripts/build-steam-tool.sh --sdk-source /Users/evanjustino/Documents/GitHub/warlock/Build
```

Default output:

`Build/SteamTool`

Output layout:

```text
Build/SteamTool/
  Launcher/
    win-x64/
      VSDK.exe
      ...
    osx-arm64/
      VSDK
      ...
    osx-x64/
      VSDK
      ...
  SDKPackage/
    *.unitypackage
  SDKContent/
    sdk-content-manifest.json
    Assets/...
  Docs/ (if present in sdk source)
  README.txt or README.md (if present in sdk source)
  STEAM_NOTES.txt
```

Steam launch paths:

- Windows launch option target: `Launcher/win-x64/VSDK.exe`
- macOS Apple Silicon launch option target: `Launcher/osx-arm64/VSDK`
- macOS Intel launch option target: `Launcher/osx-x64/VSDK`
