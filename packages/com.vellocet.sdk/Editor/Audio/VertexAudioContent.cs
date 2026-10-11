using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Vertex.Contracts;
using Vertex.Unity.Editor;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Audio;

namespace Vellocet.Sdk.Editor.Audio
{
    [InitializeOnLoad]
    internal static class VertexAudioContent
    {
        static VertexAudioContent()
        {
            VertexContentImporter.Register(AddonMedia.SharedAudioType, Import);
            VertexContentImporter.Register(AddonMedia.LegacyGrimwarAudioType, Import);
        }
        private static UnityEngine.Object Import(string key, ContentAsset asset, bool external, string source)
        {
            var cue = ScriptableObject.CreateInstance<AddonAudioCue>();
            AssetCatalog.TryParseKey(key, out var owner, out _);
            cue.name = asset.Name;
            cue.Configure(key, (AddonAudioTemplate)int.Parse(asset.Setting("template", "2"), CultureInfo.InvariantCulture),
                float.Parse(asset.Setting("gain", "1"), CultureInfo.InvariantCulture), external ? owner : null,
                external ? null : AssetDatabase.AssetPathToGUID(source),
                external ? null : ProfileFiles.Read(source, checked((int)AddonAudioPolicy.MaximumSourceFileBytes)), System.IO.Path.GetExtension(source));
            return cue;
        }
    }
}
