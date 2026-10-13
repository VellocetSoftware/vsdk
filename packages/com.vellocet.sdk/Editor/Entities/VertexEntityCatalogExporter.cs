#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Model = Vertex.Contracts;
using Object = UnityEngine.Object;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Vellocet.Sdk.Editor.Entities
{
    /// <summary>Exports existing SDK metadata; Unity serialization paths stay in the SDK.</summary>
    public static class VertexEntityCatalogExporter
    {
        [MenuItem("Tools/Vertex/Export Entity Catalog")]
        public static void ExportInteractive()
        {
            var path = EditorUtility.SaveFilePanel("Export Vertex entity catalog", "", "vellocet.vertexentities", "json");
            if (!string.IsNullOrEmpty(path)) Export(path);
        }

        public static void ExportBatch()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-vertexEntityCatalogOutput");
            if (index < 0 || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
                throw new ArgumentException("Pass -vertexEntityCatalogOutput followed by the output JSON path.");
            Export(args[index + 1]);
        }

        public static void Export(string path)
        {
            var bytes = Create().Serialize();
            var destination = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static string Icon(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            var package = PackageInfo.FindForAssetPath(assetPath);
            var path = package == null ? assetPath : Path.Combine(package.resolvedPath, assetPath.Substring(package.assetPath.Length + 1));
            return File.Exists(path) ? Convert.ToBase64String(File.ReadAllBytes(path)) : null;
        }

        public static Model.EntityCatalog Create()
        {
            var package = PackageInfo.FindForAssembly(typeof(EntityMarker).Assembly);
            var entities = new List<Model.EntityTypeDefinition>();
            var diagnostics = new List<Model.EntityCatalogDiagnostic>();
            foreach (var info in EntityDefinitionScanner.ScanMarkers())
            {
                if (string.IsNullOrWhiteSpace(info.EntityClassName))
                {
                    diagnostics.Add(new Model.EntityCatalogDiagnostic { Code = "ENTITY_CLASS_MISSING",
                        Message = $"{info.MarkerType.FullName} has no stable EntityClass ID and cannot be authored in Vertex." });
                    continue;
                }
                var host = new GameObject("Vertex metadata") { hideFlags = HideFlags.HideAndDontSave };
                host.SetActive(false);
                try
                {
                    var marker = (EntityMarker)host.AddComponent(info.MarkerType);
                    var contract = marker.SignalContract;
                    entities.Add(new Model.EntityTypeDefinition
                    {
                        Id = info.EntityClassName, Name = info.DisplayName, Category = info.Category,
                        IconPng = Icon(marker.IconPath),
                        Placement = info.PlacementKind.ToString().ToLowerInvariant(),
                        Geometry = VertexEntityImporter.GeometryRole(info.MarkerType),
                        SurfaceMaterialProperty = marker is global::Vellocet.Sdk.Entities.TriggerWaterMarker ? "surface_material" : null,
                        DecalMaterialProperty = marker is global::Vellocet.Sdk.Entities.DecalMarker ? "material" : null,
                        DecalOpacityProperty = marker is global::Vellocet.Sdk.Entities.DecalMarker ? "opacity" : null,
                        PreviewAssetProperty = marker is global::Vellocet.Sdk.Entities.ParticleMarker ? "effect" : null,
                        PreviewScaleProperty = marker is global::Vellocet.Sdk.Entities.ParticleMarker ? "effect_scale" : null,
                        Importable = VertexEntityImporter.GeometryRole(info.MarkerType) != null,
                        Capabilities = (Model.EntitySignalCapabilities)contract.Capabilities,
                        Signals = contract.Inputs.Concat(contract.Outputs).Concat(contract.Properties).Concat(contract.States)
                            .Select(member => new Model.EntitySignalDefinition
                            {
                                Name = member.NormalizedName, Kind = (Model.EntitySignalMemberKind)member.Kind,
                                ValueType = (Model.EntitySignalValueType)member.ValueType, Authority = (Model.EntitySignalAuthority)member.Authority,
                                Readable = member.Readable, Writable = member.Writable, TypeName = member.TypeName
                            }).OrderBy(member => member.Kind).ThenBy(member => member.Name, StringComparer.Ordinal).ToArray(),
                        Properties = Properties(marker, diagnostics),
                        Warnings = VertexEntityProperties.Warnings(marker)
                    });
                }
                finally { Object.DestroyImmediate(host); }
            }
            var catalog = new Model.EntityCatalog
            {
                SdkId = package.name, SdkVersion = package.version,
                Entities = entities.OrderBy(entity => entity.Id, StringComparer.Ordinal).ToArray(),
                Globals = Vellocet.Sdk.VSig.VSigGlobalEvents.Catalog.Signals.Select(signal => new Model.GlobalEventDefinition
                {
                    Source = signal.Source, Name = signal.Name,
                    Parameters = signal.Parameters.Select(parameter => new Model.SignalParameterDefinition
                    { Name = parameter.Name, TypeName = parameter.TypeName }).ToArray()
                }).OrderBy(signal => signal.Source, StringComparer.Ordinal).ThenBy(signal => signal.Name, StringComparer.Ordinal).ToArray(),
                Diagnostics = diagnostics.OrderBy(item => item.EntityId, StringComparer.Ordinal).ThenBy(item => item.PropertyId, StringComparer.Ordinal).ToArray()
            };
            catalog.Validate();
            return catalog;
        }

        private static Model.EntityPropertyDefinition[] Properties(EntityMarker marker, List<Model.EntityCatalogDiagnostic> diagnostics)
        {
            var result = new Dictionary<string, Model.EntityPropertyDefinition>(StringComparer.Ordinal);
            foreach (var field in VertexEntityProperties.Additional(marker)) { field.Validate(); result.Add(field.Id, field); }
            Type declaringType = null;
            string group = null;
            foreach (var field in SerializedFields(marker.GetType()))
            {
                if (declaringType != field.DeclaringType) { declaringType = field.DeclaringType; group = null; }
                group = field.GetCustomAttribute<HeaderAttribute>()?.header ?? group;
                var name = field.Name.StartsWith("<", StringComparison.Ordinal) ? field.Name.Substring(1, field.Name.IndexOf('>') - 1) : field.Name;
                var id = PropertyId(field);
                try
                {
                    // These fields are SDK-owned: canonical collision layers and the entity's owned break subtree.
                    if (field.FieldType == typeof(LayerMask) || marker is Vellocet.Sdk.Entities.DamageableMarker && id == "break_root") continue;
                    var definition = VertexEntityProperties.Describe(marker, id) ?? Describe(field, id, name, field.GetValue(marker));
                    definition.Group ??= group ?? "Properties";
                    definition.Validate();
                    if (!result.TryAdd(id, definition)) throw new ArgumentException("Multiple serialized fields produce the same property ID.");
                }
                catch (Exception exception) when (exception is ArgumentException || exception is InvalidDataException || exception is OverflowException)
                {
                    diagnostics.Add(new Model.EntityCatalogDiagnostic
                    {
                        Code = "ENTITY_PROPERTY_UNSUPPORTED", EntityId = marker.EntityClassName, PropertyId = id,
                        Message = $"{marker.EntityClassName}.{name}: {exception.Message} Add an SDK property binding before exposing this field in Vertex."
                    });
                }
            }
            return result.Values.ToArray();
        }

        internal static IEnumerable<FieldInfo> SerializedFields(Type type)
        {
            for (; type != null && type != typeof(object) && (typeof(EntityMarker).IsAssignableFrom(type) || !typeof(Object).IsAssignableFrom(type)); type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    if (!field.IsStatic && !field.IsInitOnly && !field.IsNotSerialized &&
                        (field.IsPublic || field.IsDefined(typeof(SerializeField)) || field.IsDefined(typeof(SerializeReference))) &&
                        !field.IsDefined(typeof(HideInInspector)) && !field.IsDefined(typeof(Vellocet.Sdk.Utilities.ReadOnly)))
                        yield return field;
        }

        internal static string PropertyId(FieldInfo field) => Model.EntitySignalNameUtility.Normalize(
            field.Name.StartsWith("<", StringComparison.Ordinal) ? field.Name.Substring(1, field.Name.IndexOf('>') - 1) : field.Name);

        private static Model.EntityPropertyDefinition Describe(FieldInfo field, string id, string name, object value, int depth = 0)
        {
            if (depth > 3) throw new ArgumentException("Structured properties exceed their nesting limit.");
            var type = field.FieldType;
            var array = type.IsArray;
            if (array) type = type.GetElementType();
            var kind = Kind(type);
            var definition = new Model.EntityPropertyDefinition
            {
                Id = id, Name = ObjectNames.NicifyVariableName(name), Type = kind, IsArray = array,
                Description = field.GetCustomAttribute<TooltipAttribute>()?.tooltip,
                Editor = kind == "string" && (field.IsDefined(typeof(TextAreaAttribute)) || field.IsDefined(typeof(MultilineAttribute))) ? "multiline" : null,
                DefaultValue = array ? ((Array)value ?? Array.Empty<object>()).Cast<object>().Select(item => Value(type, item)).ToArray() : Value(type, value)
            };
            if (kind == "record")
            {
                var defaults = Activator.CreateInstance(type);
                definition.Members = SerializedFields(type).Select(member =>
                    Describe(member, PropertyId(member), member.Name, member.GetValue(defaults), depth + 1)).ToArray();
            }
            // A base marker can reference multiple derived entity classes; the trusted importer enforces assignability.
            if (kind == "entity" && type.IsSealed) definition.ReferenceType = EntityMarker.GetEntityClassName(type);
            if (kind == "asset") definition.ReferenceType = AssetKind(type);
            if (type == typeof(Sprite)) definition.AssetRole = "sprite";
            if (type.IsEnum)
                definition.Choices = Enum.GetNames(type).Select(choice => new Model.EntityPropertyChoice
                {
                    Id = Model.EntitySignalNameUtility.Normalize(choice), Name = ObjectNames.NicifyVariableName(choice),
                    Value = Convert.ToInt32(Enum.Parse(type, choice))
                }).GroupBy(choice => choice.Value).Select(group => group.First()).ToArray();
            if (kind == "flags" && !definition.Choices.Any(choice => choice.Value == -1) &&
                (array ? ((Array)definition.DefaultValue).Cast<object>().Any(item => Convert.ToInt32(item) == -1) : Convert.ToInt32(definition.DefaultValue) == -1))
                definition.Choices = definition.Choices.Concat(new[] { new Model.EntityPropertyChoice
                    { Id = "everything", Name = "Everything", Value = -1 } }).ToArray();
            if (kind == "integer")
            {
                definition.Minimum = type == typeof(byte) || type == typeof(ushort) ? 0 : type == typeof(sbyte) ? sbyte.MinValue : type == typeof(short) ? short.MinValue : int.MinValue;
                definition.Maximum = type == typeof(byte) ? byte.MaxValue : type == typeof(ushort) ? ushort.MaxValue : type == typeof(sbyte) ? sbyte.MaxValue : type == typeof(short) ? short.MaxValue : int.MaxValue;
            }
            if (kind == "number" && type == typeof(float)) { definition.Minimum = -float.MaxValue; definition.Maximum = float.MaxValue; }
            var range = field.GetCustomAttribute<RangeAttribute>();
            var minimum = field.GetCustomAttribute<MinAttribute>();
            if (range != null) { definition.Minimum = range.min; definition.Maximum = range.max; }
            if (minimum != null) definition.Minimum = Math.Max(definition.Minimum ?? double.MinValue, minimum.min);
            return definition;
        }

        internal static string Kind(Type type)
        {
            if (type == typeof(bool)) return "bool";
            if (type == typeof(int) || type == typeof(short) || type == typeof(ushort) || type == typeof(byte) || type == typeof(sbyte)) return "integer";
            if (type == typeof(float) || type == typeof(double)) return "number";
            if (type == typeof(string)) return "string";
            if (type == typeof(Vector3)) return "vector3";
            if (type == typeof(Vector2)) return "vector2";
            if (type == typeof(Color)) return "color";
            if (type.IsEnum) return type.IsDefined(typeof(FlagsAttribute)) ? "flags" : "enum";
            if (typeof(EntityMarker).IsAssignableFrom(type)) return "entity";
            if (AssetKind(type) != null) return "asset";
            if (type.IsSerializable && !type.IsAbstract && !typeof(Object).IsAssignableFrom(type) &&
                (type.IsValueType || type.GetConstructor(Type.EmptyTypes) != null)) return "record";
            throw new ArgumentException($"Serialized type '{type.FullName}' has no portable property binding.");
        }

        private static string AssetKind(Type type)
        {
            if (typeof(Material).IsAssignableFrom(type)) return "material";
            if (typeof(Texture).IsAssignableFrom(type) || type == typeof(Sprite)) return "texture";
            if (type == typeof(Mesh)) return "model";
            if (type == typeof(GameObject)) return "prefab";
            if (type == typeof(AudioClip) || typeof(Vellocet.Stereoform.AudioEventReference).IsAssignableFrom(type)) return "audio";
            if (type == typeof(TextAsset)) return "text";
            if (typeof(ScriptableObject).IsAssignableFrom(type)) return "data";
            return null;
        }

        private static object Value(Type type, object value, int depth = 0)
        {
            if (depth > 8) throw new ArgumentException("Structured values exceed their nesting limit.");
            if (type.IsArray)
                return ((Array)value ?? Array.Empty<object>()).Cast<object>().Select(item => Value(type.GetElementType(), item, depth + 1)).ToArray();
            if (typeof(Object).IsAssignableFrom(type))
            {
                if (value is Object obj && obj) throw new ArgumentException("The default reference requires a stable catalog asset/entity ID.");
                return null;
            }
            if (type.IsEnum) return Convert.ToInt32(value);
            if (value is Vector3 vector) return new object[] { (double)vector.x, (double)vector.y, (double)vector.z };
            if (value is Vector2 vector2) return new object[] { (double)vector2.x, (double)vector2.y };
            if (value is Color color) return new object[] { (double)color.r, (double)color.g, (double)color.b, (double)color.a };
            if (Kind(type) == "record")
            {
                var record = value ?? Activator.CreateInstance(type);
                return SerializedFields(type).Select(member => (object)new object[] {
                    PropertyId(member), Value(member.FieldType, member.GetValue(record), depth + 1)
                }).ToArray();
            }
            return type == typeof(string) ? value ?? string.Empty : value;
        }
    }
}
#endif
