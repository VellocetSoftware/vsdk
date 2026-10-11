// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vellocet.Sdk.Tooling
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ToolVisual : MonoBehaviour
    {
        private const int IgnoreRaycastLayer = 2;
        private static readonly HashSet<ToolVisual> Active = new();
        private static readonly List<ToolVisual> ActiveBuffer = new(32);

        [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();
        [SerializeField] private bool includeChildren = true;
        [SerializeField] private bool visibleInEditMode = true;
        [SerializeField] private bool forceIgnoreRaycastLayer = true;

        private static bool RuntimeVisible { get; set; }

        private void Reset()
        {
            RebuildRendererCache();
            ApplyIgnoreRaycastLayer();
            ApplyVisibility();
        }

        private void OnEnable()
        {
            Active.Add(this);
            if (renderers == null || renderers.Length == 0)
                RebuildRendererCache();

            ApplyIgnoreRaycastLayer();
            ApplyVisibility();
        }

        private void OnDisable()
        {
            Active.Remove(this);
            ApplyVisibility(true);
        }

        private void OnValidate()
        {
            RebuildRendererCache();
            ApplyIgnoreRaycastLayer();
            ApplyVisibility();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            RuntimeVisible = false;
            Active.Clear();
            ActiveBuffer.Clear();
        }

        public static void SetRuntimeVisible(bool visible)
        {
            if (RuntimeVisible == visible)
                return;

            RuntimeVisible = visible;
            ApplyToAll();
        }

        private static void ApplyToAll()
        {
            ActiveBuffer.Clear();
            foreach (var visual in Active)
                ActiveBuffer.Add(visual);

            foreach (var visual in ActiveBuffer)
                if (visual)
                    visual.ApplyVisibility();
        }

        public void Refresh()
        {
            RebuildRendererCache();
            ApplyIgnoreRaycastLayer();
            ApplyVisibility();
        }

        private void RebuildRendererCache()
        {
            renderers = includeChildren
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();
        }

        private void ApplyIgnoreRaycastLayer()
        {
            if (!forceIgnoreRaycastLayer)
                return;

            if (renderers == null)
                return;

            foreach (var cachedRenderer in renderers)
            {
                if (!cachedRenderer)
                    continue;

                cachedRenderer.gameObject.layer = IgnoreRaycastLayer;
                cachedRenderer.shadowCastingMode = ShadowCastingMode.Off;
                cachedRenderer.receiveShadows = false;
            }
        }

        private void ApplyVisibility(bool? forceVisible = null)
        {
            var shouldRender = forceVisible ?? (!Application.isPlaying ? visibleInEditMode : RuntimeVisible);
            if (renderers == null)
                return;

            foreach (var cachedRenderer in renderers)
            {
                if (!cachedRenderer)
                    continue;

                cachedRenderer.forceRenderingOff = !shouldRender;
            }
        }
    }
}