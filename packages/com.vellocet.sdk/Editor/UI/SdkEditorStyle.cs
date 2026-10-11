// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Vellocet.Sdk.Editor
{
    /// <summary>
    ///     Vellocet design tokens for SDK editor UI and Scene view overlays.
    /// </summary>
    internal static class SdkEditorStyle
    {
        public const float SignalLineWidth = 2.8f;
        public const float SignalLineWidthHover = 4.0f;
        public const float SignalShadowWidth = 6.0f;
        public const float SignalArrowSpacing = 1.15f;
        public const float SignalEndpointSize = 0.075f;
        public const float SignalEndpointSizeHover = 0.105f;

        private static bool IsDark => EditorGUIUtility.isProSkin;

        // VSDK brand palette. Keep these values in sync with VSDK/App.axaml.
        public static Color Black => Rgb(0x06, 0x06, 0x06);
        public static Color NearBlack => Rgb(0x0B, 0x0B, 0x0B);
        public static Color Surface => Rgb(0x11, 0x11, 0x11);
        public static Color Raised => Rgb(0x18, 0x18, 0x18);
        public static Color White => Color.white;
        public static Color SoftWhite => Rgb(0xE9, 0xE9, 0xE9);
        public static Color Muted => Rgb(0xA2, 0xA2, 0xA2);
        public static Color Dim => Rgb(0x68, 0x68, 0x68);
        public static Color Line => Rgb(0x30, 0x30, 0x30);
        public static Color LineStrong => Rgb(0x55, 0x55, 0x55);
        public static Color Red => Rgb(0xFF, 0x30, 0x30);
        public static Color RedDark => Rgb(0x26, 0x09, 0x09);

        public static Color Accent => Red;
        public static Color AccentWarm => SoftWhite;
        public static Color Success => SoftWhite;
        public static Color Warning => Red;
        public static Color Danger => Red;

        public static Color WindowBackground => IsDark ? NearBlack : Rgb(0xE9, 0xE9, 0xE9);
        public static Color CardBackground => IsDark ? Surface : Rgb(0xF7, 0xF7, 0xF7);
        public static Color SubtleBackground => IsDark ? NearBlack : Rgb(0xEF, 0xEF, 0xEF);
        public static Color RaisedBackground => IsDark ? Raised : White;
        public static Color CardBorder => IsDark ? Line : Muted;
        public static Color StrongBorder => IsDark ? LineStrong : Dim;
        public static Color PrimaryText => IsDark ? White : Surface;
        public static Color SecondaryText => IsDark ? SoftWhite : Raised;
        public static Color MutedText => IsDark ? Muted : Dim;
        public static Color HeroPrimaryText => White;
        public static Color HeroSubtitleText => Muted;
        public static Color HeaderTop => NearBlack;
        public static Color HeaderBottom => NearBlack;
        public static Color Divider => IsDark ? Line : Muted;
        public static Color IconFrameBackground => Surface;
        public static Color ButtonFill => RaisedBackground;
        public static Color ButtonHoverFill => IsDark ? Rgb(0x21, 0x21, 0x21) : SoftWhite;
        public static Color ButtonPressedFill => IsDark ? Black : Rgb(0xD9, 0xD9, 0xD9);
        public static Color SelectionFill => WithAlpha(Red, IsDark ? 0.22f : 0.16f);
        public static Color SelectionBorder => Red;

        // Scene signal colors remain semantic so dense graphs are still readable.
        public static Color SignalOutgoing => IsDark
            ? new Color(1.00f, 0.78f, 0.32f, 1f)
            : new Color(0.86f, 0.46f, 0.08f, 1f);

        public static Color SignalIncoming => IsDark
            ? new Color(0.34f, 0.82f, 1.00f, 1f)
            : new Color(0.02f, 0.42f, 0.78f, 1f);

        public static Color SignalNamedTarget => IsDark
            ? new Color(0.54f, 0.92f, 0.58f, 1f)
            : new Color(0.03f, 0.58f, 0.26f, 1f);

        public static Color SignalHover => IsDark
            ? new Color(1.00f, 1.00f, 0.64f, 1f)
            : new Color(0.16f, 0.62f, 0.28f, 1f);

        public static Color SignalShadow => IsDark
            ? new Color(0f, 0f, 0f, 0.64f)
            : new Color(1f, 1f, 1f, 0.72f);

        public static Color SignalLabelBackground => IsDark
            ? new Color(0.045f, 0.052f, 0.060f, 0.94f)
            : new Color(0.98f, 0.99f, 1.00f, 0.96f);

        public static Color SignalLabelShadow => new(0f, 0f, 0f, IsDark ? 0.38f : 0.18f);

        public static Color SignalLabelBorder => IsDark
            ? new Color(1f, 1f, 1f, 0.14f)
            : new Color(0f, 0f, 0f, 0.18f);

        public static Color SignalLabelText => IsDark
            ? new Color(0.94f, 0.97f, 1f, 1f)
            : new Color(0.08f, 0.11f, 0.14f, 1f);

        public static Color SceneMarkerHalo => WithAlpha(Accent, IsDark ? 0.62f : 0.78f);
        public static Color SceneMarkerHaloSoft => WithAlpha(Accent, IsDark ? 0.18f : 0.22f);

        public static Color BadgeFill(Color color)
        {
            color.a = 1f;
            return color;
        }

        public static Color ContrastText(Color background)
        {
            var backgroundLuminance = RelativeLuminance(background);
            var blackContrast = ContrastRatio(backgroundLuminance, RelativeLuminance(Black));
            var whiteContrast = ContrastRatio(backgroundLuminance, RelativeLuminance(White));
            return blackContrast >= whiteContrast ? Black : White;
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static Color Rgb(byte red, byte green, byte blue)
        {
            return new Color32(red, green, blue, 0xFF);
        }

        private static float ContrastRatio(float firstLuminance, float secondLuminance)
        {
            var lighter = Mathf.Max(firstLuminance, secondLuminance);
            var darker = Mathf.Min(firstLuminance, secondLuminance);
            return (lighter + 0.05f) / (darker + 0.05f);
        }

        private static float RelativeLuminance(Color color)
        {
            return 0.2126f * Linearize(color.r) +
                   0.7152f * Linearize(color.g) +
                   0.0722f * Linearize(color.b);
        }

        private static float Linearize(float channel)
        {
            channel = Mathf.Clamp01(channel);
            return channel <= 0.04045f
                ? channel / 12.92f
                : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }
    }
}
#endif