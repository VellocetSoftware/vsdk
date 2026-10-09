# VSDK

VSDK sets up a game's SDK and opens Vertex. The complete SDK supplies the editor,
authoring content, engine packages and a standalone compiler. First launch detects
the required engine version, accepts an optional installed game location, prepares
the writable compiler cache and registers the game in Vertex. The launcher offers
map authoring and the standalone resource types supplied by the game SDK. Modders
do not create an engine project or copy package paths.

Create or open `.vertex` maps in Vertex. **Play in Game** compiles the current map,
installs its add-on and launches the installed game. **Export add-on** creates a
package for distribution. Grimwar's public SDK contains no private gameplay source
and offers no game playback in Unity. Its developers instead connect the private
game checkout through Unity's **Tools → Vertex → Open Map Editor**, then use
**Save to Unity** and play in that editor.

These choices come from the game's profile and SDK connection. Studios can expose
an editor project publicly when their game permits it. VSDK discovers any single
`.vertexsdk` beside or above the launcher; it has no hardcoded game identity.

## Standalone resources

Choose a resource type, then **Create** or **Open** its manifest. **Open folder**
exposes the manifest and source files; **Edit source** opens the default editor.
Save changes, then **Validate**, **Build**, or **Play in game**. These operations
use the game SDK's resource tool and do not require engine setup. VSDK has no
knowledge of VMods, programming languages, or game runtime behavior.

Map-bound resources are attached in Vertex and included in the map build. They
use the same source format and SDK validator as their standalone equivalents.

## Build and test

```sh
dotnet build VSDK.slnx -c Release
python3 -m unittest discover -s scripts -p 'test_*.py'
dotnet run --project src/VSDK -c Release -- --sdk /path/to/Game.vertexsdk
# Check distribution files without opening a window:
dotnet run --project src/VSDK -c Release -- --check --sdk /path/to/Game.vertexsdk
```

`--vertex /path/to/Vertex` (or `/path/to/Vertex.app` on macOS) selects a local editor build. `--map /path/to/Map.vertex`
opens a map after setup; `--setup` reopens setup even when already configured.
Unity users install and activate the version named by their profile through Unity
Hub, including the build module for their target platform. Setup detects standard
Hub locations and lets users choose a different installation.

## Distribution

Build the matching Vertex inputs first, then compose the toolkit:

```sh
# In the Vertex checkout, on macOS:
python3 tools/build-sdk.py
# In this checkout:
python3 scripts/build-sdk.py --vertex ../vertex/artifacts/sdk
```

TeamCity's **Tools → Vertex → Build and Validate** supplies the editors and matching
Unity package and worker libraries. **Tools → VSDK → Build SDK Toolkit** consumes that same-chain editor
artifact and publishes `vsdk-tools.zip`. Grimwar consumes the toolkit and Unity
package from the same chain, exports its approved content and atomically publishes:

```text
Grimwar.vertexsdk
Compiler.inputs.json
Compiler/
Compiler/VertexTools/<runtime-id>/    game-owned resource tools and public API
Authoring/
Editor/win-x64/Vertex.exe
Editor/osx-<architecture>/Vertex.app/
Launcher/<runtime-id>/VSDK[.exe]
toolchain.json
LICENSE.txt
```

The runtimes are `win-x64`, `osx-arm64`, and `osx-x64`. Steam's launch paths remain
`Launcher/win-x64/VSDK.exe`, `Launcher/osx-arm64/VSDK`, and `Launcher/osx-x64/VSDK`.
The launcher opens the complete macOS app bundle; setup and resource commands use
its `Contents/MacOS/Vertex` executable without opening a window.
`toolchain.json` records the Vertex, VSDK and game source revisions. Release the
complete SDK together; never update just one executable or package inside it.
Toolkit `Libraries/` are build inputs for the game's resource worker and are not
copied into the public game SDK.

Vertex verifies the compiler inventory before preparing a writable cache outside
the installed SDK. Updates select a new compiler revision while keeping maps,
preferences, installed game locations and developer connections. Each map retains
its chosen SDK connection even when another installation of the same game is added.

The [Vellocet SDK wiki](https://developer.vellocetsoftware.com/wiki/Vellocet_SDK)
is the public documentation. The [license](LICENSE.txt) permits commercial and
noncommercial use with Vellocet credit. Modification is prohibited. Redistribution is
limited to unmodified runtime components in compiled form inside finished projects,
as set out in the license.
The Monda font license is in `src/VSDK/Assets/Fonts/OFL.txt`.

`src/VSDK` contains the launcher and setup UI; `scripts` contains toolkit packaging.
Generated builds live in ignored `artifacts/`. TeamCity configuration lives in the
sibling `build-infra` repository.
