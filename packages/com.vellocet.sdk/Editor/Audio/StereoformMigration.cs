#if VELLOCET_STEREOFORM
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vellocet.Sdk.Audio;
using Vellocet.Stereoform;

namespace Vellocet.Sdk.Editor.Audio
{
    /// <summary>Repeatable conversion of selected map objects, with explicit unsupported-case reports.</summary>
    internal sealed class StereoformMigration : EditorWindow
    {
        private string owner = "";
        private Vector2 scroll;
        private readonly List<string> report = new();
        private const string MenuPath = "Tools/Vellocet/SDK/Audio/Convert Selected Map Audio…";

        [MenuItem(MenuPath, true)]
        private static bool CanOpen() => !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling && !EditorApplication.isUpdating && !BuildPipeline.isBuildingPlayer &&
            SdkGame.Current.AudioRuntimeCapability == AddonAudioBanks.Capability;

        [MenuItem(MenuPath, false, 505)]
        private static void Open() => GetWindow<StereoformMigration>("Convert Map Audio");
        private void OnGUI()
        {
            owner = EditorGUILayout.TextField("Owning add-on ID", owner);
            EditorGUILayout.HelpBox("Select saved scene objects. Cues keep stable IDs on repeat runs. Mixer routes, filters, and scripts that still require Unity audio are reported for explicit reauthoring.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!CanOpen()))
                if (GUILayout.Button("Convert selected objects")) Convert();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var line in report) EditorGUILayout.SelectableLabel(line, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(38));
            EditorGUILayout.EndScrollView();
        }
        private void Convert()
        {
            if (!CanOpen()) return;
            report.Clear();
            owner = AddonIdPolicy.NormalizeOrEmpty(owner);
            if (string.IsNullOrEmpty(owner)) { report.Add("Enter the add-on that owns the selected recordings."); return; }
            var roots = Selection.gameObjects.Where(value => value.scene.IsValid()).ToArray();
            if (roots.Length == 0 || roots.Any(value => value.scene.isDirty))
            { report.Add("Save the selected scenes before conversion."); return; }
            var sceneRoots = roots.Select(value => value.scene).Distinct().SelectMany(scene => scene.GetRootGameObjects()).ToArray();
            var components = sceneRoots.SelectMany(value => value.GetComponentsInChildren<Component>(true)).Where(value => value).ToArray();
            foreach (var source in roots.SelectMany(root => root.GetComponentsInChildren<AudioSource>(true)).Distinct().ToArray())
            {
                string identity = GlobalObjectId.GetGlobalObjectIdSlow(source).ToString();
                if (source.outputAudioMixerGroup || source.GetComponents<Component>().Any(value => value is AudioLowPassFilter or AudioHighPassFilter or AudioReverbFilter or AudioDistortionFilter or AudioChorusFilter or AudioEchoFilter))
                { report.Add(identity + ": unresolved mixer/filter route; author its Stereoform event and DSP first."); continue; }
                // A component requirement cannot be satisfied by rewriting serialized references.
                bool required = components.Any(component => component.GetType().GetCustomAttributes(typeof(RequireComponent), true)
                    .Cast<RequireComponent>().Any(attribute => attribute.m_Type0 == typeof(AudioSource) || attribute.m_Type1 == typeof(AudioSource) || attribute.m_Type2 == typeof(AudioSource)) && component.gameObject == source.gameObject);
                if (required) { report.Add(identity + ": a script still requires AudioSource; migrate that script first."); continue; }
                var references = new List<(UnityEngine.Object Target, string Property)>();
                foreach (var component in components)
                {
                    if (!component || component == source) continue;
                    using var serialized = new SerializedObject(component); var property = serialized.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == source)
                            references.Add((component, property.propertyPath));
                }
                // Persistent UnityEvent targets accept replacement Components; typed legacy script fields do not.
                if (references.Any(reference => !reference.Property.EndsWith(".m_Target", StringComparison.Ordinal)))
                { report.Add(identity + ": serialized script references still target AudioSource; migrate those fields first."); continue; }
                Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Migrate Stereoform emitter");
                try
                {
                    var emitter = Undo.AddComponent<AudioEmitter>(source.gameObject);
                    emitter.AutoPlay = source.playOnAwake; emitter.Loop = source.loop; emitter.Gain = source.volume;
                    emitter.Pitch = source.pitch; emitter.Muted = source.mute; emitter.SpatialBlend = source.spatialBlend;
                    emitter.MinDistance = source.minDistance; emitter.MaxDistance = source.maxDistance;
                    emitter.Doppler = source.dopplerLevel; emitter.Priority = Mathf.RoundToInt((255-source.priority)*100f/255);
                    emitter.ReverbSend = source.bypassReverbZones ? 0 : .18f * source.spatialBlend;
                    using (var serialized = new SerializedObject(emitter))
                    {
                        var curve = source.rolloffMode == AudioRolloffMode.Custom ? source.GetCustomCurve(AudioSourceCurveType.CustomRolloff) :
                            new AnimationCurve(Enumerable.Range(0, 32).Select(i => new Keyframe(i/31f,
                                source.rolloffMode == AudioRolloffMode.Linear ? 1-i/31f : i==31 ? 0 : Mathf.Min(1, source.minDistance/Mathf.Max(source.minDistance, i/31f*source.maxDistance)))).ToArray());
                        serialized.FindProperty("distanceGain").animationCurveValue = curve; serialized.ApplyModifiedProperties();
                    }
                    if (source.clip)
                    {
                        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source.clip));
                        string folder = "Assets/StereoformMigration"; Directory.CreateDirectory(folder); AssetDatabase.Refresh();
                        string path = folder + "/" + guid + ".asset";
                        var cue = AssetDatabase.LoadAssetAtPath<AddonAudioCue>(path);
                        if (!cue)
                        {
                            cue = ScriptableObject.CreateInstance<AddonAudioCue>();
                            cue.Configure(owner + ".audio." + guid, source.loop ? source.spatialBlend > 0 ? AddonAudioTemplate.AmbientLoop : AddonAudioTemplate.Ambience2DLoop : source.spatialBlend > 0 ? AddonAudioTemplate.Sfx3D : AddonAudioTemplate.Sfx2D,
                                1, null, guid, null, null);
                            AssetDatabase.CreateAsset(cue, path);
                        }
                        if (!AddonAudioPolicy.TryNormalizeCueId(owner, cue.CueId, out _)) throw new InvalidDataException("Existing cue belongs to another add-on.");
                        emitter.Event = cue;
                    }
                    foreach (var reference in references)
                    {
                        using var serialized = new SerializedObject(reference.Target);
                        string call = reference.Property.Substring(0, reference.Property.Length-"m_Target".Length);
                        var method = serialized.FindProperty(call + "m_MethodName");
                        if (method.stringValue is not ("Play" or "Stop" or "Pause" or "UnPause"))
                            throw new InvalidDataException("Unsupported UnityEvent callback: " + method.stringValue);
                        if (method.stringValue == "UnPause") method.stringValue = "Resume";
                        serialized.FindProperty(reference.Property).objectReferenceValue = emitter;
                        serialized.ApplyModifiedProperties();
                    }
                    Undo.DestroyObjectImmediate(source); EditorSceneManager.MarkSceneDirty(emitter.gameObject.scene);
                    Undo.CollapseUndoOperations(undo); report.Add(identity + ": converted to " + GlobalObjectId.GetGlobalObjectIdSlow(emitter));
                }
                catch (Exception error) { Undo.RevertAllDownToGroup(undo); report.Add(identity + ": unresolved: " + error.Message); }
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Library/StereoformMigration");
            File.WriteAllLines("Library/StereoformMigration/report.txt", report);
        }
    }
}
#endif
