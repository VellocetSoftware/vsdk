# VSDK — Unity SDK Setup

Launcher for the Steam Tool app that verifies and explains the Vellocet Unity SDK installation.

## What it does

- Detects the Steam tool distribution root from the launcher location.
- Verifies the final distribution layout:
  - `Launcher/`
  - `SDKPackage/package.json`
  - `SDKContent/sdk-content-manifest.json`
- Shows the current Unity setup flow:
  - add `SDKPackage/package.json` through Package Manager
  - let Unity open SDK Workbench and SDK Map Exporter
  - select the VSDK install root when prompted so `SDKPackage` and `SDKContent` stay synchronized
  - create or prepare a map from SDK Workbench
- Auto-refreshes status while open.

## Build

From the solution root:

```bash
dotnet build VSDK.sln -c Release
```

## Run locally

```bash
dotnet run --project VSDK/VSDK.csproj
```

## Publish launcher artifact

From the solution root (`VSDK/`):

```bash
chmod +x scripts/build-steam-tool.sh
./scripts/build-steam-tool.sh
```

Default output:

```text
Build/Launcher/
  Launcher/
    win-x64/
      VSDK.exe
    osx-arm64/
      VSDK
    osx-x64/
      VSDK
  LAUNCHER_NOTES.txt
  vsdk-build-metadata.json
```

Grimwar TeamCity consumes this launcher artifact and composes the final Steam tool payload:

```text
Launcher/
SDKPackage/
SDKContent/
```

Steam launch paths:

- Windows: `Launcher/win-x64/VSDK.exe`
- macOS Apple Silicon: `Launcher/osx-arm64/VSDK`
- macOS Intel: `Launcher/osx-x64/VSDK`
