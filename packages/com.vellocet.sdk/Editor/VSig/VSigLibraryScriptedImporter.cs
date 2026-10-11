// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using UnityEditor.AssetImporters;

namespace Vellocet.Sdk.Editor.VSig
{
    [ScriptedImporter(1, "vsiglib")]
    internal sealed class VSigLibraryScriptedImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            VSigScriptedImporterUtility.ImportTextAsset(ctx, "vsiglib.svg");
        }
    }
}
#endif