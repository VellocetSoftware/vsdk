// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Vellocet.Sdk.Editor.VSig
{
    [ScriptedImporter(2, "vsig")]
    internal sealed class VSigScriptedImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            VSigScriptedImporterUtility.ImportTextAsset(ctx, "vsig.svg");
        }
    }

    internal static class VSigScriptedImporterUtility
    {
        public static void ImportTextAsset(AssetImportContext ctx, string iconFileName)
        {
            var text = File.Exists(ctx.assetPath) ? File.ReadAllText(ctx.assetPath) : string.Empty;
            var asset = new TextAsset(text)
            {
                name = Path.GetFileNameWithoutExtension(ctx.assetPath)
            };

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Constants.IconsPath}/{iconFileName}");
            if (icon)
                ctx.AddObjectToAsset("source", asset, icon);
            else
                ctx.AddObjectToAsset("source", asset);

            ctx.SetMainObject(asset);
        }
    }
}
#endif