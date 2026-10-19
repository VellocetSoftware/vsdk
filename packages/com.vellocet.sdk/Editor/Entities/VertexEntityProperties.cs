using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using UnityEngine.Splines;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vertex.Contracts;
using Vertex.Unity.Editor;

namespace Vellocet.Sdk.Editor.Entities
{
    // Component references remain engine details. Vertex authors their useful data.
    internal static partial class VertexEntityProperties
    {
        private static EntityPropertyDefinition Field(string id, string name, string type, object value, bool array = false,
            double? min = null, double? max = null, string reference = null, string description = null, string group = null,
            EntityWarningCondition[] visibleWhen = null) => new()
        { Id = id, Name = name, Type = type, DefaultValue = value, IsArray = array, Minimum = min, Maximum = max, ReferenceType = reference, Description = description, Group = group, VisibleWhen = visibleWhen };

        internal static EntityPropertyDefinition Describe(EntityMarker marker, string id)
        {
            if (id == "effect" && marker is ParticleMarker) return new EntityPropertyDefinition
            {
                Id = id, Name = "Effect", Type = "asset", ReferenceType = "prefab", AssetRole = "effect",
                DefaultValue = null,
                Group = "Effect", Description = "Choose a particle or VFX prefab. Use Effect scale to resize this placement."
            };
            if (id == "effect_scale" && marker is ParticleMarker)
                return Field(id, "Effect scale", "vector3", new object[] { 1d, 1d, 1d }, min: .001, max: 1000,
                    group: "Effect", description: "XYZ size multiplier relative to the effect prefab; 1 preserves its native size.");
            if (id == "spline_container" && marker is ISplineEntityMarker)
                return new EntityPropertyDefinition { Id = id, Name = "Path points", Type = "vector3", IsArray = true,
                    DefaultValue = new object[] { new object[] { 0d, 0d, 0d }, new object[] { 0d, 0d, -4d } },
                    Group = "Path", Editor = "path", Space = "local", Units = "metres", MinimumItems = 2,
                    PathClosedProperty = "path_closed", PathSmoothProperty = "path_smooth",
                    Description = "Ordered points relative to the authored pivot. The generated path stays outside the moving entity. Drag points in a viewport or enter their coordinates below." };
            if (id == "label" && marker is LabelMarker) return new EntityPropertyDefinition
                { Id = id, Name = "Text", Type = "string", DefaultValue = "Label", Group = "Text", Editor = "multiline" };
            if (id == "arguments" && marker is LogicScriptMarker) return Field(id, "Module arguments", "string", Array.Empty<object>(), true,
                description: "Each entry is name=VSig expression, for example speed=4 or door=@entrance. Names must be unique.");
            return null;
        }

        internal static EntityWarningDefinition[] Warnings(EntityMarker marker) => marker is LightMarker ? new[]
        {
            new EntityWarningDefinition { Code = "LIGHT_EFFECT_BAKED",
                Message = "Light effects need Mixed or Dynamic lighting. Baked point/spot lights export as Dynamic when an effect is enabled. Mixed keeps its baked indirect light; area lights cannot animate.",
                When = new[] { new EntityWarningCondition { Property = "effect", Values = new object[] { 1, 2, 3 } },
                    new EntityWarningCondition { Property = "lighting", Values = new object[] { 0 } } } },
            new EntityWarningDefinition { Code = "LIGHT_SCRIPT_BAKED",
                Message = "Script-controlled lights need Mixed or Dynamic lighting. Baked point/spot lights export as Dynamic when scripted. Mixed keeps its baked indirect light; area lights cannot animate.",
                When = new[] { new EntityWarningCondition { Property = "scripted", Values = new object[] { true } },
                    new EntityWarningCondition { Property = "lighting", Values = new object[] { 0 } } } }
        } : null;

