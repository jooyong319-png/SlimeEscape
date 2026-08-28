using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeEscape.EditorTools
{
    /// <summary>
    /// 씬을 코드로 만든다. 씬에 손으로 배치한 것이 없으므로
    /// 판을 고치거나 규칙을 바꿔도 씬을 다시 만질 일이 없다.
    ///
    ///   Unity.exe -batchmode -quit -projectPath game -executeMethod SlimeEscape.EditorTools.ProjectBootstrap.Build
    /// </summary>
    public static class ProjectBootstrap
    {
        const string SceneDir = "Assets/Scenes";
        const string ScenePath = SceneDir + "/Main.unity";

        [MenuItem("SlimeEscape/씬 다시 만들기")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x0f, 0x16, 0x14, 0xff);
            camGo.transform.position = new Vector3(0, 0, -10);

            // rev.4 — 뱀 구조. 옛 GameController(직사각형 몸 + 중력)는 씬에 안 올린다.
            var game = new GameObject("Game");
            game.AddComponent<SnakeController>();

            if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "jooy3";
            PlayerSettings.productName = "SlimeEscape";
            PlayerSettings.defaultWebScreenWidth = 960;
            PlayerSettings.defaultWebScreenHeight = 600;
            PlayerSettings.runInBackground = true;

            AssetDatabase.SaveAssets();
            Debug.Log($"[bootstrap] 씬을 만들었다: {ScenePath}");
        }

        /// 배치모드에서 씬 생성 + 결과 보고
        public static void BuildFromCli()
        {
            Build();
            var s = SceneManager.GetActiveScene();
            bool ok = s.IsValid() && File.Exists(ScenePath) && EditorBuildSettings.scenes.Length == 1;
            Debug.Log(ok ? "[bootstrap] OK" : "[bootstrap] 실패");
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
