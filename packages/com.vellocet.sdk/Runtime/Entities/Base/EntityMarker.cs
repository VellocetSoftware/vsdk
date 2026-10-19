// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEngine;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.Logging;
using Vellocet.Sdk.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vellocet.Sdk.Entities.Base
{
    [DisallowMultipleComponent]
    public abstract class EntityMarker : MonoBehaviour
    {
        [NonSerialized] private EntitySignalContract _runtimeSignalContract;

        private static readonly Dictionary<Guid, EntityMarker> ByGuid = new();
        private static readonly Dictionary<Type, string> EntityClassByType = new();
        private static readonly Dictionary<Type, string> IconPathByType = new();

        [Tooltip("Hidden stable runtime ID. VSig authoring uses GameObject names and entity classes.")]
        [SerializeField]
        [ReadOnly]
        private string guid;

        [Header("NavMesh")]
        [Tooltip("If true, adds/updates a NavMeshModifier so this marker is ignored by NavMesh builds.")]
        [SerializeField]
        private bool ignoreNavMesh;

        [SerializeField] [HideInInspector] private bool ignoreFromNavMeshInitialized;

        public Guid Guid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(guid)) return Guid.Empty;
                return Guid.TryParse(guid, out var g) ? g : Guid.Empty;
            }
        }

        public bool IgnoreNavMesh => ignoreNavMesh;
        public string EntityClassName => GetEntityClassName(GetType());

        public virtual string IconPath => GetIconPath(GetType());

        /// <summary>Authoring-only model key; never instantiated by the runtime entity.</summary>
        public virtual string AvatarModel => GetType().GetCustomAttribute<EntityAuthoringAttribute>(false)?.AvatarModel;

        public abstract string[] Outputs { get; }
        public abstract string[] Inputs { get; }

        public virtual EntitySignalCapabilities SignalCapabilities
        {
            get
            {
                var capabilities = EntitySignalCapabilities.Lifecycle;
                var hasEnable = false;
                var hasDisable = false;
                foreach (var input in Inputs ?? Array.Empty<string>())
                {
                    hasEnable |= EntitySignalNameUtility.Matches(input, "enable");
                    hasDisable |= EntitySignalNameUtility.Matches(input, "disable");
                }

                if (hasEnable && hasDisable)
                    capabilities |= EntitySignalCapabilities.Operational;
                return capabilities;
            }
        }

        public EntitySignalContract SignalContract
        {
            get
            {
                // Signal schemas are stable during an activation; authoring always reads current marker data.
                if (!Application.isPlaying)
                    return EntitySignalContractBuilder.FromMarker(this).Build();

                return _runtimeSignalContract ??= EntitySignalContractBuilder.FromMarker(this).Build();
            }
        }

        protected virtual bool DefaultIgnoreFromNavMeshBuild => false;

        private void OnEnable()
        {
            _runtimeSignalContract = null;
            var g = Guid;
            if (g == Guid.Empty)
            {
                SdkConsoleBridge.Error(
                    $"[EntityMarker] Missing internal ID on '{name}'. Re-save or revalidate the scene before play.");
                return;
            }

            ByGuid[g] = this;
        }

        private void OnDisable()
        {
            var g = Guid;
            if (g == Guid.Empty) return;
            if (ByGuid.TryGetValue(g, out var m) && m == this)
                ByGuid.Remove(g);
        }

#if UNITY_EDITOR
        public virtual bool ShowAuthoringProperty(string propertyName) => true;
        public virtual void ApplyAuthoringChanges() { }
#endif

        public virtual void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
        }

        public static bool TryGet(Guid g, out EntityMarker marker)
        {
            return ByGuid.TryGetValue(g, out marker);
        }

        public static string GetEntityClassName(Type markerType)
        {
            if (markerType == null)
                return null;

            if (EntityClassByType.TryGetValue(markerType, out var cached))
                return cached;

            var attribute = markerType.GetCustomAttribute<EntityClassAttribute>(false);
            var className = attribute?.ClassName?.Trim();
            EntityClassByType[markerType] = string.IsNullOrWhiteSpace(className) ? null : className;
            return EntityClassByType[markerType];
        }

        public static string GetIconPath(Type markerType)
        {
            if (markerType == null)
                return null;

            if (IconPathByType.TryGetValue(markerType, out var cached))
                return cached;

            var resolved = EntityIdConvention.GetIconPath(GetEntityClassName(markerType));
            IconPathByType[markerType] = resolved;
            return resolved;
        }