        internal static IEnumerable<EntityPropertyDefinition> Additional(EntityMarker marker)
        {
            foreach (var field in CapturedProperties(marker)) yield return field;
            if(marker is DoorMarker)
                foreach(Vellocet.Sdk.Entities.Doors.DoorSoundAction action in Enum.GetValues(typeof(Vellocet.Sdk.Entities.Doors.DoorSoundAction)))
                    yield return new EntityPropertyDefinition {Id="sound_"+Vertex.Contracts.EntitySignalNameUtility.Normalize(action.ToString()),Name=ObjectNames.NicifyVariableName(action.ToString()),Type="asset",ReferenceType="audio",DefaultValue=null,Group="Sound overrides",
                        VisibleWhen=new[]{new EntityWarningCondition {Property="override_sounds",Values=new object[]{true}}},Description="Overrides this action in the sound set. Choose a game audio event or add-on cue."};
            if (marker is ISplineEntityMarker)
            {
                yield return Field("path_smooth", "Smooth curves", "bool", false, group: "Path",
                    description: "Smooth the path through its points. Turn off for straight segments and sharp corners.");
                yield return Field("path_closed", "Closed path", "bool", false, group: "Path",
                    description: "Join the last point to the first. Movement and end behaviour are configured separately.");
            }
            if (marker is ILightEntityMarker lightMarker)
            {
                EntityWarningCondition[] LightTypes(params LightType[] types) => new[]
                    { new EntityWarningCondition { Property = "light_type", Values = types.Select(type => (object)(int)type).ToArray() } };
                yield return new EntityPropertyDefinition { Id = "light_type", Name = "Light type", Type = "enum", DefaultValue = (int)LightType.Point,
                    Choices = (lightMarker.SupportsAreaLight ? new[] { LightType.Point, LightType.Spot, LightType.Rectangle, LightType.Disc } :
                        new[] { LightType.Point, LightType.Spot }).Select(type => new EntityPropertyChoice
                        { Id = type == LightType.Rectangle ? "area" : type.ToString().ToLowerInvariant(),
                            Name = type == LightType.Rectangle ? "Area" : type.ToString(), Value = (int)type }).ToArray(),
                    Group = "Light", Description = lightMarker.SupportsAreaLight
                        ? "Point, Spot or Area. Area lights are baked-only and cannot use effects or scripting."
                        : "Point or Spot. This light animates at runtime." };
                yield return Field("color", "Color", "color", new object[] { 1d, 1d, 1d, 1d }, group: "Light");
                yield return Field("intensity", "Intensity", "number", 1d, min: 0, max: 1000000, group: "Light");
                yield return Field("range", "Range (metres)", "number", 10d, min: 0, max: 1000000, group: "Light", visibleWhen: LightTypes(LightType.Point, LightType.Spot));
                if (lightMarker.SupportsAreaLight) yield return Field("area_size", "Area size (metres)", "vector2", new object[] { 1d, 1d }, group: "Light", visibleWhen: LightTypes(LightType.Rectangle, LightType.Disc));
                yield return Field("spot_angle", "Spot angle", "number", 30d, min: 1, max: 179, group: "Light", visibleWhen: LightTypes(LightType.Spot));
                yield return Field("shadows", "Cast shadows", "bool", true, group: "Light");
                yield return Field("enabled", "Start on", "bool", true, group: "Light");
                yield return Field("cookie", "Cookie", "asset", null, reference: "texture", group: "Light");
            }
            if (marker is LabelMarker)
            {
                yield return Field("color", "Color", "color", new object[] { 1d, 1d, 1d, 1d }, group: "Text");
                yield return Field("font_size", "Font size", "number", 36d, min: .01, max: 1000, group: "Text");
            }
        }

