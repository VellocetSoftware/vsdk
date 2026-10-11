using System;
using System.IO;
using System.Linq;
using UnityEditor;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Rendering;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vertex.Contracts;
using Vertex.Unity;

namespace Vellocet.Sdk.Editor.Maps
{
    internal static class VertexMapPolicy
    {
        internal static void Apply(MapAsset map, GameObject root, bool compiling = false)
        {
            var objects = root.GetComponentsInChildren<VertexObjectIdentity>(true).ToDictionary(identity => identity.ObjectId);
            foreach (var source in map.Containers.Where(container => container.Model != null))
            {
                var model = objects[source.Id];
                foreach (var component in model.GetComponentsInChildren<Component>(true))
                {
                    if (!component || !AddonScenePolicy.IsAllowedComponent(component.GetType()) || component is EntityMarker ||
                        component is Rigidbody body && !body.isKinematic || component is Rigidbody2D || component is Joint || component is Joint2D)
                        throw new InvalidDataException($"Model '{source.Name}' [{source.Id}] contains unsupported gameplay/physics component '{component?.GetType().Name ?? "missing"}'. Use a decorative model or an SDK entity that owns its runtime behavior.");
                }
                var movable = model.GetComponentsInChildren<Transform>(true).Any(MapRendererOptimizationUtility.HasRuntimeMotionRisk);
                SetMobility(model.gameObject, movable);
                // Scene-only bake metadata must not become unregistered scripted-importer subassets.
                if (compiling && movable)
                    (model.GetComponent<NavMeshModifier>() ?? model.gameObject.AddComponent<NavMeshModifier>()).ignoreFromBuild = true;
            }
            foreach (var marker in root.GetComponentsInChildren<EntityMarker>(true))
                if (marker is IMovingBrushEntityMarker or DamageableMarker or ParticleMarker)
                {
                    if (!marker.IgnoreNavMesh) throw new InvalidDataException($"Moving, breakable or effect entity '{marker.Guid}' must exclude its geometry from the static NavMesh. Reset Ignore Nav Mesh.");
                    SetMobility(marker.gameObject, true);
                }
        }

        private static void SetMobility(GameObject root, bool movable)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(transform.gameObject, movable ? 0 :
                    StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.staticShadowCaster = !movable;
                if (renderer is not MeshRenderer meshRenderer) continue;
                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                // Keep installed base meshes shared. Assets without authored UV2 receive probes instead of a generated mesh copy.
                meshRenderer.receiveGI = !movable && mesh && mesh.HasVertexAttribute(VertexAttribute.TexCoord1) ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
            }
        }
    }
}
