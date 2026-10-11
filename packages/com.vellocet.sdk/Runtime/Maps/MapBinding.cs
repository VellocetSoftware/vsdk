// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vellocet.Sdk.Maps
{
    /// <summary>
    ///     Data-only component. The weaver writes entries here.
    ///     Runtime binding happens in the base.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapBinding : MonoBehaviour
    {
        [SerializeField] private List<Entry> entries = new();
        public IReadOnlyList<Entry> Entries => entries;

#if UNITY_EDITOR
        public static Type RuntimeAssetType(Type type) => typeof(UnityEngine.Audio.AudioMixerGroup).IsAssignableFrom(type)
            ? typeof(UnityEngine.Audio.AudioMixerGroup) : type;

        public void AddEditorEntry(string componentAsmQualifiedType, int componentIndex, string propertyPath,
            string key, string valueAsmQualifiedType, string subObjectName)
        {
            entries.Add(new Entry
            {
                componentType = componentAsmQualifiedType,
                componentIndex = componentIndex,
                propertyPath = propertyPath,
                key = key,
                valueType = valueAsmQualifiedType,
                subObject = subObjectName
            });
            EditorUtility.SetDirty(this);
        }

        public void AddEditorTarget(UnityEngine.Object target, string propertyPath, string key, Type valueType, string subObject = null)
        {
            entries.Add(new Entry { target = target, componentType = target ? target.GetType().AssemblyQualifiedName : "$scene",
                propertyPath = propertyPath, key = key, valueType = RuntimeAssetType(valueType).AssemblyQualifiedName, subObject = subObject, required = true,
                renderOnly = !target || target is Renderer or MeshFilter or Material or Light or UnityEngine.VFX.VisualEffect });
            EditorUtility.SetDirty(this);
        }
#endif

        /// <summary>The same allowlist validates package rewrites and restores them at runtime.</summary>
        public static bool TryAssign(object target, string path, UnityEngine.Object value, bool apply = true)
        {
            if (!value || string.IsNullOrWhiteSpace(path)) return false;
            if (target == null || target is UnityEngine.Object unityTarget && !unityTarget)
            {
                if (path == "RenderSettings.skybox" && value is Material skybox) { if (apply) RenderSettings.skybox = skybox; return true; }
                if (path == "RenderSettings.customReflectionTexture" && value is Texture reflection) { if (apply) RenderSettings.customReflectionTexture = reflection; return true; }
                return false;
            }
            if (target is Material material)
            {
                if (path == "m_Shader" && value is Shader shader) { if (apply) material.shader = shader; return true; }
                if (path.StartsWith("texture:", StringComparison.Ordinal) && value is Texture texture)
                { if (apply) material.SetTexture(path.Substring(8), texture); return true; }
                return false;
            }
            if (path == "m_Mesh" && value is Mesh mesh)
            {
                if (target is MeshFilter filter) { if (apply) filter.sharedMesh = mesh; return true; }
                if (target is SkinnedMeshRenderer skinned) { if (apply) skinned.sharedMesh = mesh; return true; }
                if (target is MeshCollider collider) { if (apply) collider.sharedMesh = mesh; return true; }
            }
            if (target is Renderer renderer && value is Material assigned)
            {
                if (path == "m_Material") { if (apply) renderer.sharedMaterial = assigned; return true; }
                const string prefix = "m_Materials.Array.data[";
                if (path.StartsWith(prefix, StringComparison.Ordinal) && path.EndsWith("]", StringComparison.Ordinal) &&
                    int.TryParse(path.Substring(prefix.Length, path.Length - prefix.Length - 1), out var index) && index is >= 0 and < 65536)
                {
                    if (apply) { var materials = renderer.sharedMaterials; if (index >= materials.Length) Array.Resize(ref materials, index + 1); materials[index] = assigned; renderer.sharedMaterials = materials; }
                    return true;
                }
            }
            if (target is Collider physics && path == "m_Material" && value is PhysicsMaterial physicsMaterial)
            { if (apply) physics.sharedMaterial = physicsMaterial; return true; }
            if (target is AudioSource audio && path == "m_audioClip" && value is AudioClip clip)
            { if (apply) audio.clip = clip; return true; }
            if (target is AudioSource routedAudio && path == "OutputAudioMixerGroup" && value is UnityEngine.Audio.AudioMixerGroup group)
            { if (apply) routedAudio.outputAudioMixerGroup = group; return true; }
            if (target is Light light && path == "m_Cookie" && value is Texture cookie)
            { if (apply) light.cookie = cookie; return true; }
            if (target is UnityEngine.VFX.VisualEffect effect && path == "m_Asset" && value is UnityEngine.VFX.VisualEffectAsset graph)
            { if (apply) effect.visualEffectAsset = graph; return true; }
            if (target is UnityEngine.VFX.VisualEffect texturedEffect && path.StartsWith("vfx-texture:", StringComparison.Ordinal) && value is Texture vfxTexture)
            {
                var name = path.Substring("vfx-texture:".Length);
                if (!texturedEffect.HasTexture(name)) return false;
                if (apply) texturedEffect.SetTexture(name, vfxTexture);
                return true;
            }
            if (target is UnityEngine.Object and not (MonoBehaviour or ScriptableObject)) return false;
            const string arrayElement = ".Array.data[";
            var arrayStart = path.IndexOf(arrayElement, StringComparison.Ordinal);
            var fieldName = arrayStart < 0 ? path : path.Substring(0, arrayStart);
            // Only serialized fields and their array/list elements; never invoke arbitrary property setters.
            if (fieldName.IndexOf('.') >= 0 || fieldName.IndexOf('[') >= 0) return false;
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null) continue;
                if (field.IsInitOnly || field.IsNotSerialized || !field.IsPublic && !field.IsDefined(typeof(SerializeField))) return false;
                if (arrayStart >= 0)
                {
                    var indexStart = arrayStart + arrayElement.Length;
                    if (!path.EndsWith("]", StringComparison.Ordinal) || path.Length <= indexStart + 1 ||
                        !int.TryParse(path.Substring(indexStart, path.Length - indexStart - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var index)) return false;
                    var elementType = field.FieldType.IsArray && field.FieldType.GetArrayRank() == 1 ? field.FieldType.GetElementType() :
                        field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>) ? field.FieldType.GetGenericArguments()[0] : null;
                    if (elementType == null || !elementType.IsInstanceOfType(value) || field.GetValue(target) is not IList list || index >= list.Count) return false;
                    if (apply) list[index] = value;
                    return true;
                }
                if (!field.FieldType.IsInstanceOfType(value)) return false;
                if (apply) field.SetValue(target, value);
                return true;
            }
            return false;
        }

        [Serializable]
        public class Entry
        {
            public UnityEngine.Object target;
            public bool required;
            public bool renderOnly;
            public string componentType;
            public int componentIndex;
            public string propertyPath;
            public string key;
            public string valueType;
            public string subObject;
        }
    }
}
