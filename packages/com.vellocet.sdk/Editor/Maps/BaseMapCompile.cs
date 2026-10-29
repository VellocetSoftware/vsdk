// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Maps;
using Vertex.Contracts;

namespace Vellocet.Sdk.Editor.Maps
{
    /// <summary>Game policy for base maps that ship with the game.</summary>
    public sealed class BaseMapSettings
    {
        /// <summary>Folder that holds one subfolder per base map, e.g. <c>Assets/GameContent/Maps</c>.</summary>
        public string MapsRoot;
        /// <summary>Defaults for a new MapDefinition; existing definitions keep their values.</summary>
        public string DefaultModeId, Author = "", Version = "1";
        /// <summary>Runs before every compile, e.g. to validate project settings or regenerate content catalogs.</summary>
        public Action Prepare;
        /// <summary>Registers the published map with the game's content catalog (Addressables etc.).</summary>
        public Action<MapDefinition> Publish;
        /// <summary>Returns an error for a compiled map the game cannot ship, given the source and compiled scene paths.</summary>
        public Func<string, string, string> Verify;
    }

    /// <summary>
    /// Compiles a base map into its single <c>Compiled</c> folder. Nothing outside that folder, the definition and
    /// Addressables changes; a failure restores all three.
    /// </summary>
    public static class BaseMapCompile
    {
        public const string CompiledFolderName = "Compiled";
        private const string BackupRoot = "Library/VellocetBaseMaps";

        public static string CompiledFolder(MapDefinition map) =>
            Path.GetDirectoryName(AssetDatabase.GetAssetPath(map))?.Replace('\\', '/') + "/" + CompiledFolderName;

        public static MapDefinition Compile(BaseMapSettings settings, MapDefinition map, string sourceScenePath,
            MapCompiler.CompileOptions options, bool keepsSource)
        {
            if (!map || map.IsAddon || string.IsNullOrWhiteSpace(map.id))
                throw new InvalidOperationException("Choose a base map definition with an ID.");
            if (Lightmapping.isRunning || StaticOcclusionCulling.isRunning || EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for the Editor and any active bake to finish.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save the open scenes before compiling a map.");
            var definitionPath = AssetDatabase.GetAssetPath(map);
            var folder = CompiledFolder(map);
            if (sourceScenePath.StartsWith(folder + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("Choose the authoring scene, not its compiled output.");
            var scenePath = folder + "/" + map.id + ".unity";
            // A stable scene GUID keeps the map's Addressables entry and content revision across recompiles.
            var sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
            if (string.IsNullOrEmpty(sceneGuid)) sceneGuid = AssetCatalog.Hash(Encoding.UTF8.GetBytes("base-map/" + map.id)).Substring(0, 32);

            var backup = new Backup(BackupRoot + "/" + map.id, folder, definitionPath, settings.Prepare);
            var development = EditorUserBuildSettings.development;
            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(scenePath + ".meta", "fileFormatVersion: 2\nguid: " + sceneGuid + "\n");
                options.SyncBakedArtifactsToAuthoring = false; // Bakes belong to the compiled scene only.
                if (options.RequireNonDevelopmentBuild) EditorUserBuildSettings.development = false;
                var result = MapCompiler.Compile(AssetDatabase.LoadAssetAtPath<SceneAsset>(sourceScenePath), map.id, map.id,
                    MapCompilationReceipt.BaseContentKind, scenePath, options);
                if (!result.Success) throw new InvalidDataException(result.Error ?? "Compilation failed for " + map.id);
                if (settings.Verify?.Invoke(sourceScenePath, scenePath) is { } verifyError) throw new InvalidDataException(verifyError);

                // Scene reloads and bake imports invalidate previously loaded asset objects.
                map = AssetDatabase.LoadAssetAtPath<MapDefinition>(definitionPath) ??
                      throw new InvalidOperationException("The map definition was removed during compilation.");
                var receipt = result.Receipt;
                receipt.mapTitle = map.displayName;
                MapCompilationReceiptUtility.Seal(receipt);
                var receiptPath = folder + "/" + Constants.CompilationReceiptFileName;
                File.WriteAllText(receiptPath, MapCompilationReceiptUtility.Serialize(receipt));
                AssetDatabase.ImportAsset(receiptPath);

                map.contentKind = MapCompilationReceipt.BaseContentKind;
                map.authoringScenePath = keepsSource ? sourceScenePath : string.Empty;
                map.compiledScenePath = scenePath;
                map.compilationReceiptPath = receiptPath;
                map.compilationSeal = receipt.seal;
                map.contentRevision = MapCompilationReceiptUtility.ComputeContentRevision(receipt);
                map.compiledUtc = receipt.createdUtc;
                map.musicSet = result.MusicSet;
                map.sceneReference = new AssetReference(AssetDatabase.AssetPathToGUID(scenePath));
                EditorUtility.SetDirty(map);
                AssetDatabase.SaveAssetIfDirty(map);
                SharedAssetAddressableEditorUtility.EnsureSdkSharedAssets(AddressableAssetSettingsDefaultObject.GetSettings(true));
                settings.Publish?.Invoke(map);
                AssetDatabase.SaveAssets();
                backup.Discard();
                return AssetDatabase.LoadAssetAtPath<MapDefinition>(definitionPath);
            }
            catch
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                backup.Restore();
                throw;
            }
            finally
            {
                EditorUserBuildSettings.development = development;
            }
        }

        /// <summary>
        /// Moves the previous output out of Assets (instant, keeps GUIDs) and copies the small files the compiler
        /// rewrites. A pending backup from an interrupted compile is restored first.
        /// </summary>
        private sealed class Backup
        {
            private const string Addresses = "Assets/AddressableAssetsData";
            private readonly string _root, _folder, _definition;

            public Backup(string root, string folder, string definition, Action prepare)
            {
                _root = root; _folder = folder; _definition = definition;
                if (Directory.Exists(_root)) Restore();
                // Game preparation may validate every published map. Keep the previous
                // compiled scene available until preparation has finished.
                prepare?.Invoke();
                Directory.CreateDirectory(_root);
                if (Directory.Exists(_folder)) Directory.Move(_folder, _root + "/Compiled");
                if (File.Exists(_folder + ".meta")) File.Move(_folder + ".meta", _root + "/Compiled.meta");
                Copy(_definition, _root + "/MapDefinition.asset");
                Copy(_definition + ".meta", _root + "/MapDefinition.asset.meta");
                if (Directory.Exists(Addresses)) FileUtil.CopyFileOrDirectory(Addresses, _root + "/Addresses");
                File.WriteAllText(_root + "/pending", _folder);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            public void Restore()
            {
                if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
                if (File.Exists(_folder + ".meta")) File.Delete(_folder + ".meta");
                if (Directory.Exists(_root + "/Compiled")) Directory.Move(_root + "/Compiled", _folder);
                if (File.Exists(_root + "/Compiled.meta")) File.Move(_root + "/Compiled.meta", _folder + ".meta");
                Copy(_root + "/MapDefinition.asset", _definition);
                Copy(_root + "/MapDefinition.asset.meta", _definition + ".meta");
                if (Directory.Exists(_root + "/Addresses"))
                {
                    if (Directory.Exists(Addresses)) Directory.Delete(Addresses, true);
                    Directory.Move(_root + "/Addresses", Addresses);
                }
                Discard();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            public void Discard()
            {
                try { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
                catch (IOException error) { Debug.LogWarning("Map backup cleanup deferred: " + error.Message); }
            }

            private static void Copy(string from, string to)
            {
                if (File.Exists(from)) File.Copy(from, to, true);
            }
        }
    }
}
