using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.Rendering.Universal;
using Vellocet.Sdk.Entities.Base;
using Vertex.Contracts;

namespace Vellocet.Sdk.Editor.Entities
{
    internal static partial class VertexEntityProperties
    {
        private static IEnumerable<EntityPropertyDefinition> CapturedProperties(EntityMarker marker)
        {
            foreach (var property in AudioProperties(marker)) yield return property;
            if (marker is ILightEntityMarker)
            {
                yield return Field("use_pipeline_bias", "Use pipeline shadow bias", "bool", true, group: "Light");
                yield return Field("shadow_resolution_tier", "Shadow resolution tier", "integer", 2, min: -1, max: 3, group: "Light");
                yield return Field("shadow_resolution", "Shadow resolution", "integer", -1, min: -1, max: 16384, group: "Light");
                yield return Field("shadow_custom_resolution", "Custom shadow resolution", "integer", -1, min: -1, max: 16384, group: "Light");
                yield return Field("soft_shadow_quality", "Soft shadow quality", "integer", 0, min: 0, max: 3, group: "Light");
                yield return Field("custom_shadow_layers", "Custom shadow layers", "bool", false, group: "Light");
                yield return Field("urp_rendering_layers", "Light rendering layers", "integer", 1, group: "Light");
                yield return Field("shadow_rendering_layers", "Shadow rendering layers", "integer", 1, group: "Light");
                yield return new EntityPropertyDefinition { Id = "shadow_mode", Name = "Shadow mode", Type = "enum", DefaultValue = -1, Group = "Light",
                    Choices = new[] { new EntityPropertyChoice { Id = "default", Name = "Use cast shadows", Value = -1 },
                        new EntityPropertyChoice { Id = "none", Name = "None", Value = 0 }, new EntityPropertyChoice { Id = "hard", Name = "Hard", Value = 1 },
                        new EntityPropertyChoice { Id = "soft", Name = "Soft", Value = 2 } } };
                yield return Field("inner_spot_angle", "Inner spot angle", "number", 21.802d, min: 0, max: 179, group: "Light");
                yield return Field("bounce_intensity", "Indirect multiplier", "number", 1d, min: 0, max: 100, group: "Light");
                yield return Field("shadow_strength", "Shadow strength", "number", 1d, min: 0, max: 1, group: "Light");
                yield return Field("shadow_bias", "Shadow bias", "number", .05d, min: 0, max: 10, group: "Light");
                yield return Field("shadow_normal_bias", "Shadow normal bias", "number", .4d, min: 0, max: 10, group: "Light");
                yield return Field("shadow_near_plane", "Shadow near plane", "number", .2d, min: .001, max: 100, group: "Light");
                yield return Field("culling_mask", "Culling mask", "integer", -1, group: "Light");
                yield return Field("rendering_layer_mask", "Rendering layer mask", "integer", 1, group: "Light");
            }
            if (marker is IBoundedEntityMarker)
            {
                yield return new EntityPropertyDefinition { Id = "volume_shape", Name = "Volume shape", Type = "string", DefaultValue = "default", Group = "Volume", Editor = "choice",
                    Choices = new[] { "default", "box", "sphere" }.Select(x => new EntityPropertyChoice { Id = x, Name = x }).ToArray() };
                yield return Field("volume_center", "Collider centre", "vector3", new object[] { 0d, .5d, 0d }, group: "Volume");
                yield return Field("volume_size", "Collider size", "vector3", new object[] { 1d, 1d, 1d }, min: .001, group: "Volume");
                yield return Field("volume_radius", "Sphere radius", "number", .5d, min: .001, max: 10000, group: "Volume");
                yield return Field("volume_layer", "Collision layer", "string", "Trigger", group: "Volume");
                yield return Field("volume_enabled", "Collider enabled", "bool", true, group: "Volume");
                yield return Field("volume_is_trigger", "Trigger", "bool", true, group: "Volume");
                yield return Field("volume_include_layers", "Include layers", "integer", 0, group: "Volume");
                yield return Field("volume_exclude_layers", "Exclude layers", "integer", 0, group: "Volume");
                yield return Field("volume_contact_offset", "Contact offset", "number", .01d, min: .00001, max: 1000, group: "Volume");
            }
            if (marker is ISplineEntityMarker)
            {
                var vector = new object[] { 0d, 0d, 0d };
                var knot = new EntityPropertyDefinition { Id = "knots", Name = "Knots", Type = "record", IsArray = true, DefaultValue = Array.Empty<object>(), Members = new[]
                {
                    Field("position", "Position", "vector3", vector), Field("tangent_in", "Incoming tangent", "vector3", vector),
                    Field("tangent_out", "Outgoing tangent", "vector3", vector),
                    Field("rotation", "Rotation quaternion", "number", new object[] { 0d, 0d, 0d, 1d }, array: true),
                    Field("mode", "Tangent mode", "integer", 0, min: 0, max: 4),
                    Field("tension", "Automatic tangent tension", "number", .33333334d, min: 0, max: 1)
                } };
                yield return new EntityPropertyDefinition { Id = "path_splines", Name = "Bezier splines", Type = "record", IsArray = true,
                    DefaultValue = Array.Empty<object>(), Group = "Path", Members = new[] { Field("closed", "Closed", "bool", false), knot,
                        Field("speed_mult", "Speed multiplier", "number", 1d, min: .001, max: 100),
                        Field("stamina_mult", "Stamina multiplier", "number", 1d, min: .001, max: 100) } };
            }
        }

