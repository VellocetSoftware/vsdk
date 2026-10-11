// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Utilities;

namespace Vellocet.Sdk.Editor
{
    [CustomPropertyDrawer(typeof(ReadOnly))]
    internal sealed class ReadOnlyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.PropertyField(rect, property, label);
            }
        }
    }
}
#endif