using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Framework.Runtime.Spatial;
using Vertex.Contracts;

namespace Vellocet.Sdk.Editor.Maps
{
    // The SDK publishes its installed defaults and rejects author-side policy edits.
    public static class VertexBakePolicy
    {
        public static GameBakePolicy Create()
        {
            var settings = new List<BakeSetting>
            {
                Setting("occlusion.smallestOccluder", 5), Setting("occlusion.smallestHole", .25), Setting("occlusion.backfaceThreshold", 100)
            };
            var spatial = SdkGame.Current.SpatialBakeSettings.Sanitize();
            foreach (var field in typeof(SpatialOctreeBakeSettings).GetFields())
            {
                var value = field.GetValue(spatial);
                if (value is LayerMask mask) value = mask.value;
                settings.Add(Setting("spatial." + field.Name, value));
            }
            var presets = new List<LightmapPreset>();
            foreach (var quality in new[] { MapLightmapQuality.Low, MapLightmapQuality.Medium, MapLightmapQuality.High })
            {
                if (!MapLightingSettingsUtility.TryResolve(quality, false, out var asset, out _, out var error))
                    throw new InvalidDataException(error);
                var values = new List<BakeSetting>();
                using var serialized = new SerializedObject(asset);
                var property = serialized.GetIterator();
                var enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = property.propertyType == SerializedPropertyType.Generic;
                    if (property.name is "m_Name" or "m_ObjectHideFlags" or "m_CorrespondingSourceObject" or "m_PrefabInstance" or "m_PrefabAsset") continue;
                    object value = property.propertyType switch
                    {
                        SerializedPropertyType.Boolean => property.boolValue,
                        SerializedPropertyType.Integer or SerializedPropertyType.Enum => property.intValue,
                        SerializedPropertyType.Float => property.doubleValue,
                        SerializedPropertyType.String => property.stringValue.Length == 0 ? "(empty)" : property.stringValue,
                        SerializedPropertyType.ObjectReference => Reference(property.objectReferenceValue),
                        _ => null
                    };
                    if (value != null) values.Add(Setting(property.propertyPath, value));
                }
                presets.Add(new LightmapPreset { Name = quality.ToString(), Settings = values.OrderBy(v => v.Name, StringComparer.Ordinal).ToArray() });
            }
            return new GameBakePolicy { DefaultLightmaps = "Medium", GenerateLightmapUVs = true, LightmapScale = 1,
                Settings = settings.OrderBy(v => v.Name, StringComparer.Ordinal).ToArray(), Lightmaps = presets.ToArray() };
        }

        public static void Validate(GameBakePolicy policy)
        {
            if (policy == null || !policy.Serialize().SequenceEqual(Create().Serialize()))
                throw new InvalidDataException("The game's bake policy differs from the installed Vellocet SDK. Re-export/reload the game profile; occlusion, spatialization and lightmap values cannot be changed by map authors.");
        }

        public static string Lightmaps(MapAsset map, GameBakePolicy policy) => map.LightmapBake ?? policy.DefaultLightmaps;
        public static MapLightmapQuality Quality(string choice) => choice switch
        {
            "Low" => MapLightmapQuality.Low, "Medium" or "None" => MapLightmapQuality.Medium,
            "High" => MapLightmapQuality.High, _ => throw new InvalidDataException("Unknown lightmap bake option.")
        };

        public static void Apply(Scene scene, GameBakePolicy policy)
        {
            Validate(policy);
            var values = policy.Settings.ToDictionary(value => value.Name, value => value.Value);
            var previous = SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(scene);
                StaticOcclusionCulling.smallestOccluder = float.Parse(values["occlusion.smallestOccluder"], CultureInfo.InvariantCulture);
                StaticOcclusionCulling.smallestHole = float.Parse(values["occlusion.smallestHole"], CultureInfo.InvariantCulture);
                StaticOcclusionCulling.backfaceThreshold = float.Parse(values["occlusion.backfaceThreshold"], CultureInfo.InvariantCulture);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) renderer.scaleInLightmap = (float)policy.LightmapScale;
            }
            finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }

        private static BakeSetting Setting(string name, object value) => new() { Name = name, Value = Convert.ToString(value, CultureInfo.InvariantCulture) };
        private static string Reference(UnityEngine.Object value) => !value ? "null" :
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long id) ? guid + ":" + id :
            throw new InvalidDataException("Lighting presets must use persistent assets.");
    }
}