        private static bool ApplyCapturedProperty(EntityMarker marker, string id, object value)
        {
            if (IsAudioProperty(marker, id)) return true;
            if (marker is ISplineEntityMarker && id == "path_splines" || marker is IBoundedEntityMarker && id.StartsWith("volume_", StringComparison.Ordinal)) return true;
            if (marker is not ILightEntityMarker) return false;
            var light = marker.GetComponent<Light>();
            switch (id)
            {
                case "use_pipeline_bias": light.GetUniversalAdditionalLightData().usePipelineSettings = (bool)value; return true;
                case "shadow_resolution_tier":
                {
                    using var settings = new SerializedObject(light.GetUniversalAdditionalLightData());
                    settings.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = Convert.ToInt32(value);
                    settings.ApplyModifiedPropertiesWithoutUndo(); return true;
                }
                case "shadow_resolution":
                {
                    // URP can serialize a pixel resolution here that the legacy enum setter rejects.
                    using var settings = new SerializedObject(light);
                    settings.FindProperty("m_Shadows.m_Resolution").intValue = Convert.ToInt32(value);
                    settings.ApplyModifiedPropertiesWithoutUndo(); return true;
                }
                case "shadow_custom_resolution": light.shadowCustomResolution = Convert.ToInt32(value); return true;
                case "soft_shadow_quality": light.GetUniversalAdditionalLightData().softShadowQuality = (SoftShadowQuality)Convert.ToInt32(value); return true;
                case "custom_shadow_layers": light.GetUniversalAdditionalLightData().customShadowLayers = (bool)value; return true;
                case "urp_rendering_layers": light.GetUniversalAdditionalLightData().renderingLayers = unchecked((uint)Convert.ToInt64(value)); return true;
                case "shadow_rendering_layers": light.GetUniversalAdditionalLightData().shadowRenderingLayers = unchecked((uint)Convert.ToInt64(value)); return true;
                case "shadow_mode": return true;
                case "inner_spot_angle": light.innerSpotAngle = Convert.ToSingle(value); return true;
                case "bounce_intensity": light.bounceIntensity = Convert.ToSingle(value); return true;
                case "shadow_strength": light.shadowStrength = Convert.ToSingle(value); return true;
                case "shadow_bias": light.shadowBias = Convert.ToSingle(value); return true;
                case "shadow_normal_bias": light.shadowNormalBias = Convert.ToSingle(value); return true;
                case "shadow_near_plane": light.shadowNearPlane = Convert.ToSingle(value); return true;
                case "culling_mask": light.cullingMask = Convert.ToInt32(value); return true;
                case "rendering_layer_mask": light.renderingLayerMask = Convert.ToInt32(value); return true;
                default: return false;
            }
        }

