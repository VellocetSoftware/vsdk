// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using Vellocet.Sdk.UI;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("info_label")]
    [EntityAuthoring("Environment", EntityPlacementKind.Point, "Label")]
    public sealed class LabelMarker : EntityMarker<LabelMarker.Input, LabelMarker.Output>
    {
        public enum Input
        {
            SetText,
            SetColor,
            Clear
        }

        public enum Output
        {
        }

        [field: Header("References")]
        [field: Tooltip("UI Toolkit world text component used for this label.")]
        [field: SerializeField]
        public WorldText Label { get; private set; }

        [SerializeField, Tooltip("Enable for external scripts. VSig references enable this automatically.")]
        private bool scripted;
        [SerializeField, HideInInspector] private bool scriptReferenced;
        public bool RequiresRuntimeEntity => scripted || scriptReferenced;

        [SerializeField, HideInInspector] private bool vertexLabel;
        [SerializeField, HideInInspector] private string vertexText;
        [SerializeField, HideInInspector] private Color vertexColor = Color.white;
        [SerializeField, HideInInspector] private float vertexFontSize = 36;

        [SerializeField, HideInInspector] private Font vertexFont;
        [SerializeField, HideInInspector] private FontStyle vertexFontStyle;

        public string AuthoredText => vertexLabel ? vertexText : Label ? Label.text : string.Empty;
        public Color AuthoredColor => vertexLabel ? vertexColor : Label ? Label.color : Color.white;

        private void Awake()
        {
#if !UNITY_SERVER
            EnsureLabelComponent();
            if (vertexLabel)
            { Label.text = vertexText; Label.color = vertexColor; Label.fontSize = vertexFontSize; Label.FontAsset = vertexFont; Label.FontStyle = vertexFontStyle; Label.alignment = TextAnchor.MiddleCenter; }
#endif
        }

        private void Reset()
        {
            EnsureLabelComponent();
        }

        private void EnsureLabelComponent()
        {
            if (!Label) Label = GetComponent<WorldText>() ?? gameObject.AddComponent<WorldText>();
        }

#if UNITY_EDITOR
        public void SetScriptReferenced(bool value)
        {
            scriptReferenced = value;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }

        public void CaptureVertexLabel()
        {
            // Vertex positions the label's center, not the corner of the default 100-metre text box.
            Label.alignment = TextAnchor.MiddleCenter;
            vertexLabel = true;
            vertexText = Label.text; vertexColor = Label.color; vertexFontSize = Label.fontSize;
            vertexFont = Label.FontAsset; vertexFontStyle = Label.FontStyle;
        }

        public void PrepareVertexPackage()
        {
            if (!vertexLabel || !Label) return;
            // Serialize authored data without ephemeral Toolkit rendering components;
            // recreate its renderer with the authored font and style in Awake instead.
            CaptureVertexLabel();
            DestroyImmediate(Label); Label = null;
            if (TryGetComponent<MeshRenderer>(out var renderer)) DestroyImmediate(renderer);
            if (TryGetComponent<MeshFilter>(out var filter)) DestroyImmediate(filter);
        }
#endif
    }
}
