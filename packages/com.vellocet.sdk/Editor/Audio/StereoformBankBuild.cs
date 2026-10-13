using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Audio;
using Vellocet.Sdk.Utilities;
using Vellocet.Stereoform;

namespace Vellocet.Sdk.Editor.Audio
{
    internal static class StereoformBankBuild
    {
        internal static AddonManifest.ContentEntry BuildVertex(Vertex.Contracts.ContentManifest content, string sourceManifest, string root)
        {
            var audio = content.Assets.Where(asset => asset.Type == "stereoform.event").ToArray();
            if (audio.Length == 0) return null;
            var cues = audio.Select(asset =>
            {
                var path = Vertex.Contracts.ProfileFiles.Resolve(sourceManifest, asset.Source, true);
                path = Path.GetRelativePath(Path.GetFullPath("."), path).Replace('\\', '/');
                var cue = ScriptableObject.CreateInstance<AddonAudioCue>();
                cue.Configure(content.Key(asset), (AddonAudioTemplate)int.Parse(asset.Setting("template", "2"), System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(asset.Setting("gain", "1"), System.Globalization.CultureInfo.InvariantCulture), null,
                    AssetDatabase.AssetPathToGUID(path), null, null);
                return cue;
            }).ToArray();
            try
            {
                var entry = Build(content.Id, "content_audio", content.Title, root, cues);
                var files = content.Files.ToList();
                var catalog = JsonUtility.FromJson<AddonAudioCatalog>(File.ReadAllText(Path.Combine(root, entry.audio.catalog)));
                foreach (var asset in audio)
                {
                    string old = asset.Source;
                    var descriptor = catalog.cues.Single(cue => cue.id == content.Key(asset));
                    asset.Source = "Content/" + content.Id + "/AudioEvents/" + asset.Id + ".sfevent";
                    var bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(descriptor));
                    var path = Path.Combine(root, asset.Source);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, bytes);
                    files.Add(new Vertex.Contracts.BuildFile { Path = asset.Source, Bytes = bytes.Length, Sha256 = Vertex.Contracts.AssetCatalog.Hash(bytes) });
                    if (!content.Assets.Any(other => other.Source == old || other.Companions.Contains(old)))
                    { files.RemoveAll(file => file.Path == old); File.Delete(Path.Combine(root, old)); }
                }
                content.Files = files.OrderBy(file => file.Path, StringComparer.Ordinal).ToArray();
                return entry;
            }
            finally { foreach (var cue in cues) UnityEngine.Object.DestroyImmediate(cue); }
        }
        [Serializable] private sealed class Request { public string owner; public Cue[] cues; }
        [Serializable] private sealed class Cue { public string id, source, template; public double duration, gain; }

        internal static AddonManifest.ContentEntry Build(string owner, string contentId, string title, string root,
            AddonAudioCue[] cues)
        {
            var sources = cues.Select(cue =>
            {
                var path = AssetDatabase.GUIDToAssetPath(cue.SourceAssetGuid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (!clip) throw new InvalidDataException("Audio cue needs a source recording: " + cue.CueId);
                return new Cue { id = cue.CueId, source = Path.GetFullPath(path), duration = (double)clip.samples / clip.frequency,
                    gain = cue.Gain, template = AddonAudioPolicy.ToCatalogValue(cue.Template) };
            }).ToArray();
            var output = Path.Combine(root, "audio", contentId);
            if (Directory.Exists(output)) Directory.Delete(output, true);
            Directory.CreateDirectory(output);
            string request = Path.Combine(Path.GetTempPath(), "stereoform-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(request, JsonUtility.ToJson(new Request { owner = owner, cues = sources }));
                string compiler = Environment.GetEnvironmentVariable("STEREOFORM_COMPILER");
                if (string.IsNullOrEmpty(compiler))
                    compiler = Path.GetFullPath(Path.Combine("StereoformTools", Application.platform == RuntimePlatform.WindowsEditor ? "win-x64" : System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "osx-arm64" : "osx-x64", "Stereoform.Compiler" + (Application.platform == RuntimePlatform.WindowsEditor ? ".exe" : "")));
                if (!File.Exists(compiler)) throw new FileNotFoundException("The matching Stereoform compiler artifact is missing. Install the SDK build dependencies.", compiler);
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.vellocet.sdk.aneurism-iv/package.json");
                string template = Path.Combine(package.resolvedPath, "Editor/Audio/mixer.stereoform");
                var start = new ProcessStartInfo(compiler) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                start.ArgumentList.Add("compile-addon"); start.ArgumentList.Add(request); start.ArgumentList.Add(template); start.ArgumentList.Add(Path.Combine(output, "bank"));
                using var process = Process.Start(start) ?? throw new IOException("Could not start the Stereoform compiler.");
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(120000)) { process.Kill(); throw new TimeoutException("Stereoform bank compilation timed out."); }
                if (process.ExitCode != 0) throw new InvalidDataException(stderr.GetAwaiter().GetResult());
                _ = stdout.GetAwaiter().GetResult();
                var bank = BankCatalog.Read(Path.Combine(output, "bank"), true);
                var catalog = new AddonAudioCatalog { audioRuntime = AddonAudioBanks.Capability, bank = "bank", cues = cues.Select(cue =>
                {
                    string id = "addon." + owner + "/cue/" + cue.CueId;
                    return new AddonAudioCatalog.Cue { id = cue.CueId, eventId = id, duration = bank.events.Single(item => item.id == id).duration,
                        template = AddonAudioPolicy.ToCatalogValue(cue.Template), gain = cue.Gain };
                }).ToArray() };
                var catalogPath = Path.Combine(output, "audio.json");
                File.WriteAllText(catalogPath, JsonUtility.ToJson(catalog, true));
                return new AddonManifest.ContentEntry { kind = AddonManifest.AudioContentKind, id = contentId, title = title,
                    audio = new AddonManifest.AudioContent { catalog = Path.GetRelativePath(root, catalogPath).Replace('\\', '/'),
                        files = Directory.GetFiles(output, "*", SearchOption.AllDirectories).Where(path => !path.EndsWith(".meta", StringComparison.Ordinal)).OrderBy(path => path, StringComparer.Ordinal).Select(path =>
                        {
                            if (!SdkFileHashUtility.TryComputeSha256(path, out var hash)) throw new IOException("Could not hash audio bank.");
                            return new AddonManifest.PlatformFile { path = Path.GetRelativePath(root, path).Replace('\\', '/'), size = new FileInfo(path).Length, sha256 = hash };
                        }).ToArray() } };
            }
            finally { File.Delete(request); }
        }
    }
}