        internal static void RestoreVolume(EntityMarker marker, MapContainer source)
        {
            if (marker is ILightEntityMarker)
            {
                var light = marker.GetComponent<Light>();
                if (source.Entity.Properties.FirstOrDefault(p => p.Id == "shadow_mode")?.Value is { } shadow && Convert.ToInt32(shadow) >= 0)
                {
                    var castsShadows = source.Entity.Properties.FirstOrDefault(p => p.Id == "shadows")?.Value as bool?;
                    light.shadows = castsShadows == false ? LightShadows.None :
                        castsShadows == true && Convert.ToInt32(shadow) == 0 ? LightShadows.Soft : (LightShadows)Convert.ToInt32(shadow);
                }
                if (source.Entity.Properties.FirstOrDefault(p => p.Id == "inner_spot_angle")?.Value is { } inner)
                    light.innerSpotAngle = Convert.ToSingle(inner);
            }
            if (marker is not IBoundedEntityMarker) return;
            var values = source.Entity.Properties.ToDictionary(p => p.Id, p => p.Value);
            if (!values.TryGetValue("volume_shape", out var shape) || shape is not string kind || kind == "default") return;
            var center = Vector(values["volume_center"]);
            Collider collider;
            if (kind == "sphere")
            {
                var sphere = marker.gameObject.AddComponent<SphereCollider>();
                sphere.center = center; sphere.radius = Convert.ToSingle(values["volume_radius"]); collider = sphere;
            }
            else
            {
                var box = marker.GetComponent<BoxCollider>();
                if (!box) box = marker.gameObject.AddComponent<BoxCollider>();
                box.center = center; var size = Vector(values["volume_size"]); size.z = -size.z;
                box.size = size; collider = box;
            }
            collider.isTrigger = values.TryGetValue("volume_is_trigger", out var trigger) ? (bool)trigger : true;
            collider.enabled = values.TryGetValue("volume_enabled", out var enabled) ? (bool)enabled : true;
            if (values.TryGetValue("volume_layer", out var layer))
            {
                var number = LayerMask.NameToLayer((string)layer);
                if (number < 0) throw new System.IO.InvalidDataException("Missing volume collision layer: " + layer);
                collider.gameObject.layer = number;
            }
            if (values.TryGetValue("volume_include_layers", out var include)) collider.includeLayers = Convert.ToInt32(include);
            if (values.TryGetValue("volume_exclude_layers", out var exclude)) collider.excludeLayers = Convert.ToInt32(exclude);
            if (values.TryGetValue("volume_contact_offset", out var offset)) collider.contactOffset = Convert.ToSingle(offset);
        }

        private static void RestoreSplines(SplineContainer target, MapContainer source, Vector3Data[] points)
        {
            var value = source.Entity.Properties.FirstOrDefault(p => p.Id == "path_splines")?.Value as Array;
            if (value == null || value.Length == 0) return;
            var splines = new List<Spline>();
            foreach (var entry in value)
            {
                var fields = Record(entry);
                var knots = ((Array)fields["knots"]).Cast<object>().Select(Record).ToArray();
                var spline = new Spline(knots.Select(k =>
                {
                    var q = (Array)k["rotation"];
                    return new BezierKnot(Vector(k["position"]), Vector(k["tangent_in"]), Vector(k["tangent_out"]),
                        new quaternion(-Convert.ToSingle(q.GetValue(0)), -Convert.ToSingle(q.GetValue(1)), Convert.ToSingle(q.GetValue(2)), Convert.ToSingle(q.GetValue(3))));
                }), (bool)fields["closed"]);
                if (splines.Count == 0 && points.Length == spline.Count)
                    for (var i = 0; i < points.Length; i++)
                    {
                        var knot = spline[i]; knot.Position = new float3((float)points[i].X, (float)points[i].Y, -(float)points[i].Z);
                        spline.SetKnot(i, knot);
                    }
                spline.GetOrCreateFloatData("speedMult").DefaultValue = Convert.ToSingle(fields["speed_mult"]);
                spline.GetOrCreateFloatData("staminaMult").DefaultValue = Convert.ToSingle(fields["stamina_mult"]);
                splines.Add(spline);
            }
            target.Splines = splines;
            // SetTangentMode recomputes the curve, including old scenes with hand-adjusted automatic
            // knots. Restore the authored mode metadata separately so initial curve geometry is exact.
            using var serialized = new SerializedObject(target);
            var serializedSplines = serialized.FindProperty("m_Splines");
            for (var s = 0; s < value.Length; s++)
            {
                var knots = ((Array)Record(value.GetValue(s))["knots"]).Cast<object>().Select(Record).ToArray();
                var metadata = serializedSplines.GetArrayElementAtIndex(s).FindPropertyRelative("m_MetaData");
                metadata.arraySize = knots.Length;
                for (var i = 0; i < knots.Length; i++)
                {
                    var item = metadata.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative("Mode").intValue = Convert.ToInt32(knots[i]["mode"]);
                    item.FindPropertyRelative("Tension").floatValue = Convert.ToSingle(knots[i]["tension"]);
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Dictionary<string, object> Record(object value) => ((Array)value).Cast<Array>()
            .ToDictionary(pair => (string)pair.GetValue(0), pair => pair.GetValue(1));
        internal static Vector3 Vector(object value)
        {
            var v = (Array)value;
            return new Vector3(Convert.ToSingle(v.GetValue(0)), Convert.ToSingle(v.GetValue(1)), -Convert.ToSingle(v.GetValue(2)));
        }
    }
}
