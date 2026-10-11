// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Vellocet.Sdk.Editor.Workbench;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Tests
{
    public sealed class SdkWorkbenchWindowLifecycleTests
    {
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void Workbench_HasPersistentScriptIdentity()
        {
            var window = ScriptableObject.CreateInstance<SdkWorkbenchWindow>();
            try
            {
                var script = MonoScript.FromScriptableObject(window);
                Assert.That(script, Is.Not.Null);
                Assert.That(EditorUtility.IsPersistent(script), Is.True);
                Assert.That(script.GetClass(), Is.EqualTo(typeof(SdkWorkbenchWindow)));
                Assert.That(window.titleContent.text, Is.EqualTo("SDK Workbench"));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void LayoutSerialization_RestoresNavigationWithoutSerializingLivePanels()
        {
            var path = Path.Combine(Path.GetTempPath(), $"SdkWorkbenchLifecycle_{Guid.NewGuid():N}.wlt");
            var window = ScriptableObject.CreateInstance<SdkWorkbenchWindow>();
            Object[] restoredObjects = Array.Empty<Object>();
            try
            {
                var serialized = new SerializedObject(window);
                var workspace = serialized.FindProperty("_authorWorkspace");
                var scroll = serialized.FindProperty("_scroll");
                Assert.That(workspace, Is.Not.Null, "Workspace must survive layout restoration.");
                Assert.That(scroll, Is.Not.Null, "Scroll position must survive layout restoration.");
                var diagnosticsIndex = Array.IndexOf(workspace.enumNames, "Diagnostics");
                Assert.That(diagnosticsIndex, Is.GreaterThanOrEqualTo(0));
                workspace.enumValueIndex = diagnosticsIndex;
                scroll.vector2Value = new Vector2(0f, 144f);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                // This is the same Unity serializer used for editor layout snapshots.
                // Neither instance is shown, so this test cannot replace the user's dock.
                InternalEditorUtility.SaveToSerializedFileAndForget(new Object[] { window }, path, true);
                Object.DestroyImmediate(window);
                window = null;
                restoredObjects = InternalEditorUtility.LoadSerializedFileAndForget(path);
                var restored = restoredObjects.OfType<SdkWorkbenchWindow>().Single();
                var restoredState = new SerializedObject(restored);

                Assert.That(restoredState.FindProperty("_authorWorkspace").enumValueIndex,
                    Is.EqualTo(diagnosticsIndex));
                Assert.That(restoredState.FindProperty("_scroll").vector2Value,
                    Is.EqualTo(new Vector2(0f, 144f)));
                Assert.That(restored.titleContent.text, Is.EqualTo("SDK Workbench"));
                Assert.That(MonoScript.FromScriptableObject(restored).GetClass(),
                    Is.EqualTo(typeof(SdkWorkbenchWindow)));
                foreach (var panelField in new[] { "_addonContentPanel", "_spatialPanel" })
                    Assert.That(restoredState.FindProperty(panelField), Is.Null,
                        "Live callbacks and resources must be rebuilt, not serialized into layouts.");
            }
            finally
            {
                if (window)
                    Object.DestroyImmediate(window);
                foreach (var restored in restoredObjects)
                    if (restored)
                        Object.DestroyImmediate(restored);
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [TestCase("_spatialPanel", "EnsureSpatialPanel")]
        public void DisableEnableCycle_ReconnectsPanels(string fieldName, string initializeMethod)
        {
            var window = ScriptableObject.CreateInstance<SdkWorkbenchWindow>();
            try
            {
                Invoke(window, initializeMethod);
                var previous = ReadField(window, fieldName);
                Assert.That(ReadField(previous, "_subscribed"), Is.True);

                Invoke(window, "OnDisable");
                Invoke(window, "OnDisable"); // Teardown must be idempotent.
                Assert.That(ReadField(previous, "_subscribed"), Is.False);
                Assert.That(ReadField(window, fieldName), Is.Null);

                Invoke(window, "OnEnable");
                Invoke(window, initializeMethod);
                var current = ReadField(window, fieldName);
                Assert.That(current, Is.Not.SameAs(previous));
                Assert.That(ReadField(current, "_subscribed"), Is.True);
                Assert.That(ReadField(current, "_host"), Is.SameAs(window));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        private static object ReadField(object target, string fieldName) =>
            target.GetType().GetField(fieldName, InstanceMembers)?.GetValue(target);

        private static void Invoke(SdkWorkbenchWindow window, string methodName) =>
            typeof(SdkWorkbenchWindow).GetMethod(methodName, InstanceMembers)?.Invoke(window, null);
    }
}
