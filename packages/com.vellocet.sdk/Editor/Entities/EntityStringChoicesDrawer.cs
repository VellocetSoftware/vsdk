#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Editor.Entities
{
    [CustomPropertyDrawer(typeof(EntityStringChoicesAttribute))]
    internal sealed class EntityStringChoicesDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var pairs = ((EntityStringChoicesAttribute)attribute).Choices;
            var ids = Enumerable.Range(0, pairs.Length / 2).Select(i => pairs[i * 2]).ToList();
            var names = Enumerable.Range(0, pairs.Length / 2).Select(i => pairs[i * 2 + 1]).ToList();
            var selected = ids.IndexOf(property.stringValue);
            if (selected < 0) { ids.Add(property.stringValue); names.Add("Unknown: " + property.stringValue); selected = ids.Count - 1; }
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            var next = EditorGUI.Popup(position, label.text, selected, names.ToArray());
            if (EditorGUI.EndChangeCheck()) property.stringValue = ids[next];
            EditorGUI.EndProperty();
        }
    }
}
#endif
