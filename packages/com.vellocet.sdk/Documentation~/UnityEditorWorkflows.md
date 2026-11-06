# Unity editor workflows

Grimwar and ANEURISM IV use the same Vellocet editor commands. Shared tooling lives
in Framework, the SDK, and Vertex; games register their content and build policies.

## Author a map in Unity

1. Save the authoring scene in its own map folder under `Assets`.
2. In that folder, choose **Assets > Create > Vellocet > Maps > Map Definition**.
3. Set the ID, display name, author, version, and optional thumbnail. Assign the
   saved scene to **Authoring Scene** in the normal Inspector.
4. Give the ID a registered mode prefix and a name, for example `sb_example`
   in Grimwar or `nm_example` in ANEURISM IV. The Inspector shows the resolved mode.
5. Save open scenes and choose **Compile Map** in the Inspector, its context menu,
   or **Tools > Vellocet > Content > Maps > Compile Map**.

Creation makes a draft asset. Its first successful compile enables it. Failed or
canceled compilation leaves previous output and availability intact. Recompiling
an existing disabled map does not enable it.

Keep one definition per folder, outside every map's `Compiled` output. Compiled scenes, bake data, and the receipt live
in that folder's `Compiled` directory. Compilation manages the scene reference,
music selection, content revision, and Addressables entries. Keep editing the
authoring scene rather than the compiled scene.

**Enabled** controls whether a compiled map is available. **Editor Only** keeps an
enabled development map available in the Editor and excludes it from player builds.
Game mode comes from the ID prefix. Legacy stored mode metadata must agree with
that prefix and cannot override it.

Existing game folders remain supported:

| Game        | Map folder                           |
|-------------|--------------------------------------|
| Grimwar     | `Assets/GameContent/Maps/<map>/`     |
| ANEURISM IV | `Assets/Scenes/Maps/Gameplay/<map>/` |

## Author a map in Vertex

Choose **Tools > Vellocet > SDK > Open Vertex Map Editor…** in either game.
Use the game's Developer Project connection and **Save to Unity** to compile a
base map. **Refresh Vertex Developer Connection** republishes changed definitions.
Vertex sources remain editable; imported intermediate Unity scenes are temporary.
Use **Export add-on** for independently installed maps.

## Prepare, validate, and build

- **Tools > Vellocet > Content > Prepare Game Content** synchronizes generated content.
- **Tools > Vellocet > Diagnostics > Validate Project** reports problems without
  repairing assets, resealing receipts, or deleting generated content.
- **Tools > Vellocet > SDK > Workbench** exposes Unity and Vertex authoring and diagnostics.
- Manual player builds live under **Tools > Vellocet > Build > Client** and **Build > Server > Linux Dedicated
  Server…**. Both games use the same output-folder selection.
- SDK export commands live under **Tools > Vellocet > SDK**, including **Export Game
  Profile…**, **Export Selected Asset Catalog…**, and **Build Distribution…**.

Project-changing commands require Edit Mode and an idle Editor. Map compilation
also requires completed bakes and saved scenes. Batch entrypoints retain their
existing names and report failures without interactive dialogs.

## Keep shared packages consistent

Edit shared packages in their source repositories, then adopt the same verified
Framework, SDK, and Vertex revisions in both games. Do not maintain game-specific
copies of shared inspectors or commands. Product-specific commands use the same
`Tools/Vellocet` root; native Unity context menus and third-party menus retain
their own locations.

From a game checkout, run its `Tools/Packages/check.py` (under `UnityProject` in
Grimwar). To verify both games together, pass both repository roots to either
wrapper. Verification checks actual package contents as well as provenance, and
requires the shared editor stack to match. It never changes package files.

Keep authored assets and Unity metadata in Git. Preserve existing map IDs, script
GUIDs, scene addresses, and compilation receipts during tooling migrations.