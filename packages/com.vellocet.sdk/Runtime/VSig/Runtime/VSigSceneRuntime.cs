// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using Vertex.VSig;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Logging;

namespace Vellocet.Sdk.VSig.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VSigSceneRuntime : MonoBehaviour
    {
        [SerializeField, HideInInspector] private byte[] vertexProgram = Array.Empty<byte>();
        [SerializeField, HideInInspector] private string mapId;
        [SerializeField, HideInInspector] private List<NodeIdentity> nodeIds = new();
        [SerializeField, HideInInspector] private bool identityBaked;
        [SerializeField] private bool includeInactiveMarkers = true;
        private Dictionary<Transform, NodeIdentity> _nodesByTransform;

        public VSigRuntimeContext Context { get; private set; }

        public bool HasProgram => vertexProgram is { Length: > 0 };
        public Vertex.VSig.VSigCompiledProgram Program => GetVertexProgram();
        public Guid MapId => Guid.TryParse(mapId, out var id) ? id : Guid.Empty;
        internal bool IdentityBaked => identityBaked;

        [Serializable]
        private sealed class NodeIdentity
        {
            public Transform node;
            public string id;
            public string name;
        }

        public Vertex.VSig.VSigCompiledProgram GetVertexProgram() => vertexProgram is { Length: > 0 }
            ? Vertex.VSig.VSigCompiledProgram.Deserialize(vertexProgram) : null;

        internal Guid EnsureMapId()
        {
            if (identityBaked)
                return MapId != Guid.Empty ? MapId : throw new ArgumentException("Compiled VSig scene contains an invalid map ID.");
            // Check the entire imported hierarchy, including maps with no entity markers.
            var imported = VSigSceneCompiler.ResolveMapId(gameObject.scene, Guid.Empty);
            if (MapId != Guid.Empty)
            {
                if (imported != Guid.Empty && imported != MapId) throw new ArgumentException("VSig program and imported Vertex entities have different map IDs. Recompile this map.");
                return MapId;
            }
            if (!string.IsNullOrEmpty(mapId)) throw new ArgumentException("VSig scene contains an invalid map ID.");
            if (imported != Guid.Empty) { mapId = imported.ToString("D"); return imported; }
            throw new ArgumentException("VSig requires a persistent map ID. Recompile this map.");
        }

        internal Guid GetNodeId(Transform node)
        {
            var saved = GetNodeIdentity(node);
            if (!Guid.TryParse(saved.id, out var id) || id == Guid.Empty)
                throw new ArgumentException("VSig scene contains an invalid hierarchy ID.");
            return id;
        }

        internal string GetNodeName(Transform node) => GetNodeIdentity(node).name ?? node.name;

        private NodeIdentity GetNodeIdentity(Transform node)
        {
            nodeIds ??= new List<NodeIdentity>();
            _nodesByTransform ??= nodeIds.Where(value => value != null && value.node).ToDictionary(value => value.node);
            if (_nodesByTransform.TryGetValue(node, out var saved)) return saved;
            if (identityBaked) throw new ArgumentException("Compiled VSig hierarchy changed. Recompile this map.");
            saved = new NodeIdentity { node = node, id = Guid.NewGuid().ToString("D") };
            nodeIds.Add(saved);
            _nodesByTransform.Add(node, saved);
            return saved;
        }

#if UNITY_EDITOR
        /// <summary>Retain only the hierarchy used by VSig before removing authoring components.</summary>
        public void BakeSceneIdentity()
        {
            if (identityBaked) return;
            var markers = FindSceneMarkers().ToArray();
            var snapshot = new VSigSceneSnapshot(EnsureMapId(), markers, GetNodeId);
            var baked = new Dictionary<Transform, NodeIdentity>();
            foreach (var pair in snapshot.Entities)
            {
                var node = pair.Value.Node;
                for (var transform = pair.Key.transform; transform && !baked.ContainsKey(transform); transform = transform.parent)
                {
                    baked.Add(transform, new NodeIdentity { node = transform, id = node.Id.ToString("D"), name = node.Name });
                    node = node.Parent;
                }
            }
            nodeIds = baked.Values.ToList();
            _nodesByTransform = null;
            identityBaked = true;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void OnEnable()
        {
            CompileAndRegister();
        }

        private void OnDisable()
        {
            if (Context != null)
            {
                VSigRuntimeRegistry.UnregisterContext(Context);
                Context = null;
            }
        }

        private void Update()
        {
            Context?.Advance(Time.deltaTime);
        }

        public void Configure(Vertex.VSig.VSigCompiledProgram compiledProgram, Guid stableMapId, bool includeInactive = true)
        {
            if (compiledProgram == null) throw new ArgumentNullException(nameof(compiledProgram));
            if (stableMapId == Guid.Empty) throw new ArgumentException("Vertex maps require a persistent map ID.", nameof(stableMapId));
            var bytes = compiledProgram.Serialize();
            var previousMapId = mapId;
            var previousNodeIds = nodeIds;
            var previousIdentityBaked = identityBaked;
            try
            {
                identityBaked = false;
                _nodesByTransform = null;
                if (stableMapId != Guid.Empty) mapId = stableMapId.ToString("D");
                var markers = FindSceneMarkers(includeInactive).ToArray();
                EnsureMapId();
                nodeIds = (nodeIds ?? new List<NodeIdentity>()).Where(value => value != null && value.node).ToList();
                _ = new VSigSceneSnapshot(MapId, markers, GetNodeId);
            }
            catch
            {
                mapId = previousMapId;
                nodeIds = previousNodeIds;
                identityBaked = previousIdentityBaked;
                _nodesByTransform = null;
                throw;
            }
            includeInactiveMarkers = includeInactive;
            vertexProgram = bytes;
        }

        public bool CompileAndRegister()
        {
            if (!HasProgram || !isActiveAndEnabled)
                return false;
            VSigRuntimeContext candidate = null;
            try
            {
                candidate = new VSigRuntimeContext(GetVertexProgram(), FindSceneMarkers(), this);
                if (candidate.Diagnostics.Any(diagnostic => diagnostic.Severity == VSigDiagnosticSeverity.Error))
                {
                    candidate.Dispose();
                    return false;
                }
                VSigRuntimeRegistry.RegisterContext(candidate);
                if (Context != null) VSigRuntimeRegistry.UnregisterContext(Context);
                Context = candidate;
                return true;
            }
            catch (Exception exception)
            {
                if (candidate != null) VSigRuntimeRegistry.UnregisterContext(candidate);
                LogDiagnostic(new VSigDiagnostic(VSigDiagnosticSeverity.Error, 0, "VSIG_PROGRAM_REJECTED", exception.Message));
                return false;
            }
        }

        private IEnumerable<EntityMarker> FindSceneMarkers(bool? includeInactive = null)
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var marker in root.GetComponentsInChildren<EntityMarker>(includeInactive ?? includeInactiveMarkers))
                yield return marker;
        }

        private static void LogDiagnostic(VSigDiagnostic diagnostic)
        {
            if (diagnostic == null)
                return;

            var message = $"[VSig] {diagnostic}";
            switch (diagnostic.Severity)
            {
                case VSigDiagnosticSeverity.Error:
                    SdkConsoleBridge.Error(message);
                    break;
                case VSigDiagnosticSeverity.Warning:
                    SdkConsoleBridge.Warn(message);
                    break;
                default:
                    SdkConsoleBridge.Write(message);
                    break;
            }
        }
    }
}
