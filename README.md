# VSDK

The Grimwar SDK launcher opens Vertex with the installed game profile and compiler. Map authors use Vertex for geometry, entities, materials, VSig and add-on exports. They do not create a Unity project or copy Package Manager paths.

Unity remains the asset and compilation backend. The required Unity version and license must be installed once through Unity Hub. Vertex detects standard Hub installations and prepares a private compiler on first export.

## Build and test

From this repository root:

```sh
dotnet build VSDK.slnx -c Release
dotnet run --project src/VSDK -c Release -- --sdk /path/to/Grimwar.vertexsdk
# Validate an installation without opening the GUI:
dotnet run --project src/VSDK -c Release -- --check --sdk /path/to/Grimwar.vertexsdk
```

`--vertex /path/to/Vertex` overrides the bundled executable for local development. `--sdk` also accepts a developer connection published by Grimwar; Vertex then offers Save to Unity as well as Export add-on.

## Distribution

`scripts/build-steam-tool.sh` publishes the launcher. Compose its `Launcher/` output with the distribution produced by Grimwar's `SdkToolsBuilder`:

```text
Grimwar.vertexsdk
Compiler.inputs.json
Compiler/
Authoring/
Editor/<runtime-id>/Vertex[.exe]
Launcher/<runtime-id>/VSDK[.exe]
LICENSE.txt
```

Steam launch paths remain `Launcher/win-x64/VSDK.exe`, `Launcher/osx-arm64/VSDK` and `Launcher/osx-x64/VSDK`. The launcher discovers `Grimwar.vertexsdk` above its executable and starts the matching bundled Vertex. The publisher must supply a Vertex binary for every distributed runtime.

The compiler contains the SDK, engine dependencies, approved base-content proxies and portable Grimwar authoring contracts. It contains no Grimwar gameplay implementation. Vertex verifies its file inventory before preparing a writable copy; installed SDK files remain read-only inputs.

The [Vellocet SDK wiki](https://developer.vellocetsoftware.com/wiki/Vellocet_SDK) is the public documentation. Distribution terms are in `LICENSE.txt`; the Monda font license is in `src/VSDK/Assets/Fonts/OFL.txt`.

## Repository layout

`src/VSDK` contains the launcher; `scripts` contains its publisher. Generated builds live in ignored `artifacts/` directories. Shared TeamCity configuration lives in the sibling `build-infra` repository.
