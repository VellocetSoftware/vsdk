# VSDK

Desktop control surface for the Vellocet Unity SDK Steam Tool distribution.

VSDK verifies the installed `SDKPackage` and `SDKContent`, exposes the exact paths needed by Unity, and opens the
[Vellocet SDK developer wiki](https://developer.vellocetsoftware.com/wiki/Vellocet_SDK). User guidance is maintained
on the wiki rather than embedded in the launcher or shipped inside the Unity package.

The launcher and SDK are distributed under the terms in `LICENSE.txt`.

## Capabilities

- Detects the Steam Tool distribution root from the launcher location.
- Validates package identity, version, license metadata, required Unity version, and content schema.
- Verifies the SDK content manifest, managed entry set, and content assets folder.
- Enforces the wiki-only documentation policy for `SDKPackage`.
- Shows structured pass/fail checks with an issues-only filter.
- Opens the install folder and official wiki.
- Copies the Package Manager path, install path, or a complete diagnostic snapshot.
- Refreshes automatically while the launcher is open and supports manual refresh.

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

The default launcher artifact is written to `Build/Launcher`. Grimwar TeamCity combines it with `SDKPackage`,
`SDKContent`, and the distribution license before publishing the Steam Tool payload.

Steam launch paths:

- Windows: `Launcher/win-x64/VSDK.exe`
- macOS Apple Silicon: `Launcher/osx-arm64/VSDK`
- macOS Intel: `Launcher/osx-x64/VSDK`

The embedded Monda font is distributed under the SIL Open Font License in `Assets/Fonts/OFL.txt`.
