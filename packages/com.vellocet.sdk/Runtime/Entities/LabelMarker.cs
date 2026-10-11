// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using TMPro;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("info_label")]
    [EntityAuthoring("Visuals", EntityPlacementKind.Point, "Label")]
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
        [field: Tooltip("TextMeshPro component used for this label.")]
        [field: SerializeField]
        public TextMeshPro Label { get; private set; }

        [SerializeField, HideInInspector] private bool vertexLabel;
        [SerializeField, HideInInspector] private string vertexText;
        [SerializeField, HideInInspector] private Color vertexColor = Color.white;
        [SerializeField, HideInInspector] private float vertexFontSize = 36;

        public string AuthoredText => vertexLabel ? vertexText : Label ? Label.text : string.Empty;
        public Color AuthoredColor => vertexLabel ? vertexColor : Label ? Label.color : Color.white;

        private void Awake()
        {
#if !UNITY_SERVER
            EnsureLabelComponent();
            if (vertexLabel)
            { Label.text = vertexText; Label.color = vertexColor; Label.fontSize = vertexFontSize; Label.alignment = TextAlignmentOptions.Center; }
#endif
        }

        private void Reset()
        {
            EnsureLabelComponent();
        }

        private void EnsureLabelComponent()
        {
            if (!Label) Label = GetComponent<TextMeshPro>() ?? gameObject.AddComponent<TextMeshPro>();
        }

#if UNITY_EDITOR
        public void CaptureVertexLabel()
        {
            // Vertex positions the label's center, not the corner of TMP's default 100-metre text box.
            Label.alignment = TextAlignmentOptions.Center;
            vertexLabel = true;
            vertexText = Label.text; vertexColor = Label.color; vertexFontSize = Label.fontSize;
        }

        public void PrepareVertexPackage()
        {
            if (!vertexLabel || !Label) return;
            // TMP restores missing font references in OnValidate/OnEnable. Serialize authored data;
            // recreate its renderer from the installed game's default font in Awake instead.
            CaptureVertexLabel();
            foreach (var submesh in GetComponentsInChildren<TMP_SubMesh>(true)) DestroyImmediate(submesh.gameObject);
            DestroyImmediate(Label); Label = null;
            if (TryGetComponent<MeshRenderer>(out var renderer)) DestroyImmediate(renderer);
            if (TryGetComponent<MeshFilter>(out var filter)) DestroyImmediate(filter);
        }
#endif
    }
}
