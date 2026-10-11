// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Editor;
using Vellocet.Sdk.Editor.Addons;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.Editor.Tools;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Vellocet.Sdk.Tests
{
    public sealed class SdkEditorDesignTests
    {
        [Test]
        public void BrandPalette_MatchesVsdkTokens()
        {
            Assert.That(ToHex(SdkEditorStyle.Black), Is.EqualTo("060606"));
            Assert.That(ToHex(SdkEditorStyle.NearBlack), Is.EqualTo("0B0B0B"));
            Assert.That(ToHex(SdkEditorStyle.Surface), Is.EqualTo("111111"));
            Assert.That(ToHex(SdkEditorStyle.Raised), Is.EqualTo("181818"));
            Assert.That(ToHex(SdkEditorStyle.SoftWhite), Is.EqualTo("E9E9E9"));
            Assert.That(ToHex(SdkEditorStyle.Muted), Is.EqualTo("A2A2A2"));
            Assert.That(ToHex(SdkEditorStyle.Dim), Is.EqualTo("686868"));
            Assert.That(ToHex(SdkEditorStyle.Line), Is.EqualTo("303030"));
            Assert.That(ToHex(SdkEditorStyle.LineStrong), Is.EqualTo("555555"));
            Assert.That(ToHex(SdkEditorStyle.Red), Is.EqualTo("FF3030"));
            Assert.That(ToHex(SdkEditorStyle.RedDark), Is.EqualTo("260909"));
        }

        [Test]
        public void BrandFonts_AreIncludedInTheSdkPackage()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<Font>(Constants.MondaRegularFontPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Font>(Constants.MondaSemiBoldFontPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Font>(Constants.MondaBoldFontPath), Is.Not.Null);
        }

        [Test]
        public void BadgeContrastText_RemainsLegibleAcrossSemanticFills()
        {
            var fills = new[]
            {
                SdkEditorStyle.SoftWhite,
                SdkEditorStyle.Red,
                SdkEditorStyle.Muted,
                SdkEditorStyle.Dim
            };

            foreach (var fill in fills)
            {
                var text = SdkEditorStyle.ContrastText(SdkEditorStyle.BadgeFill(fill));
                Assert.That(ContrastRatio(fill, text), Is.GreaterThanOrEqualTo(4.5f),
                    $"Badge {ToHex(fill)} selected illegible text {ToHex(text)}.");
            }
        }

        [Test]
        public void PackageBuildApis_ExposeCanonicalOutputNames()
        {
            Assert.That(typeof(AddonMapBuildRequest).GetProperty("PackageOutputRoot"), Is.Not.Null);
            Assert.That(typeof(AddonContentBuildRequest).GetProperty("PackageOutputRoot"), Is.Not.Null);
            Assert.That(typeof(AddonMapBuildResult).GetProperty("SuccessfulPlatformBuilds"), Is.Not.Null);
            Assert.That(typeof(SdkPathDefaults).GetMethod("GetPackageOutputPickerStartPath"), Is.Not.Null);
        }

        [Test]
        public void AuthorFacingPackageCopy_DoesNotUseRetiredToolNames()
        {
            var package = PackageInfo.FindForAssembly(typeof(Vellocet.Sdk.Editor.Workbench.SdkWorkbenchWindow).Assembly);
            Assert.That(package?.resolvedPath, Is.Not.Null.And.Not.Empty);

            var files = new List<string>();
            var editorRoot = Path.Combine(package.resolvedPath, "Editor");
            var documentationRoot = Path.Combine(package.resolvedPath, "Documentation~");
            files.AddRange(Directory.EnumerateFiles(editorRoot, "*.cs", SearchOption.AllDirectories));
            if (Directory.Exists(documentationRoot))
                files.AddRange(Directory.EnumerateFiles(documentationRoot, "*.md", SearchOption.AllDirectories));
            files.Add(Path.Combine(package.resolvedPath, "README.md"));

            var retiredTerms = new[]
            {
                "Map " + "Exporter",
                "map " + "exporter",
                "Export development " + "package",
                "Export " + "folder",
                "export " + "folder",
                "Add-on " + "Workbench",
                "Create New " + "Map",
                "VMod package " + "builder",
                "Addon " + "Overlay",
                "[Ex" + "porter]",
                "Compilation or " + "export",
                "export-and-" + "test.md"
            };

            var violations = files
                .Where(File.Exists)
                .SelectMany(path => retiredTerms
                    .Where(term => File.ReadAllText(path).Contains(term, StringComparison.Ordinal))
                    .Select(term => $"{path}: {term}"))
                .ToArray();
            Assert.That(violations, Is.Empty,
                "Author-facing SDK copy contains retired terminology:\n" + string.Join("\n", violations));
        }

        [Test]
        public void Documentation_UsesTheDeveloperWikiWithoutPackagedGuides()
        {
            var package = PackageInfo.FindForAssembly(typeof(SdkDocumentation).Assembly);
            Assert.That(package?.resolvedPath, Is.Not.Null.And.Not.Empty);
            Assert.That(Directory.Exists(Path.Combine(package.resolvedPath, "Documentation~")), Is.False);

            var readme = File.ReadAllText(Path.Combine(package.resolvedPath, "README.md"));
            var manifest = File.ReadAllText(Path.Combine(package.resolvedPath, "package.json"));
            Assert.That(readme, Does.Contain(Constants.OfficialDocumentationUrl));
            Assert.That(manifest,
                Does.Contain($"\"documentationUrl\": \"{Constants.OfficialDocumentationUrl}\""));

            foreach (SdkDocumentationPage page in Enum.GetValues(typeof(SdkDocumentationPage)))
            {
                var value = SdkDocumentation.GetUrl(page);
                Assert.That(Uri.TryCreate(value, UriKind.Absolute, out var uri), Is.True, page.ToString());
                Assert.That(uri.Scheme, Is.EqualTo(Uri.UriSchemeHttps), page.ToString());
                Assert.That(uri.Host, Is.EqualTo("developer.vellocetsoftware.com"), page.ToString());
            }
        }

        private static string ToHex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }

        private static float ContrastRatio(Color first, Color second)
        {
            var firstLuminance = RelativeLuminance(first);
            var secondLuminance = RelativeLuminance(second);
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