using System;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using Vellocet.Framework.Runtime.Content.Surfaces;

namespace Vellocet.Sdk.Aneurism.Editor
{
    /// <summary>The stock URP Lit inspector, with one additional gameplay surface enum.</summary>
    public sealed class SurfaceLitShaderGUI : UnityEditor.ShaderGUI
    {
        private readonly UnityEditor.ShaderGUI _lit = (UnityEditor.ShaderGUI)Activator.CreateInstance(
            typeof(LitGUI).Assembly.GetType("UnityEditor.Rendering.Universal.ShaderGUI.LitShader", true), true);

        public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
        {
            EditorGUI.BeginChangeCheck();
            _lit.OnGUI(editor, properties);
            EditorGUILayout.Space();
            editor.ShaderProperty(FindProperty("_SurfaceType", properties), "Surface");
            if (EditorGUI.EndChangeCheck())
                foreach (var target in editor.targets)
                    if (target is Material material) ValidateMaterial(material);
        }

        public override void ValidateMaterial(Material material)
        {
            _lit.ValidateMaterial(material);
            material.SetColor("_SurfaceColor", material.GetColor("_BaseColor"));
            SurfaceQuerySystem.InvalidateMaterial(material);
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            _lit.AssignNewShaderToMaterial(material, oldShader, newShader);
            ValidateMaterial(material);
        }
    }
}
