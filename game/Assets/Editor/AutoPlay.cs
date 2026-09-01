using System.IO;
using UnityEditor;
using UnityEngine;

namespace SlimeEscape.EditorTools
{
    /// <summary>
    /// 🔴 **깃발이 있을 때만** 컴파일이 끝나면 저절로 Play 를 켠다.
    ///
    /// 왜 필요했나 (2026-09-02 밤, 사장님이 주무시는 동안):
    /// 화면을 확인하려면 Play 가 켜져 있어야 하는데, 코드를 고칠 때마다 꺼진다.
    /// Ctrl+P 를 보내는 건 **토글**이라 지금 켜졌는지 꺼졌는지를 먼저 알아야 하고,
    /// 그걸 알려면 화면을 봐야 하고, 화면을 보려면 Play 가 켜져 있어야 한다 —
    /// 뱅뱅 돈다. 그래서 상태를 **확정**시킨다.
    ///
    /// 🔴 깃발은 `tools/autoplay.on` 이다. 이 파일이 없으면 아무 일도 안 한다.
    ///    사장님이 쓰실 땐 깃발이 없어야 한다 — 안 그러면 편집기를 만질 때마다
    ///    게임이 멋대로 돌아간다. 밤 작업이 끝나면 지운다.
    /// </summary>
    [InitializeOnLoad]
    public static class AutoPlay
    {
        //  🔴 유니티의 작업 폴더는 **프로젝트 폴더(game/)** 다.
        //     "tools/..." 로 적으면 game/tools 를 찾아서 깃발을 못 본다 (09-02).
        static string Flag =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "../../tools/autoplay.on"));

        static AutoPlay()
        {
            if (!File.Exists(Flag)) return;
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(Flag)) return;                       // 그 사이 지웠을 수 있다
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Debug.Log("[AUTOPLAY] on");
                EditorApplication.EnterPlaymode();
            };
        }

        [MenuItem("SlimeEscape/자동 Play 끄기")]
        static void Off()
        {
            if (File.Exists(Flag)) File.Delete(Flag);
            Debug.Log("[AUTOPLAY] off");
        }
    }
}
