using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Editor.Maps;
using Vertex.Contracts;
using Vertex.Unity;
using Vertex.Unity.Editor;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Entities
{
    /// <summary>Explicit Unity authoring capture. Unsupported components are reported before publication.</summary>
    public static class VertexSceneCapture
    {
        public sealed class Options
        {
            public Guid MapId;
            public EntityCatalog Catalog;
            public Func<Object, string> AssetKey;
            public Func<MeshFilter, MapModel> Model;
            public Func<Component, string> GeneratedComponent;
            public Func<Transform, string> GeneratedSubtree;
            public Func<Component, MapContainer, bool> CaptureComponent;
            public Func<EntityMarker, string, object> ExtraProperty;
        }

        [Serializable] public sealed class Entry
        {
            public string path, component, action, detail;
        }
        public sealed class Result
        {
            public MapAsset Map;
            public readonly List<Entry> Entries = new();
            public bool Complete => Entries.All(e => e.action != "unsupported");
        }

        public static Result Capture(Scene scene, Options options)
        {
            if (!scene.IsValid() || !scene.isLoaded || options.MapId == Guid.Empty || options.Catalog == null)
                throw new ArgumentException("Capture needs a loaded scene, a new map ID and an entity catalog.");
            var result = new Result();
            var allTransforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(t => !EditorOnly(t)).ToArray();
            var generated = new HashSet<Transform>();
            foreach (var t in allTransforms)
                if (options.GeneratedSubtree?.Invoke(t) is { } reason)
                    foreach (var child in t.GetComponentsInChildren<Transform>(true))
                        if (generated.Add(child)) foreach (var component in child.GetComponents<Component>())
                            result.Entries.Add(new Entry { path = Path(child), component = component ? component.GetType().FullName : "missing",
                                action = "generated", detail = reason });
            var transforms = allTransforms.Where(t => !generated.Contains(t)).ToArray();
            var ids = transforms.ToDictionary(t => t, t => Id(options.MapId, GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject).ToString()));
            var markers = transforms.Select(t => t.GetComponent<EntityMarker>()).Where(m => m).ToArray();
            var remap = markers.Where(m => m.Guid != Guid.Empty).ToDictionary(m => m.Guid, m => ids[m.transform]);
            foreach (var authoring in transforms.Select(t => t.GetComponent<VertexEntityAuthoring>()).Where(a => a))
                remap[authoring.Read().ObjectId] = ids[authoring.transform];
            Guid Remap(Guid id) => remap.TryGetValue(id, out var replacement) ? replacement : throw new InvalidDataException("Unresolved captured object reference " + id);
            var containers = new List<MapContainer>();
            var objects = new List<MapObject>();
            foreach (var transform in transforms)
            {
                var go = transform.gameObject;
                var node = new MapContainer { Id = ids[transform], Name = go.name,
                    ParentId = transform.parent && ids.TryGetValue(transform.parent, out var parent) ? parent : null,
                    Position = Position(transform.position), Rotation = Rotation(transform.rotation),
                    State = !go.activeSelf ? new MapObjectState { Inactive = true } : null };
                containers.Add(node);
                var consumed = new HashSet<Component> { transform };
                try
                {
                    if (go.GetComponent<EntityMarker>() is { } marker && marker)
                    {
                        node.Entity = Entity(marker, options, ids);
                        if (!marker.enabled) node.State = (node.State ?? new MapObjectState()) with { MarkerDisabled = true };
                        consumed.Add(marker);
                        foreach (var companion in go.GetComponents<Component>().Where(c => c &&
                            (c is Light && marker is ILightEntityMarker || c is SplineContainer && marker is ISplineEntityMarker ||
                             c is Collider && marker is IBoundedEntityMarker || c.GetType().Name is "AnivDoorSettings" or "WorldText" or "SurfaceProvider")))
                            consumed.Add(companion);
                    }
                    if (go.GetComponent<VertexEntityAuthoring>() is { } authoring && authoring)
                    {
                        var data = authoring.Read();
                        node.Assembly = data.Assembly?.Remap(Remap);
                        node.Connections = data.Connections.Select(c => c.Remap(Remap)).ToArray(); consumed.Add(authoring);
                    }
                    if (go.GetComponent<Light>() is { } light && light && !consumed.Contains(light) && light.type != LightType.Directional)
                    {
                        node.Entity = RawEntity("env_light", transform, options.Catalog);
                        var properties = node.Entity.Properties.ToDictionary(p => p.Id, p => p.Value);
                        CaptureLight(light, properties, options.AssetKey);
                        properties["lighting"] = light.lightmapBakeType == LightmapBakeType.Baked ? 0 : light.lightmapBakeType == LightmapBakeType.Mixed ? 1 : 2;
                        node.Entity.Properties = properties.Select(p => new EntityPropertyValue { Id = p.Key, Value = p.Value }).ToArray();
                        consumed.Add(light);
                    }
                    var filter = go.GetComponent<MeshFilter>();
                    var renderer = go.GetComponent<MeshRenderer>();
                    var colliders = go.GetComponents<Collider>().Where(c => !consumed.Contains(c)).ToArray();
                    foreach (var empty in colliders.OfType<MeshCollider>().Where(c => c.sharedMesh && c.sharedMesh.triangles.Length == 0))
                    {
                        consumed.Add(empty);
                        Record(empty, "preserved", "Empty native collision mesh has no physical surface; retained its hierarchy");
                    }
                    colliders = colliders.Where(c => !consumed.Contains(c)).ToArray();
                    if (filter && filter.sharedMesh && renderer && renderer.sharedMaterials.Length > 0)
                    {
                        var model = options.Model?.Invoke(filter);
                        if (model != null)
                        {
                            if (renderer.sharedMaterials.Any(material => !material)) Record(renderer, "preserved", "Unassigned native material slots retained");
                            containers.Add(new MapContainer { Id = Id(node.Id, "model"), Name = "Visual", ParentId = node.Id, Model = model });
                            foreach (var collider in colliders) consumed.Add(collider);
                        }
                        else
                        {
                            var mesh = Mesh(filter.sharedMesh, filter.transform, renderer, options.AssetKey);
                            mesh.Id = Id(node.Id, "geometry"); mesh.ParentId = node.Id; mesh.Name = go.name;
                            mesh.Colliders = colliders.Select(c => Collider(c, options.AssetKey)).ToArray();
                            objects.Add(mesh);
                            if (mesh.Surfaces.Length == 0) Record(renderer, "preserved", "No drawable triangles; retained collision and hierarchy only");
                            foreach (var collider in colliders) consumed.Add(collider);
                        }
                        consumed.Add(filter); consumed.Add(renderer);
                    }
                    else
                    {
                        if (filter) consumed.Add(filter);
                        if (renderer) consumed.Add(renderer);
                        if (colliders.Length > 0)
                        {
                            objects.Add(new MapObject { Id = Id(node.Id, "collision"), ParentId = node.Id, Name = go.name,
                                Origin = Position(transform.position), Colliders = colliders.Select(c => Collider(c, options.AssetKey)).ToArray() });
                            foreach (var collider in colliders) consumed.Add(collider);
                        }
                    }
                }
                catch (Exception error) { Record(null, "unsupported", error.Message); }
                foreach (var component in go.GetComponents<Component>())
                {
                    if (!component) { Record(null, "unsupported", "Missing component"); continue; }
                    if (consumed.Contains(component)) { Record(component, "converted", ""); continue; }
                    try
                    {
                        if (options.CaptureComponent?.Invoke(component, node) == true) Record(component, "converted", "Game profile binding");
                        else if (options.GeneratedComponent?.Invoke(component) is { } reason) Record(component, "generated", reason);
                        else Record(component, "unsupported", "No capture binding");
                    }
                    catch (Exception error) { Record(component, "unsupported", error.Message); }
                }
                void Record(Component component, string action, string detail) => result.Entries.Add(new Entry
                { path = Path(transform), component = component ? component.GetType().FullName : "capture", action = action, detail = detail });
            }
            result.Map = new MapAsset { MapId = options.MapId, MetersPerUnit = 1, LightmapBake = CaptureLightmapBake(scene),
                Containers = containers.ToArray(), Objects = objects.ToArray() };
            return result;
        }

        private static string CaptureLightmapBake(Scene scene)
        {
            var settings = Lightmapping.GetLightingSettingsForScene(scene);
            if (!settings) return null;
            if (!settings.bakedGI) return "None";
            foreach (var quality in new[] { MapLightmapQuality.Low, MapLightmapQuality.Medium, MapLightmapQuality.High })
                if (settings == AssetDatabase.LoadAssetAtPath<LightingSettings>(MapLightingSettingsUtility.GetAssetPath(quality)))
                    return quality.ToString();
            return null;
        }

        private static MapEntity Entity(EntityMarker marker, Options options, IReadOnlyDictionary<Transform, Guid> ids)
        {
            var entity = RawEntity(marker.EntityClassName, marker.transform, options.Catalog);
            var definition = options.Catalog.Entities.Single(t => t.Id == marker.EntityClassName);
            if (definition.Geometry == "bounds" || marker is AmbientMarker { IsVolumetric: true })
                entity.Position = Position(marker.transform.position + marker.transform.up * (Mathf.Abs(marker.transform.lossyScale.y) / 2));
            var fields = VertexEntityCatalogExporter.SerializedFields(marker.GetType()).ToDictionary(VertexEntityCatalogExporter.PropertyId);
            var values = entity.Properties.ToDictionary(p => p.Id, p => p.Value);
            foreach (var property in definition.Properties)
            {
                if (marker is LabelMarker)
                {
                    var label = ((LabelMarker)marker).Label;
                    if (property.Id == "label") { values[property.Id] = label.text; continue; }
                    if (property.Id == "color") { values[property.Id] = new object[] { (double)label.color.r, (double)label.color.g, (double)label.color.b, (double)label.color.a }; continue; }
                    if (property.Id == "font_size") { values[property.Id] = (double)label.fontSize; continue; }
                    if (property.Id == "font")
                    {
                        var font = (label.GetType().GetProperty("FontAsset") ?? label.GetType().GetProperty("font"))?.GetValue(label) as Object;
                        values[property.Id] = font ? options.AssetKey(font) : null; continue;
                    }
                    if (property.Id == "font_style")
                    {
                        values[property.Id] = Convert.ToInt32((label.GetType().GetProperty("FontStyle") ?? label.GetType().GetProperty("fontStyle"))?.GetValue(label)); continue;
                    }
                }
                if (property.Id == "spline_container" && marker is ISplineEntityMarker) continue;
                if (property.Id == "arguments" && marker is LogicScriptMarker script)
                { values[property.Id] = script.Arguments.Select(a => (object)(a.Name + "=" + a.Expression)).ToArray(); continue; }
                if (fields.TryGetValue(property.Id, out var field)) values[property.Id] = Value(field.FieldType, field.GetValue(marker), options.AssetKey, ids);
                else if (property.Id.StartsWith("game_", StringComparison.Ordinal))
                {
                    var settingsType = SdkGame.Current.DoorSettingsComponentType;
                    var settingsComponent = settingsType != null ? marker.GetComponent(settingsType) : null;
                    if (settingsComponent)
                    {
                        var configuration = VertexEntityCatalogExporter.SerializedFields(settingsType).Single().GetValue(settingsComponent);
                        var member = VertexEntityCatalogExporter.SerializedFields(configuration.GetType()).Single(f => "game_" + VertexEntityCatalogExporter.PropertyId(f) == property.Id);
                        values[property.Id] = Value(member.FieldType, member.GetValue(configuration), options.AssetKey, ids);
                    }
                }
                if (options.ExtraProperty != null)
                {
                    var extra = options.ExtraProperty(marker, property.Id);
                    if (extra != null) values[property.Id] = extra;
                }
            }
            if (marker is ILightEntityMarker) CaptureLight(marker.GetComponent<Light>(), values, options.AssetKey);
            if (marker is IBoundedEntityMarker)
            {
                var collider = marker.GetComponent<Collider>();
                if (collider)
                {
                    values["volume_layer"] = LayerMask.LayerToName(marker.gameObject.layer);
                    values["volume_enabled"] = collider.enabled; values["volume_is_trigger"] = collider.isTrigger;
                    values["volume_include_layers"] = collider.includeLayers.value; values["volume_exclude_layers"] = collider.excludeLayers.value;
                    values["volume_contact_offset"] = (double)collider.contactOffset;
                }
                if (marker.GetComponent<BoxCollider>() is { } box && box)
                { values["volume_shape"] = "box"; values["volume_center"] = Vector(box.center); values["volume_size"] = new object[] { (double)box.size.x, (double)box.size.y, (double)box.size.z }; }
                else if (marker.GetComponent<SphereCollider>() is { } sphere && sphere)
                { values["volume_shape"] = "sphere"; values["volume_center"] = Vector(sphere.center); values["volume_radius"] = (double)sphere.radius; }
            }
            if (marker is ISplineEntityMarker)
            {
                var spline = (SplineContainer)fields["spline_container"].GetValue(marker);
                values["spline_container"] = spline.Spline.Select(k => (object)Vector(Local(k.Position))).ToArray();
                values["path_closed"] = spline.Spline.Closed;
                values["path_smooth"] = true;
                values["path_splines"] = spline.Splines.Select(s => (object)new object[]
                {
                    Pair("closed", s.Closed), Pair("knots", s.Select((k, i) => (object)new object[]
                    {
                        Pair("position", Vector(Local(k.Position))), Pair("tangent_in", Vector(Direction(k.TangentIn))),
                        Pair("tangent_out", Vector(Direction(k.TangentOut))),
                        Pair("rotation", QuaternionValue(Quaternion.Inverse(marker.transform.rotation) * spline.transform.rotation * new Quaternion(k.Rotation.value.x, k.Rotation.value.y, k.Rotation.value.z, k.Rotation.value.w))),
                        Pair("mode", (int)s.GetTangentMode(i)), Pair("tension", (double)s.GetAutoSmoothTension(i))
                    }).ToArray()), Pair("speed_mult", s.TryGetFloatData("speedMult", out var speed) ? (double)speed.DefaultValue : 1d),
                    Pair("stamina_mult", s.TryGetFloatData("staminaMult", out var stamina) ? (double)stamina.DefaultValue : 1d)
                }).ToArray();
                Vector3 Local(Vector3 p) => Quaternion.Inverse(marker.transform.rotation) * (spline.transform.TransformPoint(p) - marker.transform.position);
                Vector3 Direction(Vector3 p) => Vector3.Scale(p, spline.transform.lossyScale);
            }
            entity.Properties = values.Select(p => new EntityPropertyValue { Id = p.Key, Value = p.Value }).ToArray();
            entity.Validate(options.Catalog);
            return entity;
        }

        public static MapEntity RawEntity(string type, Transform transform, EntityCatalog catalog) => new()
        {
            SdkId = catalog.SdkId, TypeId = type, Position = Position(transform.position), Rotation = Rotation(transform.rotation),
            // Unity lights use range/area settings; a flattened parent scale does not remove their volume.
            Size = type == "env_light" ? new Vector3Data(1, 1, 1) :
                new Vector3Data(Math.Abs(transform.lossyScale.x), Math.Abs(transform.lossyScale.y), Math.Abs(transform.lossyScale.z)),
            Properties = catalog.Entities.Single(t => t.Id == type).Properties.Select(p => new EntityPropertyValue { Id = p.Id, Value = p.DefaultValue }).ToArray()
        };

        public static object Value(Type type, object value, Func<Object, string> key, IReadOnlyDictionary<Transform, Guid> ids)
        {
            if (type.IsArray) return ((Array)value ?? Array.Empty<object>()).Cast<object>().Select(v => Value(type.GetElementType(), v, key, ids)).ToArray();
            if (value is EntityMarker marker) return ids[marker.transform].ToString();
            if (typeof(Object).IsAssignableFrom(type)) return value is Object asset && asset ? key(asset) : null;
            if (type.IsEnum) return Convert.ToInt32(value);
            if (value is Vector3 vector) return new object[] { (double)vector.x, (double)vector.y, (double)vector.z };
            if (value is Vector2 v2) return new object[] { (double)v2.x, (double)v2.y };
            if (value is Color color) return new object[] { (double)color.r, (double)color.g, (double)color.b, (double)color.a };
            if (VertexEntityCatalogExporter.Kind(type) == "record")
                return VertexEntityCatalogExporter.SerializedFields(type).Select(f => (object)Pair(VertexEntityCatalogExporter.PropertyId(f), Value(f.FieldType, f.GetValue(value), key, ids))).ToArray();
            return type == typeof(string) ? value ?? "" : value;
        }

        private static void CaptureLight(Light light, IDictionary<string, object> values, Func<Object, string> key)
        {
            values["light_type"] = (int)light.type; values["color"] = new object[] { (double)light.color.r, (double)light.color.g, (double)light.color.b, (double)light.color.a };
            values["intensity"] = (double)light.intensity; values["range"] = (double)light.range;
            values["spot_angle"] = (double)light.spotAngle; values["inner_spot_angle"] = (double)light.innerSpotAngle;
            values["bounce_intensity"] = (double)light.bounceIntensity; values["shadow_strength"] = (double)light.shadowStrength;
            values["shadow_bias"] = (double)light.shadowBias; values["shadow_normal_bias"] = (double)light.shadowNormalBias;
            values["shadow_near_plane"] = (double)light.shadowNearPlane; values["culling_mask"] = light.cullingMask;
            values["rendering_layer_mask"] = (double)light.renderingLayerMask; values["shadows"] = light.shadows != LightShadows.None;
            values["enabled"] = light.enabled; values["cookie"] = light.cookie ? key(light.cookie) : null;
            values["shadow_mode"] = (int)light.shadows;
            values["area_size"] = new object[] { (double)light.areaSize.x, (double)light.areaSize.y };
            if (light.useColorTemperature) throw new InvalidDataException("Color temperature capture requires a binding.");
        }

        public static MapObject Mesh(Mesh mesh, Transform transform, MeshRenderer renderer, Func<Object, string> key)
        {
            var positions = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv; var colors = mesh.colors;
            if (normals.Length != positions.Length || uv.Length != positions.Length) throw new InvalidDataException("Mesh has missing normals or UV0: " + mesh.name);
            var normalMatrix = transform.localToWorldMatrix.inverse.transpose;
            var mirrored = transform.localToWorldMatrix.determinant < 0;
            var surfaces = Enumerable.Range(0, Math.Min(mesh.subMeshCount, renderer.sharedMaterials.Length))
                .Select(i => new MapSurface { Material = key(renderer.sharedMaterials[i]), Indices = Winding(mesh.GetTriangles(i), mirrored) })
                .Where(surface => surface.Indices.Length > 0).ToArray();
            return new MapObject { Origin = Position(transform.position), Rendering = Rendering(renderer),
                Vertices = surfaces.Length == 0 ? Array.Empty<MapVertex>() : positions.Select((p, i) => new MapVertex { Position = Position(transform.TransformPoint(p)),
                    Normal = Position(normalMatrix.MultiplyVector(normals[i]).normalized), U = uv[i].x, V = uv[i].y, Blend = colors.Length == positions.Length ? colors[i].a : 0 }).ToArray(),
                Surfaces = surfaces,
                Lightmap = surfaces.Length == 0 ? null : new MapLightmap { GenerateUV2 = renderer.receiveGI == ReceiveGI.Lightmaps, ResolutionScale = Math.Max(.01, renderer.scaleInLightmap) } };
        }

        public static MapRenderSettings Rendering(MeshRenderer renderer)
        {
            var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
            return new MapRenderSettings { Disabled = !renderer.enabled, ShadowCasting = renderer.shadowCastingMode.ToString(),
                LightmapScale = renderer.scaleInLightmap,
                ReceiveShadows = renderer.receiveShadows, StaticShadowCaster = renderer.staticShadowCaster, ReceiveGI = renderer.receiveGI.ToString(),
                ContributeGI = flags.HasFlag(StaticEditorFlags.ContributeGI), Occluder = flags.HasFlag(StaticEditorFlags.OccluderStatic), Occludee = flags.HasFlag(StaticEditorFlags.OccludeeStatic) };
        }

        public static MapCollider Collider(Collider collider, Func<Object, string> key)
        {
            var transform = collider.transform;
            var scale = transform.lossyScale;
            scale = new Vector3(Math.Abs(scale.x), Math.Abs(scale.y), Math.Abs(scale.z));
            var result = new MapCollider { Settings = new MapColliderSettings { Disabled = !collider.enabled, IsTrigger = collider.isTrigger,
                Layer = LayerMask.LayerToName(collider.gameObject.layer), ContactOffset = collider.contactOffset,
                IncludeLayers = unchecked((uint)collider.includeLayers.value), ExcludeLayers = unchecked((uint)collider.excludeLayers.value), OverridePriority = collider.layerOverridePriority,
                Material = collider.sharedMaterial ? key(collider.sharedMaterial) : null } };
            if (collider is MeshCollider mesh)
            {
                if (!mesh.sharedMesh) throw new InvalidDataException("Mesh collider has no mesh.");
                result.Kind = mesh.convex ? "Convex" : "Exact";
                result.Settings.NativeConvexCooking = mesh.convex;
                result.Settings.CookingOptions = (int)mesh.cookingOptions;
                result.Vertices = mesh.sharedMesh.vertices.Select(p => Position(transform.TransformPoint(p))).ToArray();
                result.Indices = Winding(mesh.sharedMesh.triangles, transform.localToWorldMatrix.determinant < 0);
            }
            else if (collider is BoxCollider box)
            {
                result.Kind = "Box"; result.Center = Position(transform.TransformPoint(box.center)); result.Rotation = Rotation(transform.rotation);
                var size = Vector3.Scale(box.size, scale); result.Size = new Vector3Data(size.x, size.y, size.z);
            }
            else if (collider is SphereCollider sphere)
            {
                result.Kind = "Sphere"; result.Center = Position(transform.TransformPoint(sphere.center));
                result.Radius = sphere.radius * Mathf.Max(scale.x, scale.y, scale.z); result.Height = result.Radius * 2;
                result.Size = new Vector3Data(result.Height, result.Height, result.Height);
            }
            else throw new InvalidDataException("Unsupported collider type: " + collider.GetType().Name);
            return result;
        }

        public static Guid Id(Guid scope, string source) { using var sha = SHA256.Create(); return new Guid(sha.ComputeHash(Encoding.UTF8.GetBytes(scope.ToString("N") + "/" + source)).Take(16).ToArray()); }
        public static Vector3Data Position(Vector3 p) => new(p.x, p.y, -p.z);
        public static QuaternionData Rotation(Quaternion q) => new(-q.x, -q.y, q.z, q.w);
        private static object[] QuaternionValue(Quaternion q) => new object[] { (double)-q.x, (double)-q.y, (double)q.z, (double)q.w };
        private static object[] Vector(Vector3 p) => new object[] { (double)p.x, (double)p.y, (double)-p.z };
        private static object[] Pair(string name, object value) => new[] { name, value };
        private static int[] Winding(int[] indices, bool mirrored) { if (!mirrored) for (var i = 0; i < indices.Length; i += 3) (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]); return indices; }
        private static bool EditorOnly(Transform t) { for (; t; t = t.parent) if (t.CompareTag("EditorOnly") || (t.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0) return true; return false; }
        private static string Path(Transform t) => t.parent ? Path(t.parent) + "/" + t.name : t.name;
    }
}
