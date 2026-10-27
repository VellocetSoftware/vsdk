using Vertex.Unity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vellocet.Sdk.Editor.VSig;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.VSig;
using Vellocet.Sdk.VSig.Runtime;
using Vertex.VSig;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed class VSigSceneCompilerTests
    {
        [Test]
        public void ControlledSources_KeepAuthoringPathsAndProjectRootImports()
        {
            const string root = Vertex.Unity.UnityBuildProtocol.AssetRoot;
            if (Directory.Exists(root)) Assert.Ignore("This check requires a project without an active controlled build.");
            try
            {
                const string scripts = root + "/Input/Scripts/";
                Directory.CreateDirectory(scripts + "maps"); Directory.CreateDirectory(scripts + "lib"); AssetDatabase.Refresh();
                const string library = "module work:\n    state value: int = 0\n    input run:\n        value = value + 1\n";
                const string entry = "from lib/work use work\nuse work as job\non map.ready:\n    job.run()\n";
                Write(scripts + "lib/work.vsiglib", library); Write(scripts + "maps/Rules.vsig", entry);
                var diagnostics = new List<VSigDiagnostic>();
                var program = VSigSceneCompiler.Parse(entry, diagnostics, sourcePath: scripts + "maps/Rules.vsig");
                VSigSceneCompiler.Validate(program, Array.Empty<EntityMarker>(), diagnostics);
                Assert.That(diagnostics.Where(d => d.Severity == VSigDiagnosticSeverity.Error), Is.Empty);
                Assert.That(program.Procedures.Single().Statements.Single().Location.Path, Is.EqualTo("lib/work.vsiglib"));
                Assert.That(program.Sources.Select(s => s.Path), Is.EquivalentTo(new[] { "maps/Rules.vsig", "lib/work.vsiglib" }));
            }
            finally { AssetDatabase.DeleteAsset(root); }
        }

        [Test]
        public void CompiledVertexSceneKeepsAuthoredMapIdentityAndRejectsMixedMaps()
        {
            var folder = "Assets/VertexIdentityCheck_" + Guid.NewGuid().ToString("N");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
                var path = folder + "/Map.unity";
                var mapId = Guid.NewGuid();
                var root = new GameObject("Map");
                root.AddComponent<Vertex.Unity.VertexObjectIdentity>().Initialize(mapId, mapId, "Map");
                Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
                Write(folder + "/Map.vsig", "# Geometry-only maps still retain their authored identity.\n");
                Assert.That(VSigCompiledSceneInjector.Inject(scene, path, null, out var message), Is.True, message);
                Assert.That(scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<VSigSceneRuntime>()), Is.Empty);
                var door = new GameObject("door"); door.transform.SetParent(root.transform);
                door.AddComponent<Vertex.Unity.VertexObjectIdentity>().Initialize(mapId, Guid.NewGuid(), "door");
                door.AddComponent<DoorMarker>().ValidateVertexImport();
                Write(folder + "/Map.vsig", "ref door: func_door\non map.ready:\n    door.open()\n");
                Assert.That(VSigCompiledSceneInjector.Inject(scene, path, null, out message), Is.True, message);
                var runtime = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<VSigSceneRuntime>()).Single();
                Assert.That(runtime.MapId, Is.EqualTo(mapId));
                Assert.That(runtime.CompileAndRegister(), Is.True, "The compiled program must bind the imported hierarchy.");
                var bytes = runtime.GetVertexProgram().Serialize();
                var other = new GameObject("Other map");
                other.AddComponent<Vertex.Unity.VertexObjectIdentity>().Initialize(Guid.NewGuid(), Guid.NewGuid(), "Other");
                Assert.That(VSigCompiledSceneInjector.Inject(scene, path, null, out message), Is.False);
                Assert.That(message, Does.Contain("one Vertex map"));
                Assert.That(runtime && runtime.GetVertexProgram().Serialize().SequenceEqual(bytes), Is.True, "Invalid identities must preserve the compiled program.");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void SceneCompilation_UsesReachableAssetsAndPreservesSourcesAndLastGoodProgram()
        {
            var folder = "Assets/VertexCompilerCheck_" + Guid.NewGuid().ToString("N");
            var scenePath = folder + "/Map.unity";
            var libraryPath = folder + "/work.vsiglib";
            var external = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".vsiglib");
            try
            {
                AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
                AssetDatabase.CreateFolder(folder, "nested");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("door").AddComponent<DoorMarker>();
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                const string library = "module work:\n    state value: int = 0\n    input run:\n        value = value + 1\n";
                Write(libraryPath, library);
                Write(folder + "/nested/forward.vsiglib", "from ../work use work\n");
                Write(folder + "/Map.vsig", "from nested/forward use work\nuse work as job\non map.ready:\n    job.run()\n");
                Assert.That(VSigCompiledSceneInjector.Inject(scene, scenePath, null, out var message), Is.True, message);
                var runtime = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<VSigSceneRuntime>()).Single();
                var compiled = runtime.GetVertexProgram();
                var bytes = compiled.Serialize();
                var program = compiled.ToProgram();
                Assert.That(program.GetType().Assembly.GetName().Name, Is.EqualTo("Vertex.VSig"));
                Assert.That(program.Procedures.Single().Statements.Single().Location.Path, Is.EqualTo(libraryPath));
                Assert.That(program.Procedures.Single().Statements.Single().Location.Line, Is.EqualTo(4));
                Assert.That(runtime.MapId, Is.EqualTo(Guid.Parse(AssetDatabase.AssetPathToGUID(scenePath))));
                using (var context = Offline(program))
                {
                    context.HandleGlobalEmit("map", "ready");
                    Assert.That(context.TryGetModuleState("job", "value", out var value) && value.ToString() == "1", Is.True);
                    Assert.That(context.Program.Procedures.Single().Location.Path, Is.EqualTo(libraryPath));
                }

                Write(libraryPath, "module work:\n    input run:\n        invalid statement\n");
                Assert.That(VSigCompiledSceneInjector.Inject(scene, scenePath, null, out message), Is.False);
                Assert.That(message, Does.Contain(libraryPath).And.Contain("3"));
                Assert.That(runtime && runtime.GetVertexProgram().Serialize().SequenceEqual(bytes), Is.True);
                AssetDatabase.DeleteAsset(folder + "/Map.vsig");
                Assert.That(VSigCompiledSceneInjector.Inject(scene, scenePath, null, out message), Is.True, message);
                Assert.That(runtime == null, Is.True, "Removing behavior from a native scene removes its VM.");
                Object.DestroyImmediate(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DoorMarker>()).Single().gameObject);
                Assert.That(VSigCompiledSceneInjector.Inject(scene, scenePath, null, out message), Is.True, message);

                File.WriteAllText(external, library);
                var diagnostics = new List<VSigDiagnostic>();
                VSigSceneCompiler.Parse($"from \"{external.Replace('\\', '/')}\" use work\nuse work as job\n", diagnostics);
                Assert.That(diagnostics.Any(diagnostic => diagnostic.Code == "VSIG_IMPORT_FILE_MISSING"), Is.True,
                    "Absolute filesystem imports are not authorized Unity script assets.");
                diagnostics.Clear();
                VSigSceneCompiler.Parse(new string('x', VSigSourceSet.MaximumSourceCharacters + 1), diagnostics);
                Assert.That(diagnostics.Single().Code, Is.EqualTo("VSIG_SOURCE_SNAPSHOT"));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
                if (File.Exists(external)) File.Delete(external);
            }
        }

        [Test]
        public void HostedLibraries_KeepSeparateDefinitionsAndRejectDuplicateEntityIds()
        {
            var leftObject = new GameObject("Left");
            var rightObject = new GameObject("Right");
            var firstLibrary = new TextAsset("module work:\n    ref door: func_door\n    state value: int = 0\n    input run:\n        value = value + 1\n");
            var secondLibrary = new TextAsset("module work:\n    ref door: func_door\n    state value: int = 0\n    input run:\n        value = value + 10\n");
            var source = new TextAsset("on map.ready:\n    left.run()\n    right.run()\n");
            try
            {
                var left = leftObject.AddComponent<LogicScriptMarker>();
                var right = rightObject.AddComponent<LogicScriptMarker>();
                var leftDoor = new GameObject("door").AddComponent<DoorMarker>();
                leftDoor.transform.SetParent(left.transform);
                var rightDoor = new GameObject("door").AddComponent<DoorMarker>();
                rightDoor.transform.SetParent(right.transform);
                foreach (var (host, library) in new[] { (left, firstLibrary), (right, secondLibrary) })
                {
                    Set(host, nameof(LogicScriptMarker.Library), library);
                    Set(host, nameof(LogicScriptMarker.ModuleName), "work");
                    Set(host, nameof(LogicScriptMarker.BindToHierarchy), true);
                }
                var markers = new EntityMarker[] { left, right, leftDoor, rightDoor };
                var diagnostics = new List<VSigDiagnostic>();
                var program = VSigSceneCompiler.Parse(source.text, diagnostics, markers);
                VSigSceneCompiler.Validate(program, markers, diagnostics);
                Assert.That(diagnostics, Is.Empty, string.Join("\n", diagnostics));
                using (var context = Offline(program, markers))
                {
                    context.HandleGlobalEmit("map", "ready");
                    Assert.That(context.TryGetModuleState("left", "value", out var a) && a.ToString() == "1", Is.True);
                    Assert.That(context.TryGetModuleState("right", "value", out var b) && b.ToString() == "10", Is.True);
                }
                // Same-named hosts still get distinct scope roots and viewport route groups.
                left.name = right.name = "Host";
                Set(left, nameof(LogicScriptMarker.Alias), "left");
                Set(right, nameof(LogicScriptMarker.Alias), "right");
                var routes = VSigRouteModelBuilder.Build(left.gameObject.scene, source, markers);
                Assert.That(routes.HasErrors, Is.False, string.Join("\n", routes.Diagnostics));
                Assert.That(routes.MarkerIdsByModuleRootId[left.transform.GetInstanceID()], Is.EqualTo(new[] { leftDoor.GetInstanceID() }));
                Assert.That(routes.MarkerIdsByModuleRootId[right.transform.GetInstanceID()], Is.EqualTo(new[] { rightDoor.GetInstanceID() }));
                typeof(EntityMarker).GetField("guid", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(right, left.Guid.ToString());
                diagnostics.Clear();
                VSigSceneCompiler.Parse("", diagnostics, markers);
                Assert.That(diagnostics.Single().Code, Is.EqualTo("VSIG_SCENE_INVALID"));
            }
            finally
            {
                Object.DestroyImmediate(leftObject); Object.DestroyImmediate(rightObject);
                Object.DestroyImmediate(firstLibrary); Object.DestroyImmediate(secondLibrary);
                Object.DestroyImmediate(source);
            }
        }

        private static void Write(string path, string source)
        {
            File.WriteAllText(path, source);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static void Set(LogicScriptMarker host, string name, object value) =>
            typeof(LogicScriptMarker).GetField($"<{name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(host, value);

        private static VSigRuntimeContext Offline(VSigProgram program, EntityMarker[] markers = null)
        {
            var previous = VSigRuntimeContext.ResolveGlobalAuthority;
            try
            {
                VSigRuntimeContext.ResolveGlobalAuthority = _ => true;
                return new VSigRuntimeContext(program, markers, null);
            }
            finally { VSigRuntimeContext.ResolveGlobalAuthority = previous; }
        }
    }
}
