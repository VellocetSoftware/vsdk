# Vellocet SDK

Unity integration for Vertex maps and add-on content. Game-specific source formats
and their standalone authoring tools belong to the game SDK.

Install exactly one matching game authoring package alongside this package:
`com.vellocet.sdk.grimwar` or `com.vellocet.sdk.aneurism-iv`. The game package owns
its modes, collision rules, layer mappings and game-specific entity fields.
Existing Grimwar marker assembly names, script GUIDs and serialized fields are preserved.
ANEURISM IV packages require their game identity and permit maps and VSig only;
external managed or native code is rejected before mounting.

Open **Tools → Vellocet → SDK → Workbench**.
See the [developer wiki](https://developer.vellocetsoftware.com/wiki/Vellocet_SDK)
for setup, authoring and publishing.

Release map packages require Windows Client, macOS Client and Linux Dedicated Server.
Keep authored source and built add-ons outside the installed SDK.
VSDK exposes standalone resources; Vertex attaches resources to maps.

`com.vellocet.vertex` supplies the shared contracts and VSig runtime. Integration
documentation and compiler checks live in the Vertex repository's `docs/` folder.

`Runtime/Content` owns content identities, manifests and shared asset addresses;
`Runtime/Maps` owns map data and game modes. `Editor/UI` contains shared window,
GUI and style code. Other editor/runtime folders group their matching features.

Map bake synchronization stores lightmap textures, reflection textures and probe
payloads once, under `Generated/Bakes/<baking-scene-guid>/` beside the authoring
scene. Moving those assets preserves their Unity GUIDs and importer settings.
Source and compiled scenes keep separate lighting, probe-set, navigation and
occlusion metadata while referencing the shared payloads. A skipped-lighting
compile reuses them. Rebaking keeps earlier payloads intact for scenes that still
reference them; remove old generations only after checking dependencies. Keep the shared bake folder when pruning old compiled-scene folders.

VSig includes its twelve standard logic modules: use `use counter as captures`
or `use latch as gate` directly, without importing a library.
This works for existing Unity-authored maps as well as Vertex maps.
The SDK keeps its game-specific `SpawnDoors.vsiglib`. Portable language tests live in Vertex; SDK host and
Unity adapter checks live in `Tests/Editor/VSigSdkIntegrationTests.cs`.
