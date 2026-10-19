using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vertex.Contracts;
using Vertex.Unity.Editor;

namespace Vellocet.Sdk.Editor.Entities
{
    internal static class DoorGameProperties
    {
        private static object Settings(EntityMarker marker)
        {
            if (marker is not DoorMarker || SdkGame.Current.DoorSettingsComponentType is not { } type)
                return null;
            var component = marker.GetComponent(type);
            if (!component)
                component = marker.gameObject.AddComponent(type);
            var configuration = VertexEntityCatalogExporter.SerializedFields(type).Single();
            return configuration.GetValue(component);
        }

        internal static IEnumerable<EntityPropertyDefinition> Describe(EntityMarker marker)
        {
            var settings = Settings(marker);
            if (settings == null)
                yield break;
            foreach (var field in VertexEntityCatalogExporter.SerializedFields(settings.GetType()))
            {
                var definition = VertexEntityCatalogExporter.Describe(field,
                    "game_" + VertexEntityCatalogExporter.PropertyId(field), field.Name, field.GetValue(settings));
                definition.Group = field.Name is "isProperty" or "property" ? "Property" : SdkGame.Current.DisplayName;
                if (field.Name == "property")
                    definition.VisibleWhen = new[]
                    { new EntityWarningCondition { Property = "game_is_property", Values = new object[] { true } } };
                yield return definition;
            }
        }

        internal static bool Apply(EntityMarker marker, string id, object value,
            IReadOnlyDictionary<Guid, EntityMarker> markers, VertexEntityBuildContext context)
        {
            if (!id.StartsWith("game_", StringComparison.Ordinal))
                return false;
            var settings = Settings(marker);
            if (settings == null)
                return false;
            var field = VertexEntityCatalogExporter.SerializedFields(settings.GetType())
                .Single(member => "game_" + VertexEntityCatalogExporter.PropertyId(member) == id);
            field.SetValue(settings, VertexEntityImporter.ConvertValue(field.FieldType, value, markers, context));
            return true;
        }
    }
}
