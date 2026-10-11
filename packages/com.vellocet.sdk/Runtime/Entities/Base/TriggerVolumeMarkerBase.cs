// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine;
using Vellocet.Sdk.Tooling;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vellocet.Sdk.Entities.Base
{
    /// <summary>
    ///     Shared authoring/runtime-safe defaults for volume-style markers.
    /// </summary>
    public abstract class TriggerVolumeMarkerBase<TInput, TOutput> : EntityMarker<TInput, TOutput>,
        IToolVolumeVisualProvider, IBoundedEntityMarker
        where TInput : struct, Enum
        where TOutput : struct, Enum
    {
        private const string TriggerLayerName = "Trigger";
        private static readonly Vector3 DefaultAuthoringSize = new(7f, 2.5f, 7f);

        [field: Header("Filter")]
        [field: Tooltip("Physics layers allowed to count toward this volume.")]
        [field: SerializeField]
        public LayerMask Layers { get; protected set; }

        protected abstract Color GizmoFillColor { get; }
        protected abstract Color GizmoWireColor { get; }
        protected virtual Vector3 InitialAuthoringSize => DefaultAuthoringSize;
        protected virtual SdkLayerSet DefaultLayerSet => SdkLayerSet.Pawn;

        protected virtual void Reset()
        {
            transform.localScale = InitialAuthoringSize;
            ApplyDefaultLayers();
            ApplyTriggerObjectLayer();
        }

        public bool TryGetToolVolumeVisual(out ToolVolumeVisualData data)
        {
            var size = GetBoundsXYZ();
            var center = new Vector3(0f, size.y * 0.5f, 0f);
            var matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            data = new ToolVolumeVisualData(matrix, center, size, GizmoFillColor, GizmoWireColor);
            return isActiveAndEnabled && gameObject.activeInHierarchy;
        }

        /// <summary>
        ///     World-space volume size. A marker's native Transform scale is its sole authored size.
        /// </summary>
        public Vector3 GetBoundsXYZ()
        {
            var scale = transform.lossyScale;
            return new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }

        private bool ApplyDefaultLayers()
        {
            if (Layers.value != 0)
                return false;

            if (!LayerMaskDefaults.TryGetMask(DefaultLayerSet, out var defaultLayers))
                return false;

            Layers = defaultLayers;
            return true;
        }

        private bool ApplyTriggerObjectLayer()
        {
            var triggerLayer = LayerMask.NameToLayer(TriggerLayerName);
            if (triggerLayer < 0 || gameObject.layer == triggerLayer)
                return false;

            gameObject.layer = triggerLayer;
            return true;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            var markerChanged = ApplyDefaultLayers();
            if (markerChanged)
            {
                EditorUtility.SetDirty(this);
                if (!PrefabUtility.IsPartOfPrefabAsset(this))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }

            // Changing a GameObject layer synchronously from OnValidate causes Unity to
            // dispatch OnLayersChanged while validation is still running. Defer it to the
            // next editor update so both prefab assets and scene instances remain safe.
            EditorApplication.delayCall -= ApplyTriggerObjectLayerDelayed;
            EditorApplication.delayCall += ApplyTriggerObjectLayerDelayed;
        }

        private void ApplyTriggerObjectLayerDelayed()
        {
            EditorApplication.delayCall -= ApplyTriggerObjectLayerDelayed;
            if (!this || EditorApplication.isPlayingOrWillChangePlaymode || !ApplyTriggerObjectLayer())
                return;

            EditorUtility.SetDirty(gameObject);
            if (!PrefabUtility.IsPartOfPrefabAsset(gameObject))
                PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
        }
#endif
    }
}
