// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Entities.MapObjects;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("env_light")]
    [EntityAuthoring("Visuals", EntityPlacementKind.Point, "Light")]
    [RequireComponent(typeof(Light))]
    public sealed class LightMarker : EntityMarker<LightMarker.Input, LightMarker.Output>, ILightEntityMarker
    {
        public enum LightingMode { Baked, Mixed, Dynamic }
        public enum LightEffect { None, Flicker, Strobe, Candle, Custom }

        [SerializeField, Tooltip("Effects or scripts change Baked point/spot lights to Dynamic. Mixed keeps baked indirect light.")]
        private LightingMode lighting = LightingMode.Baked;
        [SerializeField] private LightEffect effect;
        [SerializeField, Tooltip("Enable for external scripts or mods. VSig enables this automatically. Creates a network entity.")]
        private bool scripted;
        [SerializeField, HideInInspector] private bool scriptReferenced;

        [SerializeField, Tooltip("Optional authored intensity curve, sampled locally at 10 Hz.")]
        private LightStyle style;
        [SerializeField, Min(.001f)] private float cycleDuration = 5f;
        public LightStyle Style => style;
        public float CycleDuration => cycleDuration;
        public bool SupportsAreaLight => true;
        public LightEffect Effect => effect;
        public bool RequiresRuntimeEntity => scripted || scriptReferenced;
        public LightingMode EffectiveLighting => GetComponent<Light>().type is LightType.Rectangle or LightType.Disc
            ? LightingMode.Baked
            : lighting == LightingMode.Baked && (RequiresRuntimeEntity || effect != LightEffect.None)
                ? LightingMode.Dynamic : lighting;

        private void Start()
        {
#if !UNITY_SERVER
            var light = GetComponent<Light>();
            if (effect != LightEffect.None && light.type is not (LightType.Rectangle or LightType.Disc) &&
                SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                gameObject.AddComponent<LightEffectPlayer>();
#endif
        }

#if UNITY_EDITOR
        private void Reset()
        {
            var light = GetComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.Soft;
            ApplyLighting();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (!Application.isPlaying) ApplyLighting();
        }

        public void SetScriptReferenced(bool value)
        {
            scriptReferenced = value;
            ApplyLighting();
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }

        public void ApplyLighting()
        {
            var light = GetComponent<Light>();
            if (!light) return;
            if (light.type is not (LightType.Point or LightType.Spot or LightType.Rectangle or LightType.Disc))
                light.type = LightType.Point;
            light.lightmapBakeType = EffectiveLighting switch
            {
                LightingMode.Dynamic => LightmapBakeType.Realtime,
                LightingMode.Mixed => LightmapBakeType.Mixed,
                _ => LightmapBakeType.Baked
            };
            UnityEditor.EditorUtility.SetDirty(light);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(light);
        }
#endif

        public override void ConfigureSignalContract(Vellocet.Sdk.Entities.Signals.EntitySignalContractBuilder builder)
        {
            builder.AddProperty("is_on", Vellocet.Sdk.Entities.Signals.EntitySignalValueType.Bool, true, false);
            builder.AddProperty("color", Vellocet.Sdk.Entities.Signals.EntitySignalValueType.String, true, false);
            builder.AddState("state", nameof(Vellocet.Sdk.Entities.States.LightEntityState));
        }

        public enum Input
        {
            Toggle,
            On,
            Off,
            Color
        }

        public enum Output
        {
            OnToggle
        }
    }
}
