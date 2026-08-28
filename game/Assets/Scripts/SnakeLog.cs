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

        public static void Flush()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("판,클리어,걸린초,이미진상태초,걸음,최단,막힌입력,되돌리기,다시시작");
                foreach (var r in _runs)
                    sb.AppendLine($"{r.level},{(r.cleared ? 1 : 0)},{r.seconds:0.0},{r.lostSeconds:0.0}," +
                                  $"{r.moves},{r.best},{r.blocked},{r.undo},{r.restart}");
                System.IO.File.WriteAllText(Path, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception e) { Debug.LogWarning("[기록] 못 씀 — " + e.Message); }
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
        readonly HashSet<string> _lost = new HashSet<string>();
        public bool Ready { get; private set; }
        public int States { get; private set; }

        public static string Key(SnakeEngine.State st)
        {
            var sb = new StringBuilder();
            foreach (int c in st.Body) { sb.Append(c); sb.Append(','); }
            sb.Append('|'); sb.Append(st.Fm);
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
                for (int d = 0; d < 4; d++)
                    if (SnakeEngine.Step(L, st, (SnakeEngine.Dir)d, out var ns)) outs.Add(Id(ns));
            }
            if (win.Count == 0) return;

            var rev = new List<int>[states.Count];
            for (int i = 0; i < states.Count; i++) rev[i] = new List<int>();
            for (int i = 0; i < states.Count; i++)
                foreach (int j in edges[i]) rev[j].Add(i);

            var canWin = new bool[states.Count];
            var q = new Queue<int>();
            foreach (int i in win) { canWin[i] = true; q.Enqueue(i); }
            while (q.Count > 0)
                foreach (int p in rev[q.Dequeue()])
                    if (!canWin[p]) { canWin[p] = true; q.Enqueue(p); }

            for (int i = 0; i < states.Count; i++)
                if (!canWin[i]) _lost.Add(Key(states[i]));

            States = states.Count;
            Ready = true;
        }

        public bool IsLost(SnakeEngine.State st) => Ready && _lost.Contains(Key(st));
    }
}
