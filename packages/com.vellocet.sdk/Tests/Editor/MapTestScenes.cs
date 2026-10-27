using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vellocet.Sdk.Editor.Tests
{
    internal static class MapTestScenes
    {
        internal static Scene CreateEmpty()
        {
            var active = SceneManager.GetActiveScene();
            // Batch tests may start with Unity's untitled default scene.
            var replaceUntitled = string.IsNullOrEmpty(active.path) &&
                (active.rootCount == 0 || Application.isBatchMode);
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                replaceUntitled ? NewSceneMode.Single : NewSceneMode.Additive);
        }

        internal static void Close(Scene scene)
        {
            if (SceneManager.sceneCount == 1)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            else
                EditorSceneManager.CloseScene(scene, true);
        }
    }
}
