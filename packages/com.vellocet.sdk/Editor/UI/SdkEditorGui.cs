// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vellocet.Sdk.Editor
{
    public static class SdkEditorGui
    {
        private const float HeroLogoSize = 46f;

        private static GUIStyle _badge;
        private static GUIStyle _bigSubtitlePrimary;
        private static GUIStyle _bigTitlePrimary;
        private static GUIStyle _bigTitleSecondary;
        private static GUIStyle _body;
        private static GUIStyle _caption;
        private static GUIStyle _card;
        private static GUIStyle _heroEyebrow;
        private static GUIStyle _heroSubtitle;
        private static GUIStyle _heroTitle;
        private static GUIStyle _muted;
        private static GUIStyle _primaryButton;
        private static GUIStyle _secondaryButton;
        private static GUIStyle _sectionSubtitle;
        private static GUIStyle _sectionTitle;
        private static GUIStyle _segment;
        private static GUIStyle _segmentSelected;
        private static bool _skin;

        private static Texture2D _cardBackground;
        private static Texture2D _primaryBackground;
        private static Texture2D _primaryHoverBackground;
        private static Texture2D _primaryPressedBackground;
        private static Texture2D _secondaryBackground;
        private static Texture2D _secondaryHoverBackground;
        private static Texture2D _secondaryPressedBackground;
        private static Texture2D _segmentSelectedBackground;

        public static Color Accent => SdkEditorStyle.Accent;
        public static Color Success => SdkEditorStyle.Success;
        public static Color Warning => SdkEditorStyle.Warning;
        public static Color Danger => SdkEditorStyle.Danger;

        public static GUIStyle Card => GetStyle(ref _card);
        public static GUIStyle Body => GetStyle(ref _body);
        public static GUIStyle Muted => GetStyle(ref _muted);
        public static GUIStyle Caption => GetStyle(ref _caption);
        public static GUIStyle PrimaryButton => GetStyle(ref _primaryButton);
        public static GUIStyle SecondaryButton => GetStyle(ref _secondaryButton);

        public static void DrawWindowBackground(Rect windowRect)
        {
            EditorGUI.DrawRect(new Rect(0f, 0f, windowRect.width, windowRect.height),
                SdkEditorStyle.WindowBackground);
        }

        public static void DrawHero(Texture2D logo, string title, string eyebrow, string description,
            float height = 94f)
        {
            EnsureStyles();

            var rect = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, SdkEditorStyle.NearBlack);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), SdkEditorStyle.LineStrong);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 3f, 118f, 3f), Accent);

            var logoRect = new Rect(rect.x + 18f, rect.y + (rect.height - HeroLogoSize) * 0.5f,
                HeroLogoSize, HeroLogoSize);
            if (logo)
                GUI.DrawTexture(logoRect, logo, ScaleMode.ScaleToFit, true);
            else
                GUI.Label(logoRect, "V", _heroTitle);

            var dividerX = logoRect.xMax + 18f;
            EditorGUI.DrawRect(new Rect(dividerX, rect.y + 18f, 1f, rect.height - 36f), SdkEditorStyle.Line);

            var textX = dividerX + 18f;
            var textWidth = Mathf.Max(80f, rect.xMax - textX - 20f);
            var y = rect.y + 15f;
            if (!string.IsNullOrWhiteSpace(eyebrow))
            {
                GUI.Label(new Rect(textX, y, textWidth, 15f), eyebrow.ToUpperInvariant(), _heroEyebrow);
                y += 18f;
            }

            GUI.Label(new Rect(textX, y, textWidth, 27f), title, _heroTitle);
            y += 29f;

            if (!string.IsNullOrWhiteSpace(description))
                GUI.Label(new Rect(textX, y, textWidth, rect.yMax - y - 9f), description, _heroSubtitle);
        }

        public static void DrawSectionHeader(string title, string subtitle = null, Texture2D icon = null)
        {
            EnsureStyles();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (icon)
                    GUILayout.Label(icon, GUILayout.Width(18f), GUILayout.Height(18f));

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(title, _sectionTitle);
                    if (!string.IsNullOrWhiteSpace(subtitle))
                        EditorGUILayout.LabelField(subtitle, _sectionSubtitle);
                }
            }
        }

        public static void DrawDivider()
        {
            var rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, SdkEditorStyle.Divider);
        }

        public static void DrawInfoRow(string label, string value, float labelWidth = 108f)
        {
            EnsureStyles();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, _caption, GUILayout.Width(labelWidth));
                EditorGUILayout.LabelField(value ?? string.Empty, _body);
            }
        }

        public static void DrawBadge(string text, Color color, float minWidth = 62f)
        {
            EnsureStyles();

            var content = new GUIContent((text ?? string.Empty).ToUpperInvariant());
            var size = _badge.CalcSize(content);
            var rect = GUILayoutUtility.GetRect(Mathf.Max(minWidth, size.x + 18f), 20f,
                GUILayout.ExpandWidth(false));
            var fill = SdkEditorStyle.BadgeFill(color);
            EditorGUI.DrawRect(rect, fill);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f),
                SdkEditorStyle.WithAlpha(SdkEditorStyle.Black, 0.45f));

            var previous = _badge.normal.textColor;
            _badge.normal.textColor = SdkEditorStyle.ContrastText(fill);
            GUI.Label(rect, content, _badge);
            _badge.normal.textColor = previous;
        }

        public static bool DrawBigButton(string title, string subtitle, float height = 48f, bool primary = false)
        {
            EnsureStyles();

            var rect = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));
            var clicked = GUI.Button(rect, GUIContent.none, primary ? _primaryButton : _secondaryButton);
            var titleStyle = primary ? _bigTitlePrimary : _bigTitleSecondary;
            GUI.Label(new Rect(rect.x + 14f, rect.y + 7f, rect.width - 28f, 18f), title, titleStyle);
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                var subtitleStyle = primary ? _bigSubtitlePrimary : _caption;
                GUI.Label(new Rect(rect.x + 14f, rect.y + 25f, rect.width - 28f, 16f), subtitle, subtitleStyle);
            }

            return clicked;
        }

        public static int DrawSegmentedControl(int selected, IReadOnlyList<string> labels, float height = 28f)
        {
            EnsureStyles();
            if (labels == null || labels.Count == 0)
                return selected;

            var rect = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));
            var segmentWidth = rect.width / labels.Count;
            var next = selected;
            for (var i = 0; i < labels.Count; i++)
            {
                var segmentRect = new Rect(rect.x + segmentWidth * i, rect.y,
                    i == labels.Count - 1 ? rect.xMax - (rect.x + segmentWidth * i) : segmentWidth,
                    rect.height);
                if (GUI.Button(segmentRect, labels[i] ?? string.Empty,
                        i == selected ? _segmentSelected : _segment))
                    next = i;
            }

            return next;
        }

        public static void DrawCallout(string message, MessageType type = MessageType.None)
        {
            EnsureStyles();
            if (string.IsNullOrWhiteSpace(message))
                return;

            var content = new GUIContent(message);
            var availableWidth = Mathf.Max(180f, EditorGUIUtility.currentViewWidth - 88f);
            var height = Mathf.Max(38f, _body.CalcHeight(content, availableWidth - 38f) + 18f);
            var rect = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));
            var needsAttention = type is MessageType.Warning or MessageType.Error;
            var border = needsAttention ? Accent : SdkEditorStyle.CardBorder;
            var fill = needsAttention ? SdkEditorStyle.RedDark : SdkEditorStyle.SubtleBackground;
            DrawBorder(rect, fill, border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), border);

            var iconName = type switch
            {
                MessageType.Error => "console.erroricon",
                MessageType.Warning => "console.warnicon",
                MessageType.Info => "console.infoicon",
                _ => "console.infoicon"
            };
            var icon = EditorGUIUtility.IconContent(iconName).image;
            if (icon)
                GUI.DrawTexture(new Rect(rect.x + 12f, rect.y + 11f, 16f, 16f), icon, ScaleMode.ScaleToFit, true);
            GUI.Label(new Rect(rect.x + 38f, rect.y + 8f, rect.width - 50f, rect.height - 16f), content, _body);
        }

        public static void DrawEmptyState(string title, string message, string iconName = "console.infoicon")
        {
            EnsureStyles();

            using (new EditorGUILayout.VerticalScope(Card))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(EditorGUIUtility.IconContent(iconName), GUILayout.Width(24f),
                        GUILayout.Height(24f));
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(title, _sectionTitle);
                        if (!string.IsNullOrWhiteSpace(message))
                            EditorGUILayout.LabelField(message, _sectionSubtitle);
                    }
                }
            }
        }

        private static GUIStyle GetStyle(ref GUIStyle style)
        {
            EnsureStyles();
            return style;
        }

        private static void EnsureStyles()
        {
            if (_card != null && _skin == EditorGUIUtility.isProSkin)
                return;

            _skin = EditorGUIUtility.isProSkin;
            ReleaseTextures();

            var regular = AssetDatabase.LoadAssetAtPath<Font>(Constants.MondaRegularFontPath);
            var semiBold = AssetDatabase.LoadAssetAtPath<Font>(Constants.MondaSemiBoldFontPath);
            var bold = AssetDatabase.LoadAssetAtPath<Font>(Constants.MondaBoldFontPath);

            _cardBackground = MakeBorderTexture(SdkEditorStyle.CardBackground, SdkEditorStyle.CardBorder);
            _primaryBackground = MakeBorderTexture(SdkEditorStyle.White, SdkEditorStyle.White);
            _primaryHoverBackground = MakeBorderTexture(SdkEditorStyle.Red, SdkEditorStyle.Red);
            _primaryPressedBackground = MakeBorderTexture(SdkEditorStyle.SoftWhite, SdkEditorStyle.Red);
            _secondaryBackground = MakeBorderTexture(SdkEditorStyle.ButtonFill, SdkEditorStyle.CardBorder);
            _secondaryHoverBackground = MakeBorderTexture(SdkEditorStyle.ButtonHoverFill, SdkEditorStyle.White);
            _secondaryPressedBackground = MakeBorderTexture(SdkEditorStyle.ButtonPressedFill, SdkEditorStyle.Red);
            _segmentSelectedBackground = MakeBorderTexture(SdkEditorStyle.SoftWhite, SdkEditorStyle.SoftWhite);

            _card = CreateBoxStyle(_cardBackground, new RectOffset(16, 16, 14, 14),
                new RectOffset(0, 0, 0, 8));

            _body = new GUIStyle(EditorStyles.label)
            {
                font = regular,
                fontSize = 11,
                wordWrap = true,
                richText = true,
                normal = { textColor = SdkEditorStyle.PrimaryText }
            };

            _muted = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                font = regular,
                fontSize = 10,
                normal = { textColor = SdkEditorStyle.MutedText }
            };

            _caption = new GUIStyle(EditorStyles.miniLabel)
            {
                font = semiBold,
                fontSize = 9,
                normal = { textColor = SdkEditorStyle.MutedText }
            };

            _sectionTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                font = semiBold,
                fontSize = 12,
                richText = true,
                normal = { textColor = SdkEditorStyle.PrimaryText }
            };

            _sectionSubtitle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                font = regular,
                fontSize = 10,
                normal = { textColor = SdkEditorStyle.MutedText }
            };

            _heroTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                font = bold,
                fontSize = 20,
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = SdkEditorStyle.HeroPrimaryText }
            };

            _heroSubtitle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                font = regular,
                fontSize = 10,
                normal = { textColor = SdkEditorStyle.HeroSubtitleText }
            };

            _heroEyebrow = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                font = semiBold,
                fontSize = 9,
                normal = { textColor = Accent }
            };

            _badge = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                font = bold,
                fontSize = 8,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = SdkEditorStyle.Black }
            };

            _primaryButton = CreateButtonStyle(regular, _primaryBackground, _primaryHoverBackground,
                _primaryPressedBackground, SdkEditorStyle.Black, SdkEditorStyle.Black, SdkEditorStyle.Black);
            _secondaryButton = CreateButtonStyle(regular, _secondaryBackground, _secondaryHoverBackground,
                _secondaryPressedBackground, SdkEditorStyle.PrimaryText, SdkEditorStyle.PrimaryText,
                SdkEditorStyle.PrimaryText);

            _bigTitlePrimary = new GUIStyle(_sectionTitle)
            {
                normal = { textColor = SdkEditorStyle.Black }
            };
            _bigSubtitlePrimary = new GUIStyle(_caption)
            {
                normal = { textColor = SdkEditorStyle.Black }
            };
            _bigTitleSecondary = new GUIStyle(_sectionTitle)
            {
                normal = { textColor = SdkEditorStyle.PrimaryText }
            };

            _segment = CreateButtonStyle(regular, _secondaryBackground, _secondaryHoverBackground,
                _secondaryPressedBackground, SdkEditorStyle.MutedText, SdkEditorStyle.PrimaryText,
                SdkEditorStyle.PrimaryText);
            _segmentSelected = CreateButtonStyle(semiBold, _segmentSelectedBackground,
                _segmentSelectedBackground, _primaryPressedBackground, SdkEditorStyle.Black,
                SdkEditorStyle.Black, SdkEditorStyle.Black);
        }

        private static GUIStyle CreateBoxStyle(Texture2D background, RectOffset padding, RectOffset margin)
        {
            return new GUIStyle
            {
                border = new RectOffset(1, 1, 1, 1),
                padding = padding,
                margin = margin,
                normal = { background = background }
            };
        }

        private static GUIStyle CreateButtonStyle(Font font, Texture2D normal, Texture2D hover, Texture2D active,
            Color normalText, Color hoverText, Color activeText,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            return new GUIStyle
            {
                font = font,
                fontSize = 10,
                alignment = alignment,
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(10, 10, 4, 4),
                normal = { background = normal, textColor = normalText },
                hover = { background = hover, textColor = hoverText },
                active = { background = active, textColor = activeText },
                focused = { background = hover, textColor = hoverText },
                onNormal = { background = active, textColor = activeText },
                onHover = { background = hover, textColor = hoverText },
                onActive = { background = active, textColor = activeText },
                onFocused = { background = hover, textColor = hoverText }
            };
        }

        private static void DrawBorder(Rect rect, Color fill, Color border)
        {
            EditorGUI.DrawRect(rect, border);
            EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(0f, rect.width - 2f),
                Mathf.Max(0f, rect.height - 2f)), fill);
        }

        private static Texture2D MakeBorderTexture(Color fill, Color border)
        {
            var texture = new Texture2D(3, 3, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };

            for (var y = 0; y < 3; y++)
            for (var x = 0; x < 3; x++)
                texture.SetPixel(x, y, x == 0 || x == 2 || y == 0 || y == 2 ? border : fill);

            texture.Apply();
            return texture;
        }

        private static void ReleaseTextures()
        {
            DestroyTexture(ref _cardBackground);
            DestroyTexture(ref _primaryBackground);
            DestroyTexture(ref _primaryHoverBackground);
            DestroyTexture(ref _primaryPressedBackground);
            DestroyTexture(ref _secondaryBackground);
            DestroyTexture(ref _secondaryHoverBackground);
            DestroyTexture(ref _secondaryPressedBackground);
            DestroyTexture(ref _segmentSelectedBackground);
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture)
                Object.DestroyImmediate(texture);
            texture = null;
        }
    }
}
#endif