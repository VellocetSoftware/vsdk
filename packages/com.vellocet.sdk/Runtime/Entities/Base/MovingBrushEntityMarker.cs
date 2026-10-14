// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vellocet.Sdk.Entities.Base
{
    /// <summary>
    ///     Authoring contract for map geometry mounted to a runtime mover.
    /// </summary>
    public interface IMovingBrushEntityMarker : Vellocet.Framework.Runtime.Spatial.ISpatialDynamicOccluderSource
    {
    }

    public abstract class MovingBrushEntityMarker<TInput, TOutput> : EntityMarker<TInput, TOutput>,
        IMovingBrushEntityMarker
        where TInput : struct, Enum
        where TOutput : struct, Enum
    {
        protected override bool DefaultIgnoreFromNavMeshBuild => true;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (Application.isPlaying || !BrushTransform)
                return;

            var transforms = BrushTransform.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var current = transforms[i];
                if (!current || GameObjectUtility.GetStaticEditorFlags(current.gameObject) == 0)
                    continue;

                GameObjectUtility.SetStaticEditorFlags(current.gameObject, 0);
                EditorUtility.SetDirty(current.gameObject);
                if (!PrefabUtility.IsPartOfPrefabAsset(current.gameObject))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(current.gameObject);
            }

            var renderers = BrushTransform.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!renderer || !renderer.staticShadowCaster)
                    continue;

                renderer.staticShadowCaster = false;
                EditorUtility.SetDirty(renderer);
                if (!PrefabUtility.IsPartOfPrefabAsset(renderer))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }
#endif
        public virtual Transform BrushTransform => transform;
    }
}