using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SlimeEscape
{
    /// <summary>
    /// 🔴 게이트 4용 기록 장치.
    ///
    /// 브리프 §5의 통과 조건이 "평균 6분+" 같은 숫자다. 손목시계로는 판별로 못 재고,
    /// 옆에 앉아서 재면 그 자체가 검증을 오염시킨다. 그래서 게임이 스스로 적는다.
    ///
    /// 특히 하나 — **이미 진 상태에서 보낸 시간.**
    /// "진 걸 안 알려준다"고 정했으므로(2026-08-28), 그 결정이 옳았는지는
    /// 테스터가 헛되이 쓴 시간으로만 알 수 있다. 지금 판은 최대 544 상태라 게임 안에서 계산된다.
    ///
    /// 파일은 Application.persistentDataPath/playtest.csv 에 쌓인다.
    /// </summary>
    public static class SnakeLog
    {
        public const string FileName = "playtest.csv";
        public static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        public sealed class Run
        {
            public string level;
            public int best;                 // 최단 걸음 (표기값)
            public float seconds;            // 이 판에 쓴 시간
            public float lostSeconds;        // 🔴 그중 이미 진 상태였던 시간
            public int moves;                // 실제로 움직인 걸음
            public int blocked;              // 벽·몸에 막혀 아무 일도 안 난 입력
            public int undo, restart;
            public bool cleared;
        }

        static readonly List<Run> _runs = new List<Run>();
        public static IReadOnlyList<Run> Runs => _runs;

        public static void Add(Run r)
        {
            _runs.Add(r);
            Flush();
        }

        /// 사람이 보고 옮겨적을 수 있는 표. 🔴 WebGL은 파일을 못 쓰므로 이게 유일한 통로다.
        public static string Table()
        {
            var sb = new StringBuilder();
            sb.AppendLine("판      깸   걸린초  걸음/최단  막힘  되돌림  다시");
            foreach (var r in _runs)
                sb.AppendLine($"{r.level,-6} {(r.cleared ? "O" : "X"),-3} {r.seconds,7:0.0}  " +
                              $"{r.moves,4}/{r.best,-4} {r.blocked,4} {r.undo,6} {r.restart,5}");
            return sb.ToString();
        }

        public static void Flush()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return;                 // 브라우저엔 파일 시스템이 없다 — 화면으로 보여준다
#else
            try
            {
                // 🔴 **덧붙인다.** 예전엔 통째로 덮어써서 Play 를 다시 켤 때마다 앞 기록이 날아갔다.
                //    실제로 16판 중 앞 14판의 시간을 잃었다 (08-30). 이 값이 분량의 유일한 자다.
                bool isNew = !System.IO.File.Exists(Path);
                var sb = new StringBuilder();
                if (isNew) sb.AppendLine("때,판,클리어,걸린초,이미진상태초,걸음,최단,막힌입력,되돌리기,다시시작");
                string now = System.DateTime.Now.ToString("MM-dd HH:mm");
                foreach (var r in _runs)
                    sb.AppendLine($"{now},{r.level},{(r.cleared ? 1 : 0)},{r.seconds:0.0},{r.lostSeconds:0.0}," +
                                  $"{r.moves},{r.best},{r.blocked},{r.undo},{r.restart}");
                System.IO.File.AppendAllText(Path, sb.ToString(), Encoding.UTF8);
                _runs.Clear();          // 이미 적었으니 다시 안 적는다
            }
            catch (Exception e) { Debug.LogWarning("[기록] 못 씀 — " + e.Message); }
#endif
        }

        /// <summary>사람이 읽을 요약. 개발자 화면에 띄운다.</summary>
        public static string Summary()
        {
            if (_runs.Count == 0) return "기록 없음";
            float total = 0, lost = 0; int cleared = 0;
            foreach (var r in _runs) { total += r.seconds; lost += r.lostSeconds; if (r.cleared) cleared++; }
            return $"{cleared}/{_runs.Count}판 · 합계 {total / 60f:0.0}분 · " +
                   $"판당 {total / _runs.Count / 60f:0.0}분 · 이미 진 상태 {lost:0}초";
        }
    }

    /// <summary>
    /// 🔴 "여기서부터는 이겨도 못 이긴다"를 게임 안에서 계산한다.
    /// 플레이어에게는 **절대 안 보여준다** — 기록에만 쓴다.
    ///
    /// 방법은 tools/metrics.js와 같다: 도달 가능한 상태를 전부 펴고,
    /// 승리에서 거꾸로 퍼뜨려 이길 수 있는 상태를 표시한다. 나머지가 이미 진 상태다.
    /// </summary>
    public sealed class LostSet
    {
        /// 열쇠 -> 이기기까지 남은 걸음 수. 없으면 **여기서부턴 못 이긴다.**
        readonly Dictionary<string, int> _togo = new Dictionary<string, int>();
        public bool Ready { get; private set; }
        public int States { get; private set; }

        public static string Key(SnakeEngine.State st)
        {
            var sb = new StringBuilder();
            foreach (int c in st.Body) { sb.Append(c); sb.Append(','); }
            // 🔴 상태를 이루는 걸 **하나도 빼면 안 된다.** 빼면 서로 다른 상태가 하나로 합쳐져서
            //    "이미 졌다"도 "다음 한 걸음"도 엉뚱한 답이 나온다.
            //    (08-30: Dm/Sc/Pm 을 빼먹고 있었다 — 홈이 둘인 판에서는 전부 틀린 값이었다)
            sb.Append('|'); sb.Append(st.Fm);
            sb.Append('|'); sb.Append(st.Pg);
            sb.Append('|'); sb.Append(st.Dm);
            sb.Append('|'); sb.Append(st.Sc);
            sb.Append('|'); sb.Append(st.Pm);
            return sb.ToString();
        }

        /// <summary>판이 너무 크면 포기하고 Ready=false로 둔다 (기록만 비게 된다).</summary>
        public LostSet(SnakeEngine.Level L, int cap = 200000)
        {
            var index = new Dictionary<string, int>();
            var states = new List<SnakeEngine.State>();
            var edges = new List<List<int>>();
            var win = new List<int>();

            int Id(SnakeEngine.State st)
            {
                string k = Key(st);
                if (index.TryGetValue(k, out int v)) return v;
                v = states.Count; index[k] = v; states.Add(st); edges.Add(null);
                return v;
            }

            Id(SnakeEngine.StartState(L));
            for (int i = 0; i < states.Count; i++)
            {
                if (states.Count > cap) return;            // Ready = false
                var st = states[i];
                var outs = new List<int>();
                edges[i] = outs;
                if (SnakeEngine.IsWin(L, st)) { win.Add(i); continue; }
                int acts = L.Pads.Count > 0 ? 5 : 4;
                for (int d = 0; d < acts; d++)
                    if (SnakeEngine.Step(L, st, (SnakeEngine.Dir)d, out var ns)) outs.Add(Id(ns));
            }
            if (win.Count == 0) return;

            var rev = new List<int>[states.Count];
            for (int i = 0; i < states.Count; i++) rev[i] = new List<int>();
            for (int i = 0; i < states.Count; i++)
                foreach (int j in edges[i]) rev[j].Add(i);

            // 🔴 이긴 자리에서 **거꾸로** 퍼뜨리며 "몇 걸음 남았나"를 적는다.
            //    이 한 번의 계산으로 "이미 졌나"와 "다음 한 걸음"이 둘 다 나온다.
            var togo = new int[states.Count];
            for (int i = 0; i < states.Count; i++) togo[i] = int.MaxValue;
            var q = new Queue<int>();
            foreach (int i in win) { togo[i] = 0; q.Enqueue(i); }
            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                foreach (int p in rev[cur])
                    if (togo[p] == int.MaxValue) { togo[p] = togo[cur] + 1; q.Enqueue(p); }
            }

            for (int i = 0; i < states.Count; i++)
                if (togo[i] != int.MaxValue) _togo[Key(states[i])] = togo[i];

            States = states.Count;
            Ready = true;
        }

        /// 여기서부턴 무슨 짓을 해도 못 이긴다
        public bool IsLost(SnakeEngine.State st) => Ready && !_togo.ContainsKey(Key(st));

        /// 이기기까지 남은 최소 걸음. 못 이기면 -1.
        public int ToGo(SnakeEngine.State st) =>
            Ready && _togo.TryGetValue(Key(st), out int v) ? v : -1;

        /// <summary>
        /// 🔴 **다음 한 걸음만** 알려준다. 답을 통째로 보여주면 그 판은 거기서 끝난다.
        /// 지금 자리에서 남은 걸음이 하나 줄어드는 쪽을 고른다 —
        /// 처음부터의 정답이 아니라 **어디서 꼬였든** 거기서의 정답이다.
        /// </summary>
        public bool Nudge(SnakeEngine.Level L, SnakeEngine.State st, out SnakeEngine.Dir dir)
        {
            dir = SnakeEngine.Dir.Up;
            int here = ToGo(st);
            if (here <= 0) return false;
            int acts = L.Pads.Count > 0 ? 5 : 4;
            for (int d = 0; d < acts; d++)
            {
                if (!SnakeEngine.Step(L, st, (SnakeEngine.Dir)d, out var ns)) continue;
                if (ToGo(ns) == here - 1) { dir = (SnakeEngine.Dir)d; return true; }
            }
            return false;
        }
    }
}
