// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Entities;

namespace Vellocet.Sdk.Editor.Entities
{
    [CustomPropertyDrawer(typeof(AmbientEventNameAttribute))]
    internal sealed class AmbientEventNameDrawer : PropertyDrawer
    {
        private const float RefreshButtonWidth = 22f;
        private const float IssueHeight = 38f;

        private static readonly GUIContent RefreshContent = EditorGUIUtility.IconContent(
            "Refresh",
            "Refresh ambient Events from the configured Wwise project.");

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var eventNames = AmbientEventCatalog.GetEventNames(out var error);
            return string.IsNullOrWhiteSpace(GetIssue(property, eventNames, error))
                ? EditorGUIUtility.singleLineHeight
                : EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + IssueHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var eventNames = AmbientEventCatalog.GetEventNames(out var error);
            var issue = GetIssue(property, eventNames, error);
            var lineRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginProperty(position, label, property);
            var valueRect = EditorGUI.PrefixLabel(lineRect, label);
            var refreshRect = new Rect(valueRect.xMax - RefreshButtonWidth, valueRect.y, RefreshButtonWidth,
                valueRect.height);
            var popupRect = new Rect(valueRect.x, valueRect.y,
                Mathf.Max(0f, valueRect.width - RefreshButtonWidth - 2f), valueRect.height);

            var currentValue = property.stringValue?.Trim() ?? string.Empty;
            var currentIndex = IndexOf(eventNames, currentValue);
            var missingCurrentValue = !string.IsNullOrWhiteSpace(currentValue) && currentIndex < 0;
            var choices = BuildChoices(eventNames, currentValue, missingCurrentValue);
            var popupIndex = missingCurrentValue ? choices.Length - 1 : currentIndex + 1;

            var previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var selectedIndex = EditorGUI.Popup(popupRect, Mathf.Max(0, popupIndex), choices);
            if (EditorGUI.EndChangeCheck())
                property.stringValue = selectedIndex <= 0
                    ? string.Empty
                    : selectedIndex <= eventNames.Count
                        ? eventNames[selectedIndex - 1]
                        : currentValue;

            EditorGUI.showMixedValue = previousMixedValue;

            if (GUI.Button(refreshRect, RefreshContent, EditorStyles.iconButton))
            {
                AmbientEventCatalog.Invalidate();
                GUI.changed = true;
            }

            if (!string.IsNullOrWhiteSpace(issue))
            {
                var issueRect = new Rect(position.x,
                    lineRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                    position.width,
                    IssueHeight);
                EditorGUI.HelpBox(issueRect, issue, MessageType.Warning);
            }

            EditorGUI.EndProperty();
        }

        private static GUIContent[] BuildChoices(IReadOnlyList<string> eventNames, string currentValue,
            bool includeMissingValue)
        {
            var choices = new GUIContent[eventNames.Count + 1 + (includeMissingValue ? 1 : 0)];
            choices[0] = new GUIContent("<None>");
            for (var i = 0; i < eventNames.Count; i++)
                choices[i + 1] = new GUIContent(eventNames[i]);

            if (includeMissingValue)
                choices[^1] = new GUIContent($"{currentValue} (not in catalog)");

            return choices;
        }

        private static string GetIssue(SerializedProperty property, IReadOnlyList<string> eventNames, string error)
        {
            if (eventNames.Count == 0)
                return string.IsNullOrWhiteSpace(error)
                    ? $"No Wwise Events beginning with '{AmbientMarker.EventNamePrefix}' are available."
                    : error;

            if (property.hasMultipleDifferentValues || string.IsNullOrWhiteSpace(property.stringValue))
                return string.Empty;

            return IndexOf(eventNames, property.stringValue.Trim()) >= 0
                ? string.Empty
                : $"'{property.stringValue}' is not in the compiled ambient Event catalog.";
        }

        private static int IndexOf(IReadOnlyList<string> values, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return -1;

            for (var i = 0; i < values.Count; i++)
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                    return i;

            return -1;
        }
    }
}
#endif