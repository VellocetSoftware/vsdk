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
            VertexContentImporter.Register("stereoform.event", Import);
#if VELLOCET_STEREOFORM
            VertexAudioEvents.Describe = source => source is Vellocet.Stereoform.AudioEventReference audio && !string.IsNullOrEmpty(audio.Id)
                ? new CatalogAudioEvent { Runtime = AddonAudioBanks.Capability, Bank = audio.Bank, Event = audio.Id, Duration = audio.Duration } : null;
#endif
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
#if VELLOCET_STEREOFORM
            if (asset.Type == "stereoform.event")
            {
                var clip = external ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(source);
                cue.ConfigureEvent("addon." + owner + "/content_audio", "addon." + owner + "/cue/" + key, clip ? clip.length : 0);
                cue.ClearEmbeddedMedia();
            }
#endif
            return cue;
        }
    }
}
