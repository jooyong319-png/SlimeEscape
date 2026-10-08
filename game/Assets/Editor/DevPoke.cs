using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SlimeEscape.EditorTools
{
    /// <summary>
    /// 🔴 **돌아가는 게임 속을 찔러보는 도구** (09-20).
    ///
    /// MCP 로 보내는 C# 조각은 <c>System.Reflection</c> 을 **못 쓴다** (막혀 있다).
    /// 그래서 게임의 안쪽 값(개발자 모드 · 지금 판 · 표)을 만지려면 여기를 거쳐야 한다 —
    /// 파일 쓰기가 막혀 <c>BuildWeb.RunLocal</c> 로 옮겼던 것과 같은 사정이다.
    ///
    /// ⚠️ Editor 폴더라 **빌드에 안 들어간다.** 게임 기능이 아니라 다듬는 도구다.
    ///
    ///   DevPoke.Dev(true)     개발자 모드 켜기 (잠긴 판이 열리고 표 찍기가 켜진다)
    ///   DevPoke.Pick(1)       지금 판에 「좋음」 을 찍고 다음 판으로
    ///   DevPoke.Where()       지금 어느 판인지
    /// </summary>
    public static class DevPoke
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        /// 🔴 **시범 슬라임은 건너뛴다.** 타이틀에서 도는 것도 같은 부품이라, 안 가리면 엉뚱한 쪽을 만진다.
        static SnakeController Main()
        {
            foreach (var c in Object.FindObjectsByType<SnakeController>(FindObjectsSortMode.None))
                if (!(bool)typeof(SnakeController).GetField("_demo", F).GetValue(c)) return c;
            return null;
        }

        static string Id(SnakeController c)
        {
            var lv = typeof(SnakeController).GetProperty("Def", F)?.GetValue(c);
            return lv == null ? "(판 없음)" : (string)lv.GetType().GetField("id").GetValue(lv);
        }

        public static string Dev(bool on)
        {
            var c = Main();
            if (c == null) return "게임이 안 돌고 있다 (Play 를 켜야 한다)";
            typeof(SnakeController).GetField("_dev", F).SetValue(c, on);
            return "개발자 모드 " + (on ? "켬" : "끔") + " · 지금 " + Id(c);
        }

        public static string Pick(int score)
        {
            var c = Main();
            if (c == null) return "게임이 안 돌고 있다 (Play 를 켜야 한다)";
            string before = Id(c);
            typeof(SnakeController).GetMethod("Pick", F).Invoke(c, new object[] { score });
            return before + " 에 " + score + "번 표를 찍었다 → 다음 판 " + Id(c);
        }

        public static string Where()
        {
            var c = Main();
            return c == null ? "게임이 안 돌고 있다" : "지금 " + Id(c);
        }
    }
}
