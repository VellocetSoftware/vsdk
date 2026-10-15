using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vertex.Contracts;
using Vertex.Unity.Editor;

namespace Vellocet.Sdk.Editor.Entities
{
    [InitializeOnLoad]
    public static class VertexEntityImporter
    {
        internal static string GeometryRole(Type type)
        {
            if (typeof(IMovingBrushEntityMarker).IsAssignableFrom(type) || typeof(IEntityImportGroup).IsAssignableFrom(type) || type == typeof(DamageableMarker) || type == typeof(ButtonMarker)) return "brushes";
            if (typeof(IBoundedEntityMarker).IsAssignableFrom(type)) return "bounds";
            return "none";
        }
        static VertexEntityImporter() => VertexSdkRegistry.Register("com.vellocet.sdk", Import);

        private static bool CanOwnEntity(Type owner, Type child) =>
            typeof(IEntityImportGroup).IsAssignableFrom(owner) ||
            typeof(IMovingBrushEntityMarker).IsAssignableFrom(owner) &&
            (child == typeof(AmbientMarker) || child == typeof(LightMarker) ||
             child == typeof(LabelMarker) || child == typeof(ParticleMarker) || child == typeof(TriggerHurtMarker) ||
             child == typeof(DecalMarker));

        public static void Import(VertexEntityBuildContext context)
        {
            var catalog = VertexEntityCatalogExporter.Create();
            var bindings = EntityDefinitionScanner.ScanMarkers().Where(info => !string.IsNullOrWhiteSpace(info.EntityClassName))
                .ToDictionary(info => info.EntityClassName, info => info.MarkerType, StringComparer.Ordinal);
            var containers = context.Map.Containers.ToDictionary(container => container.Id);
            var markers = new Dictionary<Guid, EntityMarker>();
            foreach (var source in context.Entities)
            {
                try
                {
                    source.Entity.Validate(catalog);
                    if (!bindings.TryGetValue(source.Entity.TypeId, out var binding))
                        throw new InvalidDataException($"Entity type '{source.Entity.TypeId}' has no Vertex import binding yet.");
                    for (var parent = source.ParentId; parent.HasValue; parent = containers[parent.Value].ParentId)
                        if (containers[parent.Value].Entity is { } owner &&
                            (owner.SdkId != source.Entity.SdkId || owner.TypeId != "logic_script" &&
                                (!bindings.TryGetValue(owner.TypeId, out var ownerType) || !CanOwnEntity(ownerType, binding))))
                            throw new InvalidDataException("Nested SDK entities need an explicit ownership binding. Move this entity outside its entity parent.");
                    var go = context.Objects[source.Id];
                    var marker = (EntityMarker)go.AddComponent(binding);
                    // AddComponent/Reset can change a marker's transform. The map owns the authored pose.
                    go.transform.localScale = Vector3.one;
                    go.transform.SetPositionAndRotation(VertexMapBuilder.Position(source.Entity.Position, context.Map.MetersPerUnit),
                        VertexMapBuilder.Rotation(source.Entity.Rotation));
                    marker.ValidateVertexImport();
                    markers.Add(source.Id, marker);
                }
                catch (Exception exception) { throw Error(source, exception); }
            }
            foreach (var source in context.Entities)
            {
                try
                {
                    var marker = markers[source.Id];
                    var fields = VertexEntityCatalogExporter.SerializedFields(marker.GetType()).ToDictionary(VertexEntityCatalogExporter.PropertyId);
                    foreach (var definition in catalog.Entities.Single(type => type.Id == source.Entity.TypeId).Properties)
                    {
                        var property = source.Entity.Properties.FirstOrDefault(value => value.Id == definition.Id);
                        var value = property != null ? property.Value : definition.DefaultValue;
                        if (VertexEntityProperties.Apply(marker, definition.Id, value, source, context)) continue;
                        var field = fields[definition.Id];
                        field.SetValue(marker, ConvertValue(field.FieldType, value, markers, context));
                    }
                    if (GeometryRole(marker.GetType()) == "brushes" && marker is not IEntityImportGroup &&
                        marker is not ButtonMarker { Model: not "" })
                    {
                        // Damageable's required root collider is only a legacy authoring default. Owned geometry is authoritative.
                        if (marker is DamageableMarker) marker.GetComponent<BoxCollider>().enabled = false;
                        if (!marker.GetComponentsInChildren<Collider>(true).Any(collider => collider.enabled))
                            throw new InvalidDataException("This entity needs owned collision. Parent its geometry/proxies under this entity.");
                        foreach (var transform in marker.GetComponentsInChildren<Transform>(true))
                            GameObjectUtility.SetStaticEditorFlags(transform.gameObject, 0);
                        foreach (var renderer in marker.GetComponentsInChildren<MeshRenderer>(true))
                        { renderer.staticShadowCaster = false; renderer.receiveGI = ReceiveGI.LightProbes; }
                    }
                    else
                    {
                        if (marker.transform.childCount != 0 && marker is not (LogicScriptMarker or IEntityImportGroup))
                            throw new InvalidDataException("Point/volume entities cannot own brush geometry. Use their authored bounds for volumes.");
                        if (GeometryRole(marker.GetType()) == "bounds" || marker is AmbientMarker { IsVolumetric: true })
                        {
                            var size = source.Entity.Size;
                            var scale = (float)context.Map.MetersPerUnit;
                            marker.transform.position -= marker.transform.up * ((float)size.Y * scale / 2);
                            marker.transform.localScale = new Vector3((float)size.X * scale, (float)size.Y * scale, (float)size.Z * scale);
                            // The SDK runtime creates its trigger collider. No blocking level collider is imported here.
                        }
                    }
                    marker.ValidateVertexImport();
                    if (marker is LightMarker light && light.GetComponent<Light>().type == LightType.Rectangle &&
                        (light.RequiresRuntimeEntity || light.Effect != LightMarker.LightEffect.None))
                        throw new InvalidDataException("Area lights are baked-only and cannot use scripting or effects. Choose Point or Spot.");
                    if (marker is LabelMarker label) label.CaptureVertexLabel();
                    if (marker is DecalMarker decal)
                    {
                        if (!decal.Material || !float.IsFinite(decal.Opacity) || decal.Opacity is < 0 or > 1 ||
                            !float.IsFinite(decal.DrawDistance) || decal.DrawDistance is < 1 or > 1000)
                            throw new InvalidDataException("A decal needs a material, opacity between 0 and 1, and draw distance between 1 and 1000 metres.");
                        var projector = marker.gameObject.AddComponent<UnityEngine.Rendering.Universal.DecalProjector>();
                        projector.material = decal.Material;
                        // Material.FindPass depends on the currently active render pipeline. Imports also
                        // run before the first camera and on headless build workers; inspect shader data.
                        if (!VertexAssetCatalogExporter.IsDecalMaterial(decal.Material))
                            throw new InvalidDataException("Choose a material with a supported URP decal shader.");
                        // Bounds follow the SDK bottom-centre convention; native projection is local +Z
                        // (local -Z in Vertex). No collider or network entity is required.
                        projector.scaleMode = UnityEngine.Rendering.Universal.DecalScaleMode.ScaleInvariant;
                        projector.size = marker.transform.localScale;
                        projector.pivot = Vector3.up * (projector.size.y / 2);
                        projector.fadeFactor = decal.Opacity;
                        projector.drawDistance = decal.DrawDistance;
                        projector.enabled = marker.enabled;
                    }
                    if (marker is ParticleMarker particle)
                    {
                        if (!particle.Effect) throw new InvalidDataException("Choose an effect prefab.");
                        if (!VertexAssetCatalogExporter.IsEffectPrefab(particle.Effect))
                            throw new InvalidDataException("Choose a particle or VFX prefab without enabled colliders. Ordinary model prefabs are not effects.");
                        if (particle.Effect.GetComponentsInChildren<Collider>(true).Any(collider => collider.enabled) ||
                            particle.Effect.GetComponentsInChildren<Component>(true).Any(component => component &&
                                component.GetType().FullName == "FishNet.Object.NetworkObject"))
                            throw new InvalidDataException("Effect prefabs cannot contain enabled colliders or network objects. Place solid scenery separately.");
                        var presentation = UnityEngine.Object.Instantiate(particle.Effect, particle.transform, false);
                        presentation.name = "Effect";
                        // The native root rotation/scale are asset defaults; its pivot is the entity's origin.
                        presentation.transform.localPosition = Vector3.zero;
                        presentation.transform.localScale = Vector3.Scale(presentation.transform.localScale, particle.EffectScale);
                        particle.SetPresentation(presentation);
                        var index = 0;
                        foreach (var transform in presentation.GetComponentsInChildren<Transform>(true))
                        {
                            GameObjectUtility.SetStaticEditorFlags(transform.gameObject, 0);
                            context.AddGeneratedAsset(source.Id, "effect:" + index, transform.gameObject);
                            var componentIndex = 0;
                            foreach (var component in transform.GetComponents<Component>())
                                if (component) context.AddGeneratedAsset(source.Id, "effect:" + index + ":" + componentIndex++, component);
                            index++;
                        }
                        foreach (var renderer in presentation.GetComponentsInChildren<MeshRenderer>(true))
                        { renderer.staticShadowCaster = false; renderer.receiveGI = ReceiveGI.LightProbes; }
                    }
                    context.AddGeneratedAsset(source.Id, "marker", marker);
                    foreach (var component in marker.GetComponents<Component>())
                        if (component != marker && component is not Transform && component is not Vertex.Unity.VertexObjectIdentity)
                            context.AddGeneratedAsset(source.Id, "component:" + component.GetType().FullName, component);
                }
                catch (Exception exception) { throw Error(source, exception); }
            }
            // Game contracts declare ownership without exposing their private runtime types.
            foreach (var marker in markers.Values.Where(value => value is IEntityImportOwner))
            {
                var owner = ((IEntityImportOwner)marker).ImportOwner;
                if (!owner) throw new InvalidDataException($"Entity '{marker.Guid}' must reference its owner.");
                marker.transform.SetParent(owner.transform, true);
            }
            Vellocet.Sdk.Editor.Maps.VertexMapPolicy.Apply(context.Map, context.Root);
        }

