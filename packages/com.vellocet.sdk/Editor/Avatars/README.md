# Entity avatars

Scene view loads previews from `Editor/Avatars/{entity_class}.prefab`.
Keep Gizmos enabled to see them. Select a marker and press **F** to frame its avatar.

Use a static prefab with `MeshFilter`/`MeshRenderer` components and materials.
Match the runtime entity's pivot, facing and size. Keep dependencies in the SDK's
`Editor` folder so the standalone SDK needs no game assets.
The drawer supports child meshes, submeshes, and authored prefab scale.

Avatars follow the marker's world position and rotation. Marker scale is ignored,
matching point-entity spawning. Previews draw in Scene view during edit mode
without adding objects or references to the map.

`func_resupply` contains a static bake of the resupply locker's closed pose, with a
512-pixel albedo and an unlit material. Re-bake it if the runtime model changes.
