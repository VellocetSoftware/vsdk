using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Vertex.Contracts;
using Vertex.Unity.Editor;

namespace Vellocet.Sdk.Editor.Maps
{
    /// <summary>Writes native scene settings before baking; no runtime updater is needed.</summary>
    public static class VertexMapEnvironment
    {
        public static event Action<Scene, MapEnvironment> Applied;
        public static void Apply(Scene scene, MapEnvironment settings)
        {
            if (settings == null) return; // Maps without environment settings keep the scene defaults.
            settings.Validate();
            var sky = string.IsNullOrEmpty(settings.SkyMaterial) ? null : VertexAssetCatalogProfile.Resolve<Material>(settings.SkyMaterial);
            if (!string.IsNullOrEmpty(settings.SkyMaterial) && !sky)
                throw new InvalidDataException("The environment sky material could not be resolved.");
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidDataException("Environment settings need a loaded map scene.");
            var previous = SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(scene);
                var suns = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>(true))
                    .Where(light => light.type == LightType.Directional).ToArray();
                if (suns.Length != 1) throw new InvalidDataException("A configured environment requires exactly one directional sun.");
                var sun = suns[0];
                sun.color = Color(settings.SunColor);
                // Keep the scene's required sun present; zero intensity authors an unlit/night sky.
                sun.intensity = (float)settings.SunIntensity;
                sun.shadows = settings.SunShadows ? LightShadows.Soft : LightShadows.None;
                if (settings.SunUsePipelineBias.HasValue) sun.GetUniversalAdditionalLightData().usePipelineSettings = settings.SunUsePipelineBias.Value;
                sun.lightmapBakeType = settings.SunLighting == "Baked" ? LightmapBakeType.Baked :
                    settings.SunLighting == "Mixed" ? LightmapBakeType.Mixed : LightmapBakeType.Realtime;
                sun.transform.rotation = Quaternion.Euler((float)settings.SunPitch, -(float)settings.SunYaw, 0);
                RenderSettings.sun = sun;
                RenderSettings.skybox = sky;
                RenderSettings.ambientMode = settings.AmbientMode == "Gradient" ? AmbientMode.Trilight :
                    settings.AmbientMode == "Skybox" ? AmbientMode.Skybox : AmbientMode.Flat;
                RenderSettings.ambientSkyColor = Color(settings.AmbientSkyColor);
                RenderSettings.ambientEquatorColor = Color(settings.AmbientEquatorColor);
                RenderSettings.ambientGroundColor = Color(settings.AmbientGroundColor);
                RenderSettings.ambientIntensity = (float)settings.AmbientIntensity;
                if (settings.AmbientMode == "Flat") RenderSettings.ambientLight = Color(settings.AmbientColor);
                RenderSettings.fog = settings.FogMode != "Off";
                RenderSettings.fogMode = settings.FogMode == "Linear" ? FogMode.Linear :
                    settings.FogMode == "Exponential" ? FogMode.Exponential : FogMode.ExponentialSquared;
                RenderSettings.fogColor = Color(settings.FogColor);
                RenderSettings.fogDensity = (float)settings.FogDensity;
                RenderSettings.fogStartDistance = (float)settings.FogStart;
                RenderSettings.fogEndDistance = (float)settings.FogEnd;
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                RenderSettings.customReflectionTexture = null;
                RenderSettings.reflectionIntensity = (float)settings.ReflectionIntensity;
                RenderSettings.reflectionBounces = settings.ReflectionBounces;
                if (settings.VolumeProfile != null)
                {
                    var profile = VertexAssetCatalogProfile.Resolve<VolumeProfile>(settings.VolumeProfile);
                    if (!profile) throw new InvalidDataException("The environment volume profile could not be resolved.");
                    var volumes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Volume>(true))
                        .Where(v => v.isGlobal).ToArray();
                    if (volumes.Length > 1) throw new InvalidDataException("A Vertex-owned environment requires one global volume.");
                    var volume = volumes.SingleOrDefault();
                    if (!volume) { var go = new GameObject("Global Volume"); SceneManager.MoveGameObjectToScene(go, scene); volume = go.AddComponent<Volume>(); }
                    volume.isGlobal = true; volume.sharedProfile = profile; volume.weight = 1; volume.priority = 0;
                }
                Applied?.Invoke(scene, settings);
                EditorUtility.SetDirty(sun);
                EditorUtility.SetDirty(sun.transform);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            }
            finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }

        private static Color Color(string value)
        {
            if (!ColorUtility.TryParseHtmlString(value, out var color)) throw new InvalidDataException("Invalid environment color: " + value);
            return color;
        }
    }
}
