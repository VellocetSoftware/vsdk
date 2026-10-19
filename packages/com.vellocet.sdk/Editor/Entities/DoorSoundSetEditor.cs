using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Vellocet.Sdk.Entities.Doors;

namespace Vellocet.Sdk.Editor.Entities
{
    [CustomEditor(typeof(DoorSoundSet))]
    internal sealed class DoorSoundSetEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            void Build()
            {
                root.Unbind();
                root.Clear();
                serializedObject.Update();
                var actions = serializedObject.FindProperty("actions");
                foreach (DoorSoundAction action in Enum.GetValues(typeof(DoorSoundAction)))
                {
                    var index = -1;
                    for (var i = 0; i < actions.arraySize; i++)
                        if (actions.GetArrayElementAtIndex(i).FindPropertyRelative("Action").intValue == (int)action)
                        {
                            index = i;
                            break;
                        }
                    var sounds = index < 0 ? null : actions.GetArrayElementAtIndex(index).FindPropertyRelative("Sounds");
                    var foldout = new Foldout { text = ObjectNames.NicifyVariableName(action.ToString()) + (action == DoorSoundAction.Moving ? " (loop)" : ""), value = sounds?.arraySize > 0 };
                    root.Add(foldout);
                    if (sounds == null || sounds.arraySize == 0)
                        foldout.Add(new Label("Silent"));
                    else
                    {
                        for (var variant = 0; variant < sounds.arraySize; variant++)
                        {
                            var entry = sounds.GetArrayElementAtIndex(variant);
                            var row = new VisualElement();
                            var cue = entry.FindPropertyRelative("Cue").objectReferenceValue;
                            row.Add(new Label(cue ? cue.name : entry.FindPropertyRelative("Event").stringValue));
                            if (SdkGame.Current.AudioRuntimeCapability.StartsWith("wwise", StringComparison.OrdinalIgnoreCase))
                                row.Add(new PropertyField(entry.FindPropertyRelative("Event"), "Event"));
                            var advanced = new Foldout { text = "Event details", value = false };
                            advanced.Add(new PropertyField(entry));
                            row.Add(advanced);
                            foldout.Add(row);
                        }
                    }
                    var assign = new ObjectField("Add event / cue") { objectType = typeof(UnityEngine.Object), allowSceneObjects = false };
                    var selectedAction = action;
                    assign.RegisterValueChangedCallback(change =>
                    {
                        if (!change.newValue)
                            return;
                        var reference = DoorSoundAuthoring.Reference(change.newValue);
                        serializedObject.Update();
                        var entries = serializedObject.FindProperty("actions");
                        var entryIndex = -1;
                        for (var i = 0; i < entries.arraySize; i++)
                            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("Action").intValue == (int)selectedAction)
                            {
                                entryIndex = i;
                                break;
                            }
                        if (entryIndex < 0)
                        {
                            entryIndex = entries.arraySize++;
                            entries.GetArrayElementAtIndex(entryIndex).FindPropertyRelative("Action").intValue = (int)selectedAction;
                            entries.GetArrayElementAtIndex(entryIndex).FindPropertyRelative("Sounds").arraySize = 0;
                        }
                        var values = entries.GetArrayElementAtIndex(entryIndex).FindPropertyRelative("Sounds");
                        var item = values.GetArrayElementAtIndex(values.arraySize++);
                        item.FindPropertyRelative("Runtime").stringValue = reference.Runtime;
                        item.FindPropertyRelative("Bank").stringValue = reference.Bank;
                        item.FindPropertyRelative("Event").stringValue = reference.Event;
                        item.FindPropertyRelative("Duration").floatValue = reference.Duration;
                        item.FindPropertyRelative("Cue").objectReferenceValue = reference.Cue;
                        serializedObject.ApplyModifiedProperties();
                        Build();
                    });
                    foldout.Add(assign);
                    if (SdkGame.Current.AudioRuntimeCapability.StartsWith("wwise", StringComparison.OrdinalIgnoreCase) && (sounds == null || sounds.arraySize == 0))
                    {
                        var named = new TextField("Event") { isDelayed = true };
                        named.RegisterValueChangedCallback(change =>
                        {
                            if (string.IsNullOrWhiteSpace(change.newValue))
                                return;
                            serializedObject.Update();
                            var entries = serializedObject.FindProperty("actions");
                            var i = -1;
                            for (var existing = 0; existing < entries.arraySize; existing++)
                                if (entries.GetArrayElementAtIndex(existing).FindPropertyRelative("Action").intValue == (int)selectedAction)
                                {
                                    i = existing;
                                    break;
                                }
                            if (i < 0)
                                i = entries.arraySize++;
                            var record = entries.GetArrayElementAtIndex(i);
                            record.FindPropertyRelative("Action").intValue = (int)selectedAction;
                            var values = record.FindPropertyRelative("Sounds");
                            values.arraySize = 1;
                            var sound = values.GetArrayElementAtIndex(0);
                            sound.FindPropertyRelative("Runtime").stringValue = "wwise";
                            sound.FindPropertyRelative("Event").stringValue = change.newValue;
                            sound.FindPropertyRelative("Bank").stringValue = "";
                            sound.FindPropertyRelative("Duration").floatValue = 0;
                            sound.FindPropertyRelative("Cue").objectReferenceValue = null;
                            serializedObject.ApplyModifiedProperties();
                            Build();
                        });
                        foldout.Add(named);
                    }
                    if (index >= 0)
                    {
                        var removeIndex = index;
                        foldout.Add(new Button(() => { serializedObject.Update(); serializedObject.FindProperty("actions").DeleteArrayElementAtIndex(removeIndex); serializedObject.ApplyModifiedProperties(); Build(); }) { text = "Clear action sounds" });
                    }
                }
                root.Bind(serializedObject);
            }
            root.RegisterCallback<AttachToPanelEvent>(_ => { Undo.undoRedoPerformed -= Build; Undo.undoRedoPerformed += Build; });
            root.RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= Build);
            Build();
            return root;
        }
    }
}