#if UNITY_EDITOR
        // Compiled scenes keep their authored GUID after VertexObjectIdentity is stripped.
        [SerializeField, HideInInspector] private bool vertexAuthoredGuid;
        private static readonly Dictionary<string, EntityMarker> EditorOwners = new();
        private static readonly HashSet<int> PendingNavMeshSync = new();

        internal void PreserveAuthoredGuid()
        {
            vertexAuthoredGuid = true;
            EditorUtility.SetDirty(this);
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }

        public void ValidateVertexImport()
        {
            if (!TryGetComponent<Vertex.Unity.VertexObjectIdentity>(out var identity) || identity.ObjectId == Guid.Empty)
                throw new InvalidOperationException("Imported entities require their Vertex identity before validation.");
            OnValidate();
            SyncNavMeshModifier();
        }

        protected virtual void OnValidate()
        {
            _runtimeSignalContract = null;
            if (Application.isPlaying) return;

            var isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(this);

            if (TryGetComponent<Vertex.Unity.VertexObjectIdentity>(out var vertexIdentity) && vertexIdentity.ObjectId != Guid.Empty)
            {
                // Vertex owns these IDs. Importing, instantiating or revalidating must never randomize them.
                guid = vertexIdentity.ObjectId.ToString("N");
                vertexAuthoredGuid = true;
            }
            else if (!isPrefabAsset && !vertexAuthoredGuid)
            {
                var needsNew =
                    string.IsNullOrEmpty(guid)
                    || (PrefabUtility.GetCorrespondingObjectFromSource(this) is { } src &&
                        !string.IsNullOrEmpty(src.guid) && src.guid == guid)
                    || (EditorOwners.TryGetValue(guid, out var owner) && owner && owner != this);

                if (needsNew)
                {
                    guid = Guid.NewGuid().ToString("N");
                    EditorUtility.SetDirty(this);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(this);
                }

                if (!string.IsNullOrEmpty(guid))
                    EditorOwners[guid] = this;
            }

            if (!ignoreFromNavMeshInitialized)
            {
                ignoreNavMesh = DefaultIgnoreFromNavMeshBuild;
                ignoreFromNavMeshInitialized = true;
                EditorUtility.SetDirty(this);
                if (!isPrefabAsset)
                    PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }

            QueueNavMeshModifierSync();
        }

        private void QueueNavMeshModifierSync()
        {
            var instanceId = GetInstanceID();
            if (!PendingNavMeshSync.Add(instanceId))
                return;

            var marker = this;

            EditorApplication.delayCall += () =>
            {
                PendingNavMeshSync.Remove(instanceId);

                if (!marker || Application.isPlaying)
                    return;

                marker.SyncNavMeshModifier();
            };
        }

        private void SyncNavMeshModifier()
        {
            var isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(this);

            if (!ignoreNavMesh)
            {
                if (TryGetComponent(out NavMeshModifier existing))
                    if (existing.ignoreFromBuild)
                    {
                        existing.ignoreFromBuild = false;
                        EditorUtility.SetDirty(existing);
                        if (!isPrefabAsset)
                            PrefabUtility.RecordPrefabInstancePropertyModifications(existing);
                    }

                return;
            }

            if (!TryGetComponent(out NavMeshModifier modifier))
            {
                modifier = Undo.AddComponent<NavMeshModifier>(gameObject);
                EditorUtility.SetDirty(this);
                if (!isPrefabAsset)
                    PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }

            if (!modifier.ignoreFromBuild)
            {
                modifier.ignoreFromBuild = true;
                EditorUtility.SetDirty(modifier);
                if (!isPrefabAsset)
                    PrefabUtility.RecordPrefabInstancePropertyModifications(modifier);
            }
        }
#endif
    }

    /// <summary>
    ///     Generic marker that auto-populates Inputs/Outputs from enum types.
    /// </summary>
    public abstract class EntityMarker<TInput, TOutput> : EntityMarker
        where TInput : struct, Enum
        where TOutput : struct, Enum
    {
        public override string[] Inputs => SignalNames<TInput>.Array;
        public override string[] Outputs => SignalNames<TOutput>.Array;
    }
}