        private static object ConvertValue(Type type, object value, IReadOnlyDictionary<Guid, EntityMarker> markers, VertexEntityBuildContext context)
        {
            if (type.IsArray)
            {
                var values = (Array)value;
                var element = type.GetElementType();
                var result = Array.CreateInstance(element, values.Length);
                for (var i = 0; i < values.Length; i++) result.SetValue(ConvertValue(element, values.GetValue(i), markers, context), i);
                return result;
            }
            if (typeof(EntityMarker).IsAssignableFrom(type))
            {
                if (value == null) return null;
                if (!markers.TryGetValue(Guid.Parse((string)value), out var target) || !type.IsInstanceOfType(target))
                    throw new InvalidDataException("An entity reference is missing, belongs to another SDK, or has the wrong entity type.");
                return target;
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return VertexEntityProperties.ResolveAsset(type, value, context);
            if (type.IsEnum) return Enum.ToObject(type, Convert.ToInt32(value, CultureInfo.InvariantCulture));
            if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Color))
            {
                var values = (Array)value;
                float Number(int index) => Convert.ToSingle(values.GetValue(index), CultureInfo.InvariantCulture);
                if (type == typeof(Vector2)) return new Vector2(Number(0), Number(1));
                if (type == typeof(Vector3)) return new Vector3(Number(0), Number(1), Number(2));
                return new Color(Number(0), Number(1), Number(2), Number(3));
            }
            if (VertexEntityCatalogExporter.Kind(type) == "record")
            {
                var record = Activator.CreateInstance(type);
                var fields = VertexEntityCatalogExporter.SerializedFields(type).ToDictionary(VertexEntityCatalogExporter.PropertyId);
                foreach (var entry in ((Array)value).Cast<Array>())
                {
                    if (!fields.TryGetValue((string)entry.GetValue(0), out var field))
                        throw new InvalidDataException("Unknown structured property member.");
                    field.SetValue(record, ConvertValue(field.FieldType, entry.GetValue(1), markers, context));
                }
                return record;
            }
            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }

        private static InvalidDataException Error(MapContainer source, Exception exception) =>
            new($"Entity '{source.Name}' [{source.Id}] ({source.Entity.TypeId}): {exception.Message}", exception);
    }
}
