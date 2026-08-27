using System.Text;
using UnityEditor;
using UnityEngine;

namespace SlimeEscape.EditorTools
{
    /// <summary>
    /// 🔴 규칙이 두 벌(JS/C#) 있다는 위험을 묶어 두는 검사.
    ///
    /// levels.json의 정답 수순은 <b>JS 솔버가 박은 값</b>이다.
    /// 그 수순을 C# 엔진으로 재생해서
    ///   (1) 한 걸음도 막히지 않고  (2) 정확히 best 걸음에  (3) 클리어되는지
    /// 를 본다. 두 엔진이 한 군데라도 달라지면 여기서 깨진다.
    ///
    ///   Unity.exe -batchmode -quit -projectPath game -executeMethod SlimeEscape.EditorTools.ConformanceCheck.Run
    /// </summary>
    public static class ConformanceCheck
    {
        [MenuItem("SlimeEscape/규칙 적합성 검사")]
        public static bool Check()
        {
            var defs = LevelSet.LoadAll();
            var log = new StringBuilder();
            int fail = 0, trivial = 0;

            foreach (var d in defs)
            {
                if (d.back == 0) trivial++;
                string why = null;
                var L = LevelSet.ToLevel(d);

                if (!SlimeEngine.StartState(L, out var st)) why = "시작하자마자 막힘";
                else if (string.IsNullOrEmpty(d.sol)) why = "정답 수순이 비었다 (tools/stamp.js --write)";
                else
                {
                    int steps = 0;
                    foreach (char c in d.sol)
                    {
                        int dx = c == '→' ? 1 : c == '←' ? -1 : 0;
                        if (dx == 0) { why = $"수순에 모르는 글자 '{c}'"; break; }
                        if (!SlimeEngine.Move(L, st, dx, out st)) { why = $"{steps + 1}번째 걸음에서 막힘"; break; }
                        steps++;
                        if (SlimeEngine.IsWin(L, st)) break;
                    }
                    if (why == null)
                    {
                        if (!SlimeEngine.IsWin(L, st)) why = "수순을 다 밟았는데 클리어가 아니다";
                        else if (steps != d.best) why = $"걸음 수가 다르다 (C# {steps} vs 표기 {d.best})";
                    }
                }

                if (why != null) { fail++; log.AppendLine($"  X {d.id,-10} {why}"); }
                else log.AppendLine($"  O {d.id,-10} {d.best}걸음 · 되돌아가기 {d.back} · {d.sol}");
            }

            Debug.Log($"[적합성] 판 {defs.Length}개\n{log}" +
                      $"\n되돌아가기가 없는 판 {trivial}/{defs.Length} (= 오른쪽만 누르면 풀린다)" +
                      (fail == 0 ? "\n통과 — C# 엔진이 JS 솔버와 같은 답을 낸다"
                                 : $"\n실패 {fail}개 — 두 엔진이 어긋났다"));
            return fail == 0;
        }

        public static void Run() => EditorApplication.Exit(Check() ? 0 : 1);
    }
}
