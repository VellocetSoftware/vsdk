using Vertex.VSig;
using VSigCompiledProgram = Vellocet.Sdk.VSig.VSigCompiledProgram;
using VSigCompiledHandler = Vellocet.Sdk.VSig.VSigCompiledHandler;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.Entities.States;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.VSig;
using Vellocet.Sdk.VSig.Runtime;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Tests
{
    public sealed class VSigVertexMigrationTests
    {
        [Test]
        public void CompiledIdentity_StripsAuthoringComponentsAndKeepsScopedBindingsAfterReload()
        {
            var folder = "Assets/VertexBakedIdentityCheck_" + Guid.NewGuid().ToString("N");
            var previous = SceneManager.GetActiveScene();
            var singleScene = string.IsNullOrEmpty(previous.path);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                singleScene ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            VSigSceneRuntime runtime = null;
            try
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
                var mapId = Guid.NewGuid();
                var groupId = Guid.NewGuid();
                var entityId = Guid.NewGuid();
                var root = new GameObject("Map");
                root.AddComponent<Vertex.Unity.VertexObjectIdentity>().Initialize(mapId, mapId, "Map");
                var group = new GameObject("Room"); group.transform.SetParent(root.transform);
                group.AddComponent<Vertex.Unity.VertexObjectIdentity>().Initialize(mapId, groupId, "Room");
                var door = new GameObject("door"); door.transform.SetParent(group.transform);
                door.AddComponent<Vertex.Unity.VertexObjectIdentity>().Initialize(mapId, entityId, "door");
                door.AddComponent<DoorMarker>().ValidateVertexImport();
                var decoration = new GameObject("Decoration"); decoration.transform.SetParent(root.transform);
                decoration.AddComponent<Vertex.Unity.VertexObjectIdentity>().Initialize(mapId, Guid.NewGuid(), "Decoration");
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/Map.prefab");
                Object.DestroyImmediate(root);
                root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var marker = root.GetComponentInChildren<DoorMarker>();
                runtime = new GameObject("VSig").AddComponent<VSigSceneRuntime>();
                var program = Compile("ref door: func_door\nvar value: int = 0\non map.ready:\n    value = 7\n", new[] { marker }).ToProgram();
                program.Refs[0].ScopeAnchorMarkerGuid = entityId.ToString("D");
                program.Refs[0].ScopeRootPath = "Map/Room";
                runtime.Configure(Vertex.VSig.VSigCompiledProgram.FromProgram(program), mapId);
                typeof(EntityMarker).GetField("vertexAuthoredGuid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(marker, false);
                Assert.That(MapDeadContentUtility.Strip(scene, out var message), Is.True, message);
                Assert.That(root.GetComponentsInChildren<Vertex.Unity.VertexObjectIdentity>(true), Is.Empty);
                Assert.That(prefab.GetComponentsInChildren<Vertex.Unity.VertexObjectIdentity>(true), Has.Length.EqualTo(4),
                    "Stripping a compiled instance must preserve the imported authoring prefab.");
                using (var serialized = new SerializedObject(runtime))
                    Assert.That(serialized.FindProperty("nodeIds").arraySize, Is.EqualTo(3), "Unreferenced scenery needs no runtime identity.");
                foreach (var transform in root.GetComponentsInChildren<Transform>()) transform.name = "Runtime " + transform.GetInstanceID();
                AssertBindings();
                VSigRuntimeRegistry.UnregisterContext(runtime.Context);
                var scenePath = folder + "/Compiled.unity";
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                if (!singleScene) EditorSceneManager.CloseScene(scene, true);
                scene = EditorSceneManager.OpenScene(scenePath, singleScene ? OpenSceneMode.Single : OpenSceneMode.Additive);
                runtime = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<VSigSceneRuntime>()).Single();
                marker = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<DoorMarker>()).Single();
                Assert.That(marker.Guid, Is.EqualTo(entityId), "Editor validation must not replace imported IDs after stripping their components.");
                Assert.That(scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Vertex.Unity.VertexObjectIdentity>(true)), Is.Empty);
                AssertBindings();
                Assert.That(Offline(runtime.CompileAndRegister), Is.True, "Re-registering uses the same persisted identities.");

                void AssertBindings()
                {
                    var snapshot = new VSigSceneSnapshot(runtime.MapId, new[] { marker }, runtime.GetNodeId, runtime.GetNodeName);
                    var node = snapshot.Entities[marker].Node;
                    Assert.That(new[] { node.Id, node.Parent.Id, node.Parent.Parent.Id }, Is.EqualTo(new[] { entityId, groupId, mapId }));
                    Assert.That(node.Path, Is.EqualTo("Map/Room/door"));
                    Assert.That(Offline(runtime.CompileAndRegister), Is.True, "Scoped refs must bind after removing authoring components.");
                    Assert.That(runtime.Context.MapId, Is.EqualTo(mapId));
                    runtime.Context.HandleGlobalEmit("map", "ready");
                    Assert.That(runtime.Context.TryGetVariable("value", out var value) && value.ToString() == "7", Is.True);
                }
            }
            finally
            {
                if (runtime && runtime.Context != null) VSigRuntimeRegistry.UnregisterContext(runtime.Context);
                if (scene.IsValid() && scene.isLoaded)
                {
                    if (singleScene) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    else EditorSceneManager.CloseScene(scene, true);
                }
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyProgram_MigratesNestedStatementsAndRejectsCycles(bool storedBytes)
        {
            var compiled = Compile("var value: int = 0\non map.ready:\n    if true:\n        if true:\n            value = 7\n");
            var migrated = compiled.ToVertex();
            Assert.That(migrated.formatVersion, Is.EqualTo(Vertex.VSig.VSigCompiledProgram.CurrentFormatVersion));
            var legacy = VSigCompiledProgram.FromVertex(migrated);
            if (!storedBytes) typeof(VSigCompiledProgram).GetField("vertexBytes", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(legacy, null);
            using var context = Offline(() => new VSigRuntimeContext(legacy.ToProgram(), null, null));
            Assert.That(context.PortableContext.GetType().Assembly.GetName().Name, Is.EqualTo("Vertex.VSig"));
            context.HandleGlobalEmit("map", "ready");
            Assert.That(context.TryGetVariable("value", out var value) && value.ToString() == "7", Is.True);

            var handlers = (VSigCompiledHandler[])typeof(VSigCompiledProgram)
                .GetField("handlers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(legacy);
            handlers[0].statements[0].statements = new[] { handlers[0].statements[0] };
            Assert.Throws<ArgumentException>(() => legacy.ToVertex());
        }

        [Test]
        public void HostCallbacks_AreCapturedPerMapAndAuthorityRemainsLive()
        {
            var oldCommands = VSigRuntimeContext.SubmitServerCommand;
            var oldAuthority = VSigRuntimeContext.ResolveGlobalAuthority;
            var first = 0;
            var second = 0;
            var server = true;
            try
            {
                VSigRuntimeContext.ResolveGlobalAuthority = side => side == "server" && server;
                VSigRuntimeContext.SubmitServerCommand = (int scene, int line, string command, out string error) =>
                { first++; error = null; return true; };
                var program = Compile("on map.ready:\n    wait 1s\n    server_command(\"echo ready\")\n").ToProgram();
                using var a = new VSigRuntimeContext(program, null, null);
                VSigRuntimeContext.SubmitServerCommand = (int scene, int line, string command, out string error) =>
                { second++; error = null; return true; };
                using var b = new VSigRuntimeContext(program, null, null);
                a.HandleGlobalEmit("map", "ready"); b.HandleGlobalEmit("map", "ready");
                a.Advance(1); b.Advance(1);
                Assert.That(first, Is.EqualTo(1)); Assert.That(second, Is.EqualTo(1));
                a.HandleGlobalEmit("map", "ready");
                server = false;
                a.Advance(1);
                Assert.That(first, Is.EqualTo(1));
                Assert.That(a.GetActiveTasks(), Is.Empty);
            }
            finally
            {
                VSigRuntimeContext.SubmitServerCommand = oldCommands;
                VSigRuntimeContext.ResolveGlobalAuthority = oldAuthority;
            }
        }

        [Test]
        public void ClientDoorAudio_UsesTheClientContractAndKeepsActorProvenance()
        {
            var audioObject = new GameObject("audio");
            var doorObject = new GameObject("door");
            try
            {
                var audio = audioObject.AddComponent<AmbientMarker>();
                var door = doorObject.AddComponent<DoorMarker>();
                var markers = new EntityMarker[] { audio, door };
                var program = Compile("ref audio: env_ambient\nref door: func_door\nclient on door.open:\n    audio.play(\"open\")\n", markers).ToProgram();
                var recording = new RecordingRuntime(audio);
                using var context = Offline(() => new VSigRuntimeContext(program, markers, null));
                context.OnRuntimeRegistered(audio, recording);
                var actor = EntitySignalHandle.ForActor("participant:17", "Player");
                context.HandleEmit(door, "open", null, EntitySignalContext.FromActivator(actor));
                Assert.That(recording.Calls, Is.EqualTo(1));
                Assert.That(recording.Context.Activator, Is.EqualTo(actor));
                Assert.That(recording.Context.Caller, Is.EqualTo(EntitySignalHandle.ForEntity(door)));
                Assert.That(recording.Context.Self, Is.EqualTo(EntitySignalHandle.ForEntity(audio)));
                Assert.That(context.Diagnostics, Is.Empty);
            }
            finally { Object.DestroyImmediate(audioObject); Object.DestroyImmediate(doorObject); }
        }

        [Test]
        public void ModObservation_DoesNotRegisterUndeclaredScriptEvents()
        {
            using var context = new VSigRuntimeContext(new VSigProgram(), null, null);
            VSigSignalEvent observed = default;
            void Observe(VSigSignalEvent signal) => observed = signal;
            try
            {
                VSigRuntimeRegistry.SignalEmitted += Observe;
                context.HandleGlobalEmit("mod:example", "Changed", new[] { VSigValue.FromObject(3) });
                Assert.That(observed.SourceAlias, Is.EqualTo("mod:example"));
                Assert.That(observed.OutputName, Is.EqualTo("changed"));
                Assert.That(observed.Values, Is.EqualTo(new object[] { 3 }));
                Assert.That(context.PortableContext.Host.Globals.TryGetSource("mod:example", out _), Is.False);
            }
            finally { VSigRuntimeRegistry.SignalEmitted -= Observe; }
        }

        [Test]
        public void SdkStateSnapshots_RetainTheirConcreteTypeAndProperties()
        {
            var obj = new GameObject("door");
            try
            {
                var marker = obj.AddComponent<DoorMarker>();
                var recording = new RecordingRuntime(marker);
                var program = Compile("ref door: func_door\nvar saved: state = null\non map.ready:\n    saved = door.state\n    door.restore(saved)\n", new[] { marker });
                using var context = Offline(() => new VSigRuntimeContext(program.ToProgram(), new[] { marker }, null));
                context.OnRuntimeRegistered(marker, recording);
                context.HandleGlobalEmit("map", "ready");
                Assert.That(context.TryGetVariable("saved", out var value), Is.True);
                Assert.That(value.TryGetState(out var saved), Is.True);
                Assert.That(saved, Is.TypeOf<DoorEntityState>());
                Assert.That(((DoorEntityState)saved).IsOpen, Is.True);
                Assert.That(recording.Restored, Is.TypeOf<DoorEntityState>());
                Assert.That(context.Diagnostics, Is.Empty);
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [Test]
        public void VertexProgram_InstallsDirectlyAndKeepsSourceLocations()
        {
            var obj = new GameObject("Vertex Program Host");
            try
            {
                var diagnostics = new List<Vertex.VSig.VSigDiagnostic>();
                var program = new Vertex.VSig.VSigParser().Parse("var value: int = 0\non map.ready:\n    value = 7\n",
                    diagnostics, null, "scripts/direct.vsig");
                Assert.That(diagnostics, Is.Empty);
                var compiled = Vertex.VSig.VSigCompiledProgram.FromProgram(program);
                var compatibility = VSigCompiledProgram.FromVertex(compiled).ToVertex();
                Assert.That(compatibility.sources.Single().Hash, Is.EqualTo(compiled.sources.Single().Hash), "The compatibility view must retain source receipts.");
                Assert.That(compatibility.ToProgram().Handlers.Single().Location.Path, Is.EqualTo("scripts/direct.vsig"));
                var runtime = obj.AddComponent<VSigSceneRuntime>();
                var mapId = Guid.NewGuid();
                Assert.Throws<ArgumentException>(() => runtime.Configure(compiled, Guid.Empty));
                runtime.Configure(compiled, mapId);
                Assert.That(Offline(runtime.CompileAndRegister), Is.True);
                var context = runtime.Context;
                context.HandleGlobalEmit("map", "ready");
                Assert.That(context.MapId, Is.EqualTo(mapId));
                Assert.That(context.Program.Variables.Count, Is.EqualTo(1));
                Assert.That(context.TryGetVariable("value", out var value) && value.ToString() == "7", Is.True);
                Assert.That(context.PortableContext.EventTrace[0].Location.Path, Is.EqualTo("scripts/direct.vsig"));
                VSigRuntimeRegistry.UnregisterContext(context);
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SceneReload_PreservesScriptComponentMapAndEntityIdsAndLastGoodRuntime(bool legacy)
        {
            var folder = "Assets/VertexMigrationCheck_" + Guid.NewGuid().ToString("N");
            var path = folder + "/Map.unity";
            Scene scene = default;
            try
            {
                AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
                // Edit Mode tests run in a fresh scene; use the Test Framework's single-scene lifecycle.
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Root"); SceneManager.MoveGameObjectToScene(root, scene);
                var doorObject = new GameObject("door"); doorObject.transform.SetParent(root.transform);
                var marker = doorObject.AddComponent<DoorMarker>();
                var id = marker.Guid;
                var runtime = root.AddComponent<VSigSceneRuntime>();
                var scriptId = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(runtime)));
                var mapId = Guid.NewGuid();
                var compiled = Compile("ref door: func_door\nvar value: int = 0\non map.ready:\n    value = 7\n", new[] { marker });
                if (legacy)
                    typeof(VSigSceneRuntime).GetField("program", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runtime, compiled);
                else runtime.Configure(compiled, stableMapId: mapId);
                Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (legacy)
                {
                    // Exercise the former inline Unity schema, including the absence of all new fields.
                    var yaml = File.ReadAllText(path);
                    yaml = Regex.Replace(yaml, @"(?m)^  (vertexProgram|mapId|nodeIds):[^\r\n]*\r?\n", string.Empty);
                    yaml = Regex.Replace(yaml, @"(?m)^    formatVersion:[^\r\n]*\r?\n", string.Empty);
                    File.WriteAllText(path, yaml);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    mapId = Guid.Parse(AssetDatabase.AssetPathToGUID(path));
                }
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                runtime = scene.GetRootGameObjects()[0].GetComponent<VSigSceneRuntime>();
                marker = scene.GetRootGameObjects()[0].GetComponentInChildren<DoorMarker>();
                Assert.That(marker.Guid, Is.EqualTo(id));
                Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(runtime))), Is.EqualTo(scriptId));
                Assert.That(runtime.GetVertexProgram().formatVersion, Is.EqualTo(Vertex.VSig.VSigCompiledProgram.CurrentFormatVersion));
                Assert.That(runtime.Program.RefCount, Is.EqualTo(1));
                Assert.That(Offline(runtime.CompileAndRegister), Is.True);
                Assert.That(runtime.MapId, Is.EqualTo(mapId));
                var current = runtime.Context;
                current.HandleGlobalEmit("map", "ready");
                Assert.That(current.TryGetVariable("value", out var value) && value.ToString() == "7", Is.True);
                Assert.That(AddonScenePolicy.TryValidate(scene, Array.Empty<MapCompiler.RuntimeDependency>(),
                    out var auditError), Is.True, auditError);
                typeof(VSigSceneRuntime).GetField("vertexProgram", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(runtime, new byte[] { 0 });
                Assert.That(AddonScenePolicy.TryValidate(scene, Array.Empty<MapCompiler.RuntimeDependency>(),
                    out auditError), Is.False);
                Assert.That(auditError, Does.Contain("VSig"));
                LogAssert.Expect(LogType.Error, new Regex("VSIG_PROGRAM_REJECTED"));
                Assert.That(runtime.CompileAndRegister(), Is.False);
                Assert.That(runtime.Context, Is.SameAs(current));
                VSigRuntimeRegistry.UnregisterContext(current);
                Assert.That(current.PortableContext.ActiveRoutineCount, Is.Zero);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [UnityTest]
        public IEnumerator SceneRuntime_AdvancesOnUnityFramesAndCancelsOnDisable()
        {
            yield return new EnterPlayMode();
            var host = new GameObject("SDK VSig Clock");
            try
            {
                var runtime = host.AddComponent<VSigSceneRuntime>();
                runtime.Configure(Compile("var value: int = 0\non map.ready:\n    wait 0s\n    value = 1\n    wait 60s\n    value = 2\n"));
                Assert.That(Offline(runtime.CompileAndRegister), Is.True);
                var context = runtime.Context;
                context.HandleGlobalEmit("map", "ready");
                Assert.That(context.TryGetVariable("value", out var initial) && initial.ToString() == "0", Is.True);
                for (var i = 0; i < 4; i++) yield return null;
                Assert.That(context.TryGetVariable("value", out var next) && next.ToString() == "1", Is.True);
                runtime.enabled = false;
                Assert.That(runtime.CompileAndRegister(), Is.False);
                context.Advance(120);
                Assert.That(context.TryGetVariable("value", out var final) && final.ToString() == "1", Is.True);
                Assert.That(context.PortableContext.ActiveRoutineCount, Is.Zero);
            }
            finally { Object.DestroyImmediate(host); }
            yield return new ExitPlayMode();
        }

        // These checks provide their own offline peer. A game may register a disconnected network policy.
        private static T Offline<T>(Func<T> create)
        {
            var previous = VSigRuntimeContext.ResolveGlobalAuthority;
            var entityAuthority = VSigRuntimeContext.ResolveAuthority;
            try
            {
                VSigRuntimeContext.ResolveGlobalAuthority = _ => true;
                VSigRuntimeContext.ResolveAuthority = (_, _) => true;
                return create();
            }
            finally
            {
                VSigRuntimeContext.ResolveGlobalAuthority = previous;
                VSigRuntimeContext.ResolveAuthority = entityAuthority;
            }
        }

        private static VSigCompiledProgram Compile(string source, EntityMarker[] markers = null)
        {
            var diagnostics = new List<VSigDiagnostic>();
            var program = VSigSceneCompiler.Parse(source, diagnostics, markers);
            VSigSceneCompiler.Validate(program, markers, diagnostics);
            Assert.That(diagnostics, Is.Empty, string.Join("\n", diagnostics));
            return VSigCompiledProgram.FromProgram(program);
        }

        private sealed class RecordingRuntime : IEntitySignalRuntime
        {
            private readonly EntityMarker _marker;
            public RecordingRuntime(EntityMarker marker) => _marker = marker;
            public int Calls { get; private set; }
            public IEntityStateSnapshot Restored { get; private set; }
            public EntitySignalContext Context { get; private set; }
            public EntitySignalContract SignalContract => _marker.SignalContract;
            public EntitySignalHandle SignalHandle => EntitySignalHandle.ForEntity(_marker);
            public bool InvokeInput(string input, string parameter, EntitySignalContext context)
            { Calls++; Context = context; return true; }
            public bool TryReadProperty(string property, out object value) { value = null; return false; }
            public bool TryCaptureState(out IEntityStateSnapshot state) { state = new DoorEntityState(true, false); return true; }
            public bool TryRestoreState(IEntityStateSnapshot state) { Restored = state; return true; }
        }
    }
}
