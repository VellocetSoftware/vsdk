// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using Vertex.VSig;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.VSig;
using Vellocet.Sdk.VSig.Runtime;

namespace Vellocet.Sdk.Editor.VSig.Inspectors
{
    [CustomEditor(typeof(VSigSceneRuntime))]
    internal sealed class VSigSceneRuntimeEditor : UnityEditor.Editor
    {
        private Vector2 _diagnosticScroll;
        private VSigEditorValidator.Result _lastResult;
        private bool _showRuntimeState = true;
        private Vector2 _traceScroll;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var runtime = (VSigSceneRuntime)target;
            var program = serializedObject.FindProperty("program");
            var includeInactive = serializedObject.FindProperty("includeInactiveMarkers");

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(program);
            }

            EditorGUILayout.PropertyField(includeInactive);

            EditorGUILayout.Space(8f);
            DrawActions(runtime, includeInactive);
            EditorGUILayout.Space(8f);
            DrawSummary(runtime, includeInactive.boolValue);
            DrawRuntimeState(runtime);
            DrawDiagnostics();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawActions(VSigSceneRuntime runtime, SerializedProperty includeInactive)
        {
            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                SdkEditorGui.DrawSectionHeader("VSig tools", "Inspect compiled scene signal data.");
                EditorGUILayout.Space(4f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Validate", SdkEditorGui.PrimaryButton, GUILayout.Height(28f)))
                    {
                        _lastResult = Validate(runtime, includeInactive.boolValue);
                        LogDiagnostics(_lastResult);
                    }

                    if (GUILayout.Button("Copy refs", SdkEditorGui.SecondaryButton, GUILayout.Height(28f)))
                        GUIUtility.systemCopyBuffer =
                            VSigEditorValidator.GenerateRefBlock(runtime.gameObject.scene, includeInactive.boolValue);

                    if (GUILayout.Button("Copy scaffold", SdkEditorGui.SecondaryButton, GUILayout.Height(28f)))
                        GUIUtility.systemCopyBuffer =
                            VSigEditorValidator.GenerateScaffold(runtime.gameObject.scene, includeInactive.boolValue);
                }

            }
        }

        private void DrawSummary(VSigSceneRuntime runtime, bool includeInactiveMarkers)
        {
            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                var compiled = runtime.Program;
                if (compiled == null)
                {
                    SdkEditorGui.DrawSectionHeader("Status: empty", "No compiled VSig program is embedded.");
                    return;
                }

                _lastResult ??= Validate(runtime, includeInactiveMarkers);
                var status = _lastResult.HasErrors ? "Invalid" : "Valid";
                SdkEditorGui.DrawSectionHeader($"Status: {status}",
                    $"{compiled.RefCount} refs, {compiled.UseCount} modules, {compiled.VariableCount} vars, {compiled.StateExportCount} states, {compiled.OutputCount} outputs, {compiled.HandlerCount} handlers, {compiled.ProcedureCount} procedures");

                if (Application.isPlaying && runtime.Context != null)
                    EditorGUILayout.LabelField("Runtime context", "Registered", SdkEditorGui.Body);
            }
        }

        private void DrawRuntimeState(VSigSceneRuntime runtime)
        {
            if (!Application.isPlaying || runtime.Context == null)
                return;

            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                var context = runtime.Context;
                var states = context.GetStateSnapshot();
                var tasks = context.GetActiveTasks();
                _showRuntimeState = EditorGUILayout.Foldout(_showRuntimeState,
                    $"Live State ({states.Count}) · Tasks ({tasks.Count})", true);
                if (!_showRuntimeState)
                    return;

                for (var i = 0; i < states.Count; i++)
                {
                    var state = states[i];
                    EditorGUILayout.LabelField(state.Name,
                        $"{state.Value}  [{state.TypeName}]", SdkEditorGui.Body);
                }

                if (tasks.Count > 0)
                    EditorGUILayout.LabelField("Scheduled", string.Join(", ", tasks), SdkEditorGui.Body);

                var trace = context.EventTrace;
                if (trace.Count == 0)
                    return;

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Recent events", SdkEditorGui.Muted);
                _traceScroll = EditorGUILayout.BeginScrollView(_traceScroll, GUILayout.MinHeight(54f),
                    GUILayout.MaxHeight(150f));
                for (var i = 0; i < trace.Count; i++)
                    EditorGUILayout.LabelField(trace[i], SdkEditorGui.Muted);
                EditorGUILayout.EndScrollView();
            }

            Repaint();
        }

        private void DrawDiagnostics()
        {
            if (_lastResult == null)
                return;

            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                SdkEditorGui.DrawSectionHeader($"Diagnostics ({_lastResult.Diagnostics.Count})");
                if (_lastResult.Diagnostics.Count == 0)
                {
                    EditorGUILayout.LabelField("No diagnostics.", SdkEditorGui.Muted);
                    return;
                }

                _diagnosticScroll = EditorGUILayout.BeginScrollView(_diagnosticScroll, GUILayout.MinHeight(72f),
                    GUILayout.MaxHeight(180f));
                for (var i = 0; i < _lastResult.Diagnostics.Count; i++)
                    DrawDiagnostic(_lastResult.Diagnostics[i]);
                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawDiagnostic(VSigDiagnostic diagnostic)
        {
            if (diagnostic == null)
                return;

            var type = diagnostic.Severity switch
            {
                VSigDiagnosticSeverity.Error => MessageType.Error,
                VSigDiagnosticSeverity.Warning => MessageType.Warning,
                _ => MessageType.Info
            };

            SdkEditorGui.DrawCallout(diagnostic.ToString(), type);
        }

        private static void LogDiagnostics(VSigEditorValidator.Result result)
        {
            if (result == null || result.Diagnostics.Count == 0)
            {
                Debug.Log("[VSig] Validation passed without diagnostics.");
                return;
            }

            for (var i = 0; i < result.Diagnostics.Count; i++)
            {
                var diagnostic = result.Diagnostics[i];
                var message = $"[VSig] {diagnostic}";
                switch (diagnostic.Severity)
                {
                    case VSigDiagnosticSeverity.Error:
                        Debug.LogError(message);
                        break;
                    case VSigDiagnosticSeverity.Warning:
                        Debug.LogWarning(message);
                        break;
                    default:
                        Debug.Log(message);
                        break;
                }
            }
        }

        private static VSigEditorValidator.Result Validate(VSigSceneRuntime runtime, bool includeInactiveMarkers)
        {
            var result = new VSigEditorValidator.Result();
            var program = runtime.GetVertexProgram()?.ToProgram() ?? new VSigProgram();
            var markers = VSigEditorValidator.GetSceneMarkers(runtime.gameObject.scene, includeInactiveMarkers);
            VSigSceneCompiler.Validate(program, markers, result.Diagnostics);
            return result;
        }
    }
}
#endif
