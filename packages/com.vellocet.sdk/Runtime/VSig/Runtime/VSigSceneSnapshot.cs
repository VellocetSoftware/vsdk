using Vertex.VSig;
using System;
using System.Collections.Generic;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Portable = Vertex.Contracts;

namespace Vellocet.Sdk.VSig.Runtime
{
    /// <summary>Converts Unity authoring data once; running scripts retain only Vertex descriptors.</summary>
    internal sealed class VSigSceneSnapshot
    {
        private readonly Dictionary<Guid, EntityMarker> _markersById = new();
        public Dictionary<EntityMarker, Portable.EntityDescriptor> Entities { get; } = new();
        public Guid MapId { get; }

        internal static Guid ImportedMapId(IEnumerable<EntityMarker> markers)
        {
            var result = Guid.Empty;
            foreach (var marker in markers ?? Array.Empty<EntityMarker>())
                if (marker && marker.GetComponentInParent<Vertex.Unity.VertexObjectIdentity>(true) is { } identity)
                {
                    if (identity.MapId == Guid.Empty || result != Guid.Empty && result != identity.MapId)
                        throw new ArgumentException("A VSig scene must contain entities from one Vertex map with a valid map ID.");
                    result = identity.MapId;
                }
            return result;
        }

        public VSigSceneSnapshot(Guid mapId, IEnumerable<EntityMarker> markers, Func<Transform, Guid> nodeId,
            Func<Transform, string> bakedNodeName = null)
        {
            MapId = mapId;
            var nodes = new Dictionary<Transform, Portable.SceneNode>();
            var ids = new Dictionary<Transform, Guid>();
            var nodeIds = new HashSet<Guid>();
            foreach (var marker in markers ?? Array.Empty<EntityMarker>())
            {
                if (!marker) continue;
                if (_markersById.Count >= Portable.MapAsset.MaximumObjects)
                    throw new ArgumentException("The scene has too many authored VSig nodes.");
                if (marker.Guid == Guid.Empty || !_markersById.TryAdd(marker.Guid, marker))
                    throw new ArgumentException($"Entity '{marker.name}' has a missing or duplicate stable ID.");
                ids.Add(marker.transform, marker.Guid);
                // Data attachments still contribute stable hierarchy IDs, but have no signal runtime to bind.
                var contract = marker.SignalContract;
                if (contract.Inputs.Count == 0 && contract.Outputs.Count == 0 && contract.Properties.Count == 0 &&
                    contract.States.Count == 0 && contract.Capabilities == EntitySignalCapabilities.None &&
                    marker is not Vellocet.Sdk.Entities.LogicScriptMarker) continue;
                if (Entities.Count >= VSigSecurityLimits.MaximumSceneMarkers)
                    throw new ArgumentException("The scene has too many VSig entities.");
                Entities.Add(marker, null);
            }
            Portable.SceneNode Node(Transform transform, int depth = 0)
            {
                if (!transform) return null;
                if (depth > 64) throw new ArgumentException("VSig scene hierarchy exceeds 64 levels.");
                if (nodes.TryGetValue(transform, out var existing)) return existing;
                var parent = Node(transform.parent, depth + 1);
                // Non-entity IDs are persisted by VSigSceneRuntime. Hostless callers own a temporary snapshot.
                var identity = bakedNodeName == null ? transform.GetComponent<Vertex.Unity.VertexObjectIdentity>() : null;
                if (identity && identity.MapId != mapId) throw new ArgumentException("Vertex hierarchy belongs to a different map.");
                var id = ids.TryGetValue(transform, out var entityId) ? entityId : identity ? identity.ObjectId : nodeId?.Invoke(transform) ?? Guid.NewGuid();
                if (!nodeIds.Add(id)) throw new ArgumentException("VSig hierarchy contains duplicate stable IDs.");
                var node = new Portable.SceneNode(id, identity ? identity.AuthoredName : bakedNodeName?.Invoke(transform) ?? transform.name, parent);
                nodes.Add(transform, node);
                return node;
            }
            foreach (var marker in new List<EntityMarker>(Entities.Keys))
                Entities[marker] = new Portable.EntityDescriptor(mapId, Node(marker.transform),
                    Contract(marker.SignalContract), marker.GetType().Name, marker.GetType().FullName);
        }

