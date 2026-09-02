using System.Text;
using UnityEditor;
using UnityEngine;

namespace SlimeEscape.EditorTools
{
    /// <summary>
    /// 🔴 규칙이 두 벌(JS/C#) 있다는 위험을 묶어 두는 검사.
    ///
    /// levels.json의 정답 수순 sol 은 <b>JS 솔버(tools/engine.js)가 박은 값</b>이다.
    /// 그 수순을 게임이 실제로 쓰는 C# 엔진으로 재생해서
    ///   (1) 한 걸음도 막히지 않고  (2) 정확히 best 걸음에  (3) 클리어되는지
    /// 를 본다. 중력·지지·머리 판정이 한 군데라도 어긋나면 여기서 깨진다.
    ///
    /// 🔴 이 검사가 없으면 "JS가 풀린다고 한 판"이 게임에선 안 풀릴 수 있다.
    ///    실제로 낡은 채 방치돼 있었다 (2026-08-28에 rev.5로 다시 씀).
    ///
    ///   Unity.exe -batchmode -quit -projectPath game -executeMethod SlimeEscape.EditorTools.ConformanceCheck.Run
    /// </summary>
    public static class ConformanceCheck
    {
        static bool ToDir(char c, out SnakeEngine.Dir d)
        {
            switch (c)
            {
                case '↑': d = SnakeEngine.Dir.Up; return true;
                case '↓': d = SnakeEngine.Dir.Down; return true;
                case '←': d = SnakeEngine.Dir.Left; return true;
                case '→': d = SnakeEngine.Dir.Right; return true;
                case '↧': d = SnakeEngine.Dir.Drop; return true;   // 받침대에 몸을 놓는다
            }
            d = SnakeEngine.Dir.Up; return false;
        }

        [MenuItem("SlimeEscape/규칙 적합성 검사")]
        public static bool Check()
        {
            var set = SnakeLevels.Load();
            var log = new StringBuilder();
            int fail = 0, easy = 0, unmeasured = 0;

            foreach (var d in set.levels)
            {
                //  🔴 **안 쟴 판을 달리 센다.** 상태가 40만을 넘으면
                //     측정을 포기하고 lost 가 0 으로 남는다. 그걸 "쉽다"로 읽으면
                //     **제일 큰 판 다섯이 항상 경고로 뜼다** — 빨간불이 틀리면
                //     사람은 경고를 안 보게 된다. 초록불이 틀린 것만큼 나쁘다 (09-02).
                if (d.states <= 0) unmeasured++;
                else if (d.lost < 1f) easy++;
                string why = null;
                SnakeEngine.Level L = null;
                SnakeEngine.State st = null;

                try { L = SnakeLevels.ToLevel(d, set.gravity); }
                catch (System.Exception e) { why = "판을 못 읽는다 — " + e.Message; }

                if (why == null)
                {
                    st = SnakeEngine.StartState(L);
                    if (string.IsNullOrEmpty(d.sol)) why = "정답 수순이 비었다 (node tools/stamp.js --write)";
                }

                int steps = 0;
                if (why == null)
                {
                    foreach (char c in d.sol)
                    {
                        if (!ToDir(c, out var dir)) { why = $"수순에 모르는 글자 '{c}'"; break; }
                        if (!SnakeEngine.Step(L, st, dir, out st)) { why = $"{steps + 1}번째 걸음에서 막힘"; break; }
                        steps++;
                        if (SnakeEngine.IsWin(L, st)) break;
                    }
                }
                if (why == null)
                {
                    if (!SnakeEngine.IsWin(L, st))
                        why = $"수순을 다 밟았는데 클리어가 아니다 (홈 {SnakeEngine.Filled(L, st)}/{L.Target.Count}, 길이 {st.Length})";
                    else if (steps != d.best) why = $"걸음 수가 다르다 (C# {steps} vs 표기 {d.best})";
                }

                if (why != null) { fail++; log.AppendLine($"  X {d.id,-10} {why}"); }
                else log.AppendLine($"  O {d.id,-10} {d.best,3}걸음 · 이미 진 상태 {d.lost,5:0.0}% · {d.sol}");
            }

            Debug.Log($"[적합성] 판 {set.levels.Length}개 · 중력 {(set.gravity ? "켬" : "끔")}\n{log}" +
                      (easy > 0
                        ? $"\n🔴 이미 진 상태 1% 미만인 판 {easy}개 — 실수해도 저절로 회복된다"
                        : "") +
                      (unmeasured > 0
                        ? $"\n⚠️ 난이도를 못 재본 판 {unmeasured}개 — 상태가 너무 많아 포기한 큰 판들이다."
                          + "\n   못 재는 것과 쉬운 것은 다르다 — 이쪽이 오히려 제일 길다."
                        : "") +
                      (fail == 0 ? "\n통과 — C# 엔진이 JS 솔버와 같은 답을 낸다"
                                 : $"\n실패 {fail}개 — 두 엔진이 어긋났다"));
            return fail == 0;
        }

        public static void Run() => EditorApplication.Exit(Check() ? 0 : 1);
    }
}