        internal static bool Apply(EntityMarker marker, string id, object value, MapContainer source, VertexEntityBuildContext context)
        {
            if (ApplyCapturedProperty(marker, id, value)) return true;
            if(marker is DoorMarker door && id.StartsWith("sound_",StringComparison.Ordinal))
            {
                if(value==null)return true;
                var action=Enum.GetValues(typeof(Vellocet.Sdk.Entities.Doors.DoorSoundAction)).Cast<Vellocet.Sdk.Entities.Doors.DoorSoundAction>()
                    .Single(a=>"sound_"+Vertex.Contracts.EntitySignalNameUtility.Normalize(a.ToString())==id);
                var audioSource=context.ResolveAsset?.Invoke((string)value);
                var reference=DoorSoundAuthoring.Reference(audioSource);
                door.SetSoundOverride(action,reference);return true;
            }
            float Number() => Convert.ToSingle(value, CultureInfo.InvariantCulture);
            Color ColorValue() { var components = (Array)value; return new Color(Convert.ToSingle(components.GetValue(0)), Convert.ToSingle(components.GetValue(1)), Convert.ToSingle(components.GetValue(2)), Convert.ToSingle(components.GetValue(3))); }
            if (marker is ILightEntityMarker)
            {
                var light = marker.GetComponent<Light>();
                switch (id)
                {
                    case "light_type": light.type = (LightType)Convert.ToInt32(value); return true;
                    case "area_size":
                        var size = (Array)value;
                        var width = Convert.ToSingle(size.GetValue(0));
                        var height = Convert.ToSingle(size.GetValue(1));
                        if (!(width > 0) || !(height > 0) || float.IsInfinity(width) || float.IsInfinity(height))
                            throw new InvalidDataException("Area size must contain finite positive dimensions.");
                        light.areaSize = new Vector2(width, height); return true;
                    case "color": light.color = ColorValue(); return true;
                    case "intensity": light.intensity = Number(); return true;
                    case "range": light.range = Number(); return true;
                    case "spot_angle": light.spotAngle = Number(); return true;
                    case "shadows": light.shadows = (bool)value ? LightShadows.Soft : LightShadows.None; return true;
                    case "enabled": light.enabled = (bool)value; return true;
                    case "cookie": light.cookie = (Texture)ResolveAsset(typeof(Texture), value, context); return true;
                }
            }
            if (marker is LabelMarker)
            {
                var label = marker.GetComponent<TextMeshPro>();
                switch (id)
                {
                    case "label": label.text = (string)value; return true;
                    case "color": label.color = ColorValue(); return true;
                    case "font_size": label.fontSize = Number(); return true;
                }
            }
            if (id == "arguments" && marker is LogicScriptMarker)
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var args = ((Array)value).Cast<string>().Select(text =>
                {
                    var parts = text.Split(new[] { '=' }, 2); var name = parts[0].Trim();
                    if (parts.Length != 2 || name.Length == 0 || !name.All(c => char.IsLetterOrDigit(c) || c == '_') ||
                        char.IsDigit(name[0]) || !names.Add(name) || string.IsNullOrWhiteSpace(parts[1]))
                        throw new InvalidDataException("Module arguments must be unique name=expression entries.");
                    var argument = new LogicScriptMarker.Argument();
                    typeof(LogicScriptMarker.Argument).GetField("name", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(argument, name);
                    typeof(LogicScriptMarker.Argument).GetField("expression", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(argument, parts[1].Trim());
                    return argument;
                }).ToArray();
                Set(marker, id, args); return true;
            }
            if (marker is ISplineEntityMarker && id is "path_smooth" or "path_closed") return true;
            if (id != "spline_container" || marker is not ISplineEntityMarker) return false;
            Describe(marker, id).ValidateValue(value);
            var points = ((Array)value).Cast<Array>().Select(point => new Vector3Data(Convert.ToDouble(point.GetValue(0)),
                Convert.ToDouble(point.GetValue(1)), Convert.ToDouble(point.GetValue(2)))).ToArray();
            bool Flag(string key) => source.Entity.Properties.FirstOrDefault(property => property.Id == key)?.Value is true;
            var smooth = Flag("path_smooth");
            var closed = Flag("path_closed");
            var tangents = EntityPath.Tangents(points, closed);
            float3 ToUnity(Vector3Data vector) => new((float)vector.X, (float)vector.Y, -(float)vector.Z);
            var path = new GameObject("Vertex_path_" + source.Id.ToString("N"));
            path.transform.SetParent(context.Root.transform, false);
            path.transform.SetPositionAndRotation(marker.transform.position, marker.transform.rotation);
            var spline = path.AddComponent<SplineContainer>();
            spline.Spline = new Spline(points.Select((point, index) => new BezierKnot(ToUnity(point),
                -ToUnity(tangents[index]), ToUnity(tangents[index]), quaternion.identity)), closed);
            spline.Spline.SetTangentMode(smooth ? TangentMode.Broken : TangentMode.Linear);
            RestoreSplines(spline, source, points);
            Set(marker, id, spline);
            context.AddGeneratedAsset(source.Id, "path", path);
            context.AddGeneratedAsset(source.Id, "path-transform", path.transform);
            context.AddGeneratedAsset(source.Id, "path-spline", spline);
            return true;
        }

        private static void Set(EntityMarker marker, string id, object value) => VertexEntityCatalogExporter.SerializedFields(marker.GetType())
            .Single(field => VertexEntityCatalogExporter.PropertyId(field) == id).SetValue(marker, value);

        internal static UnityEngine.Object ResolveAsset(Type type, object value, VertexEntityBuildContext context)
        {
            if (value == null) return null;
            var asset = context.ResolveAsset?.Invoke((string)value);
            if (!asset || !type.IsInstanceOfType(asset)) throw new InvalidDataException($"Asset '{value}' is unavailable or incompatible with {type.Name}. Choose an asset from the installed game catalog.");
            return asset;
        }
    }
}