        public Portable.EntitySignalContext ToVertex(EntitySignalContext context) =>
            new(ToVertex(context.Activator), ToVertex(context.Caller), ToVertex(context.Self));

        public EntitySignalContext ToSdk(Portable.EntitySignalContext context) =>
            new(ToSdk(context.Activator), ToSdk(context.Caller), ToSdk(context.Self));

        private Portable.EntitySignalHandle ToVertex(EntitySignalHandle handle)
        {
            if (handle.Kind == EntitySignalHandleKind.Actor)
                return Portable.EntitySignalHandle.ForActor(handle.RuntimeIdentity, handle.DisplayName);
            return handle.IsEntity && _markersById.TryGetValue(handle.StableGuid, out var marker) && marker &&
                   handle.Equals(EntitySignalHandle.ForEntity(marker))
                && Entities.TryGetValue(marker, out var descriptor)
                ? Portable.EntitySignalHandle.ForEntity(descriptor) : default;
        }

        private EntitySignalHandle ToSdk(Portable.EntitySignalHandle handle)
        {
            if (handle.Kind == Portable.EntitySignalHandleKind.Actor)
                return EntitySignalHandle.ForActor(handle.RuntimeIdentity, handle.DisplayName);
            return handle.IsEntity && handle.MapId == MapId && _markersById.TryGetValue(handle.StableGuid, out var marker)
                ? EntitySignalHandle.ForEntity(marker) : default;
        }

        private static Portable.EntitySignalContract Contract(EntitySignalContract source) =>
            new(source.EntityClassName, Members(source.Inputs), Members(source.Outputs), Members(source.Properties),
                Members(source.States), (Portable.EntitySignalCapabilities)source.Capabilities);

        private static Portable.EntitySignalMember[] Members(IReadOnlyList<EntitySignalMember> source)
        {
            var result = new Portable.EntitySignalMember[source.Count];
            for (var i = 0; i < result.Length; i++)
            {
                var member = source[i];
                result[i] = new Portable.EntitySignalMember(member.Name, (Portable.EntitySignalMemberKind)member.Kind,
                    (Portable.EntitySignalValueType)member.ValueType, (Portable.EntitySignalAuthority)member.Authority,
                    member.Readable, member.Writable, member.TypeName);
            }
            return result;
        }

        internal sealed class Runtime : Portable.IEntitySignalRuntime
        {
            private readonly VSigSceneSnapshot _scene;
            private readonly Portable.EntityDescriptor _entity;
            public IEntitySignalRuntime Source { get; }
            public Runtime(VSigSceneSnapshot scene, Portable.EntityDescriptor entity, IEntitySignalRuntime source)
            { _scene = scene; _entity = entity; Source = source; }
            public Portable.EntitySignalContract SignalContract => _entity.SignalContract;
            public Portable.EntitySignalHandle SignalHandle => Portable.EntitySignalHandle.ForEntity(_entity);
            public bool InvokeInput(string name, string parameters, Portable.EntitySignalContext context) =>
                Source.InvokeInput(name, parameters, _scene.ToSdk(context));
            public bool TryReadProperty(string name, out object value) => Source.TryReadProperty(name, out value);
            public bool TryCaptureState(out Portable.IEntityStateSnapshot state)
            {
                var result = Source.TryCaptureState(out var snapshot);
                state = snapshot;
                return result;
            }
            public bool TryRestoreState(Portable.IEntityStateSnapshot state) =>
                state is IEntityStateSnapshot snapshot && Source.TryRestoreState(snapshot);
        }
    }
}
