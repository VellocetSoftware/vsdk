# Changelog

## 4.1.0

- Brings the ANEURISM IV and Grimwar SDK copies back to one source; both games vendor this package unchanged.
- Adds `VertexBaseMapSave` and `BaseMapCompile`: one Vertex base-map save and compile flow for every game. A map folder holds its
  Vertex sources, `MapDefinition` and a single `Compiled` folder; the source scene and imported mesh are temporary.
- Keeps lightmaps and probe data with the compiled scene instead of shared `Generated/Bakes` generations, and copies
  imported materials with compiled geometry so compiled maps never depend on the `.vertexmesh` importer.
- Compiles Stereoform audio only when `com.vellocet.stereoform` is installed (`VELLOCET_STEREOFORM`); other games keep
  their own audio runtime through `SdkGameDefinition.AudioRuntimeCapability`.
- Lets publishers choose their distribution folder through `SdkPublisher.DistributionRoot`.

## 2.0.0

- Separates shared contracts and compiler tooling from Grimwar and ANEURISM IV authoring packages.
- Resolves game modes, reusable content roots, trigger layers and navigation masks through the active game profile.
- Adds package game identity and a per-game policy for external code.
- Preserves Grimwar marker serialization while moving its team filters and game-specific markers into its package.
- Adds legacy aliases to map definitions for existing server configuration and console commands.

## 1.9.0

- Targets Grimwar ModApi 14 and add-on schema 9.
- Exposes the normalized public server name through `IModContext.ServerName`.
- Applies Humanoid NavMesh agent, surface, traversal-link and advanced bake settings during map setup and every navigation bake.

## 1.8.0

- Targets Grimwar ModApi 13 and add-on schema 9.
- Adds manifest authoring support for operator-granted HTTPS audio streams.
- Adds host-rendered panels with readouts, audio controls, actions, and bounded text input.

## 1.7.0

- Targets Grimwar ModApi 12 and add-on schema 9.
- Adds authoring support for manifest-declared, operator-granted JSON HTTPS resources and consented external links.
- Generates server-only VMods. The host continues to load API 11 packages.

## 1.6.0

- Targets Grimwar ModApi 11 and add-on schema 9.
- Adds bounded, timed player-scale effects that compose across VMods and survive respawns.
- Adds Workbench for project setup, maps, entities, spatial data, VSig and add-on packages.
- Validates audio, map, VMod and mixed packages before replacing output; generates build IDs and UTC timestamps.
- Adds guided audio-pack creation, bulk cue import, direct add-on requirements, and the in-game content browser for references and provenance.
- Uses audio catalog schema 3, including bounded cue gain and dedicated two-dimensional ambience routing.
- Includes common entity lifecycle and operational contracts, contextual VSig targets, participant snapshots, and scripted game-mode bots.
- Adds indexed VSig timer ticks, explicit restart controls, deadline-anchored repeating schedules, and authoring diagnostics for unsafe elapsed-time equality.
- Links help actions to the Vellocet developer wiki.
- Updates SDK colors, Monda typography and entity icons.
- Uses Vellocet-owned assembly and namespace identities for reusable SDK and spatial contracts.
