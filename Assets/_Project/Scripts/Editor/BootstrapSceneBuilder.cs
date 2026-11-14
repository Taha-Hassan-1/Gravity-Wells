using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GravityWells.Game;

namespace GravityWells.Editor
{
    // One-shot tool that (re)builds Bootstrap.unity: a single GameObject with a single
    // Bootstrap component, nothing else. Run via the menu, or headlessly via
    // -executeMethod GravityWells.Editor.BootstrapSceneBuilder.BuildAndSave — never hand-edit
    // the scene's YAML (see CLAUDE.md: scene YAML isn't reliably hand-editable).
    public static class BootstrapSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Bootstrap.unity";

        [MenuItem("GravityWells/Rebuild Bootstrap Scene")]
        public static void BuildAndSave()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("Bootstrap");
            go.AddComponent<Bootstrap>();

            EditorSceneManager.MoveGameObjectToScene(go, scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
            EditorBuildSettings.scenes = scenes;

            Debug.Log($"Bootstrap scene written to {ScenePath} and registered in Build Settings.");

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
