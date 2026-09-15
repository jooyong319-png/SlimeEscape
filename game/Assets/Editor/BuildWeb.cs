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

        /// <summary>
        /// 🔴 **에디터를 켠 채로** 웹 빌드 (09-16 다듬기 루프). <see cref="Run"/> 은 배치모드용이라
        /// 끝에 EditorApplication.Exit 로 **에디터를 닫는다** — 켜 둔 에디터에서 부르면 안 된다.
        /// 이건 컴파일·Play 가 끝나기를 기다렸다 한 번 빌드하고, 결과 한 줄을 logPath 에 적는다.
        /// 유니티 MCP 에서 한 줄로 부른다 (MCP 명령 안에서 파일을 쓰면 "사용자 확인이 필요하다"며 막혔다).
        /// </summary>
        public static void RunLocal(string logPath)
        {
            _log = logPath;
            if (File.Exists(_log)) File.Delete(_log);
            EditorApplication.update -= LocalOnce;
            EditorApplication.update += LocalOnce;
            Debug.Log("[빌드] 켠 채로 빌드 대기 — " + _log);
        }

        static string _log;

        /// <summary>
        /// 🔴 **시작 화면(유니티 로고)을 끈다** (09-16 다듬기 5단계 · 시작 화면).
        /// 켜 두면 웹에서 불러오기가 끝난 뒤 회색 바탕에 "MADE WITH Unity" 가 한 번 더 뜬다 —
        /// 우리 틀의 이름·막대와 겹쳐 보였고, 바탕도 게임과 다른 회색이었다.
        /// Unity 6 부터는 무료(Personal)도 끌 수 있다. 혹시 켜지더라도 바탕은 게임과 같은 색으로 둔다.
        /// </summary>
        static void Splash()
        {
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = new Color32(0x0b, 0x0d, 0x10, 0xff);
        }

        static void LocalOnce()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlaying) return;
            EditorApplication.update -= LocalOnce;
            string res;
            try
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.template = "PROJECT:SlimeEscape";
                Splash();
                var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Out));
                Directory.CreateDirectory(dir);
                var rep = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = Array.ConvertAll(EditorBuildSettings.scenes, s => s.path),
                    locationPathName = dir,
                    target = BuildTarget.WebGL,
                    targetGroup = BuildTargetGroup.WebGL,
                    options = BuildOptions.None,
                });
                var s = rep.summary;
                res = $"result={s.result} errors={s.totalErrors} secs={s.totalTime.TotalSeconds:0} mb={s.totalSize / 1024 / 1024f:0.0}";
            }
            catch (Exception e) { res = "EXCEPTION " + e.Message; }
            File.WriteAllText(_log, res);
            Debug.Log("[빌드] " + res);
        }

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
            //  🔴 우리 틀 (Assets/WebGLTemplates/SlimeEscape) — 기본 틀은 로고·전체화면 띠가 게임 아래를 가렸다 (09-16)
            PlayerSettings.WebGL.template = "PROJECT:SlimeEscape";
            Splash();
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
