using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SlimeEscape.EditorTools
{
    /// <summary>
    /// 🔴 WebGL 빌드 — 친구들에게 링크로 넘기려면 이게 유일하게 쉬운 길이다.
    /// itch.io도 같은 산출물을 쓰므로, 여기서 뚫어두면 출시 때 그대로 쓴다.
    ///
    ///   Unity.exe -batchmode -quit -projectPath game -executeMethod SlimeEscape.EditorTools.BuildWeb.Run
    ///
    /// ⚠️ 브라우저엔 파일 시스템이 없다. 기록은 파일 대신 **마지막 결과 화면**으로 돌려받는다
    ///    (SnakeLog.Flush가 WebGL에서는 아무것도 안 한다).
    /// </summary>
    public static class BuildWeb
    {
        const string Out = "../build/web";

        public static void Run()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes == null || scenes.Length == 0)
            {
                Debug.LogError("[빌드] 빌드 설정에 씬이 없다. ProjectBootstrap.Build 를 먼저 돌릴 것");
                EditorApplication.Exit(1);
                return;
            }

            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Out));
            Directory.CreateDirectory(dir);

            // 압축은 안 한다 — itch.io도 그냥 열리고, 로컬에서 파일로 열어봐도 동작한다
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.runInBackground = true;
            PlayerSettings.companyName = "jooy3";
            PlayerSettings.productName = "SlimeEscape";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);

            var opts = new BuildPlayerOptions
            {
                scenes = Array.ConvertAll(scenes, s => s.path),
                locationPathName = dir,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(opts);
            var sum = report.summary;
            if (sum.result == BuildResult.Succeeded)
            {
                Debug.Log($"[빌드] 성공 — {dir}  ({sum.totalSize / 1024 / 1024f:0.0} MB, {sum.totalTime.TotalSeconds:0}초)");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[빌드] 실패 — {sum.result}, 오류 {sum.totalErrors}개");
                EditorApplication.Exit(1);
            }
        }
    }
}
