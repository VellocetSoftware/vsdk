using System.IO;
using UnityEditor;
using UnityEngine;
using Vertex.Contracts;
using Vertex.Unity.Editor;

namespace Vellocet.Sdk.Editor.Maps
{
    /// <summary>Compiled collision uses native layers; no per-object runtime scripts or pairwise ignore lists.</summary>
    [InitializeOnLoad]
    public static class VertexCollisionPolicy
    {
        public const string Id = "com.vellocet.sdk.collision";
        static VertexCollisionPolicy() => VertexSdkRegistry.RegisterCollisionPolicy(Id, ResolveLayer);
        public static int NavigationMask => ~LayerMask.GetMask("EntitySolid", "EntityClip", "RaycastOnly");

        public static GameCollisionPolicy Create() => new()
        {
            Id = Id, DefaultMask = 3, IgnoreRaycasts = false, ProxyIgnoreRaycasts = true,
            Targets = new[]
            {
                new CollisionTarget { Bit = 1, Name = "Players" },
                new CollisionTarget { Bit = 2, Name = "Entities" }
            }
        };

        public static int ResolveLayer(MapCollisionFilter filter)
        {
            if (filter.Policy != Id || (filter.Mask & ~3u) != 0)
                throw new InvalidDataException("Choose the collision targets provided by the Vellocet SDK profile.");
            if (filter.Mask == 0 && filter.IgnoreRaycasts) return -1;
            var name = (filter.Mask, filter.IgnoreRaycasts) switch
            {
                (3, false) => "Default",
                (3, true) => "WorldClip",
                (1, false) => "PlayerSolid",
                (1, true) => "PlayerClip",
                (2, false) => "EntitySolid",
                (2, true) => "EntityClip",
                (0, false) => "RaycastOnly",
                _ => throw new InvalidDataException("Unsupported collision target combination.")
            };
            var layer = LayerMask.NameToLayer(name);
            if (layer < 0) throw new InvalidDataException("The SDK compiler is missing collision layer '" + name + "'. Update its published ProjectSettings.");
            return layer;
        }
    }
}
