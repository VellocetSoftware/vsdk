// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary
using UnityEngine;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("env_particle")]
    [EntityAuthoring("Environment", EntityPlacementKind.Point, "Particle effect")]
    public sealed class ParticleMarker : EntityMarker<ParticleMarker.Input, ParticleMarker.Output>
    {
        [SerializeField, Tooltip("Effect-only prefab. Simulation settings belong to this asset.")]
        private GameObject effect;
        [SerializeField, Tooltip("XYZ size multiplier for this placement, relative to the prefab's native size.")]
        private Vector3 effectScale = Vector3.one;
        [SerializeField] private bool startOn = true;
        [SerializeField, Tooltip("Enable external control. VSig references enable this automatically; unreferenced effects use no network entity.")]
        private bool scripted;
        [SerializeField, HideInInspector] private bool scriptReferenced;
        [SerializeField, HideInInspector] private GameObject presentation;

        public GameObject Effect => effect;
        public Vector3 EffectScale => effectScale;
        public GameObject Presentation => presentation;
        public bool StartOn => startOn;
        public bool RequiresRuntimeEntity => scripted || scriptReferenced;
        protected override bool DefaultIgnoreFromNavMeshBuild => true;

#if UNITY_EDITOR
        public void PrepareVertexPackage()
        {
            // Presentation is already instantiated. The source prefab is editor data, not a runtime dependency.
            effect = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void SetPresentation(GameObject value)
        {
            presentation = value;
            SetScriptReferenced(scriptReferenced);
        }

        public void SetScriptReferenced(bool value)
        {
            scriptReferenced = value;
            if (presentation)
            {
                // Scripted playback starts only after the client's replicated initial state arrives.
                presentation.SetActive(startOn && !RequiresRuntimeEntity);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(presentation);
            }
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }
#endif
        public override void ConfigureSignalContract(Vellocet.Sdk.Entities.Signals.EntitySignalContractBuilder builder)
        {
            builder.AddProperty("is_playing", Vellocet.Sdk.Entities.Signals.EntitySignalValueType.Bool, true, false,
                authority: Vellocet.Sdk.Entities.Signals.EntitySignalAuthority.Any);
            builder.AddState("state", nameof(Vellocet.Sdk.Entities.States.EffectEntityState));
        }
        public enum Input { Play, Stop, Restart, Trigger }
        public enum Output { Started, Stopped }
    }
}
