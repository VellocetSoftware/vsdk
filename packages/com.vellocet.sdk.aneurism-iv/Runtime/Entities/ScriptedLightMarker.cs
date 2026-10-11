using System;
using Entities.MapObjects;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("env_aniv_scripted_light")]
    [EntityAuthoring("Visuals", EntityPlacementKind.Point, "Animated Light")]
    [RequireComponent(typeof(Light))]
    public sealed class ScriptedLightMarker : EntityMarker, ILightEntityMarker
    {
        public enum LightingMode { Dynamic, Mixed }
        [SerializeField] private LightingMode lighting;
        [SerializeField, HideInInspector] private bool lightingInitialized;
        [SerializeField] private LightStyle style;
        [SerializeField, Min(.001f)] private float cycleDuration = 5;
        public bool SupportsAreaLight => false;
        public LightStyle Style => style;
        public float CycleDuration => cycleDuration;
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (Application.isPlaying || !TryGetComponent<Light>(out var light)) return;
            if (!lightingInitialized)
            {
                lighting = light.lightmapBakeType == LightmapBakeType.Mixed ? LightingMode.Mixed : LightingMode.Dynamic;
                lightingInitialized = true;
            }
            var mode = lighting == LightingMode.Mixed ? LightmapBakeType.Mixed : LightmapBakeType.Realtime;
            if (light.lightmapBakeType == mode) return;
            light.lightmapBakeType = mode;
            UnityEditor.EditorUtility.SetDirty(light);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(light);
        }
#endif
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
