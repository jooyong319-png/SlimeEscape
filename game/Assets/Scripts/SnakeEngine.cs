using System.Collections.Generic;

namespace SlimeEscape
{
    /// <summary>
    /// 뱀 규칙. 머리를 상하좌우로 한 칸씩 움직이면 몸이 따라온다.
    /// 🔴 중력이 있다 (2026-08-28 확정). 몸이 통째로 떨어진다 — 자기 몸은 지지대가 못 된다.
    ///    그래서 **위로 k칸 오르려면 길이가 k+1 이상**이어야 한다. 짧으면 못 올라간다.
    ///
    /// - 몸은 **칸의 사슬**. body[0]이 머리, 마지막이 꼬리. 시작 길이 1
    /// - 먹이를 먹으면 **길이 +1** (그 걸음엔 꼬리가 안 비켜난다). 줄어드는 건 없다
    /// - 벽·자기 몸에 부딪히는 걸음은 **막힌다** (죽지 않는다. 못 갈 뿐)
    ///   · 꼬리 칸은 어차피 비켜나므로 밟아도 된다 — 단 이번에 자라지 않을 때만
    ///
    /// ⚠️ 옛 SlimeEngine(직사각형 몸 + 중력)은 이 리비전에서 안 쓴다.
    ///    지우진 않았다 — 백업 커밋 c61d031 에 있고, 씬에도 안 올라간다.
    /// </summary>
    public static class SnakeEngine
    {
        public const int MaxFoods = 30;

        public enum Dir { Up, Down, Left, Right }
        public static readonly (int dx, int dy)[] Delta = { (0, -1), (0, 1), (-1, 0), (1, 0) };

        public sealed class Level
        {
            public string Id = "test";
            public int W, H;
            public bool[] Wall;               // [y * W + x]
            public int Start;
            public List<int> Foods = new List<int>();

            /// 🔴 몸으로 정확히 채워야 할 칸. 남아도 모자라도 안 된다.
            public List<int> Target = new List<int>();
            public HashSet<int> TargetSet = new HashSet<int>();

            /// 심(心) — 머리가 마지막에 있어야 할 칸. -1이면 없다(아무 데서나 끝나도 됨).
            public int Core = -1;

            public bool IsWall(int cell) => Wall[cell];
            public int X(int cell) => cell % W;
            public int Y(int cell) => cell / W;

            /// 🔴 중력 (확정). 켜면 몸이 통째로 떨어진다 — 자기 몸은 지지대가 못 된다.
            ///    🔴 위로 k칸 오르려면 길이가 k+1 이상이어야 한다 — 꼬리가 바닥을 짚어야 하니까.
            public bool Gravity;

            /// 길이가 맞아야 채울 수 있다: 시작 1 + 조각 수 == 목표 칸 수
            public bool LengthAddsUp => Target.Count == 0 || Foods.Count + 1 == Target.Count;
        }

        /// 몸은 머리부터 꼬리 순. Fm = 먹은 먹이 비트마스크.
        public sealed class State
        {
            public List<int> Body;
            public int Fm;
            public State(List<int> body, int fm) { Body = body; Fm = fm; }
            public int Head => Body[0];
            public int Length => Body.Count;
            public State Clone() => new State(new List<int>(Body), Fm);
        }

        /// 문자 격자를 판으로.
        ///   # 벽 · . 빈칸 · S 시작(머리) · + 조각 · = 채워야 할 칸 · * 심(채워야 할 칸이면서 끝나는 자리)
        public static Level Parse(string[] grid, string id = "test", bool gravity = false)
        {
            var L = new Level { Id = id, H = grid.Length, W = grid[0].Length, Start = -1, Gravity = gravity };
            L.Wall = new bool[L.W * L.H];
            for (int y = 0; y < L.H; y++)
            {
                if (grid[y].Length != L.W)
                    throw new System.ArgumentException($"{id}: {y}번째 줄 길이가 다르다");
                for (int x = 0; x < L.W; x++)
                {
                    int c = y * L.W + x;
                    switch (grid[y][x])
                    {
                        case '#': L.Wall[c] = true; break;
                        case 'S': L.Start = c; break;
                        case '+': L.Foods.Add(c); break;
                        case '=': L.Target.Add(c); break;
                        case '*': L.Target.Add(c); L.Core = c; break;
                    }
                }
            }
            if (L.Start < 0) throw new System.ArgumentException($"{id}: S가 없다");
            if (L.Foods.Count > MaxFoods) throw new System.ArgumentException($"{id}: 조각이 {MaxFoods}개를 넘는다");

            L.TargetSet = new HashSet<int>(L.Target);
            // 🔴 길이가 안 맞으면 애초에 못 푸는 판이다. 판을 만들 때 바로 알아야 한다.
            if (!L.LengthAddsUp)
                throw new System.ArgumentException(
                    $"{id}: 길이가 안 맞는다 — 조각 {L.Foods.Count}개면 최대 길이 {L.Foods.Count + 1}, " +
                    $"목표는 {L.Target.Count}칸");
            return L;
        }

        /// 몸의 어느 칸이든 바로 아래가 벽이면 버틴다.
        static bool Supported(Level L, List<int> body)
        {
            foreach (int c in body)
            {
                int below = c + L.W;
                if (below >= L.W * L.H || L.IsWall(below)) return true;
            }
            return false;
        }

        /// 지지될 때까지 떨어뜨린다. 떨어질 데가 없으면 false = 그 걸음은 불가.
        static bool Settle(Level L, List<int> body)
        {
            if (!L.Gravity) return true;
            for (int guard = 0; guard < 64; guard++)
            {
                if (Supported(L, body)) return true;
                for (int i = 0; i < body.Count; i++)
                {
                    int next = body[i] + L.W;
                    if (next >= L.W * L.H) return false;
                    body[i] = next;
                }
            }
            return false;
        }

        public static State StartState(Level L)
        {
            var body = new List<int> { L.Start };
            Settle(L, body);
            return new State(body, 0);
        }

        public static bool IsEaten(State st, int foodIndex) => (st.Fm & (1 << foodIndex)) != 0;

        /// 🔴 클리어 — 몸이 목표 칸을 **정확히** 덮고, 심이 있으면 **머리가 거기** 있어야 한다.
        ///    남아도 안 되고 모자라도 안 된다.
        public static bool IsWin(Level L, State st)
        {
            if (L.Target.Count == 0) return false;
            if (st.Body.Count != L.Target.Count) return false;
            foreach (int c in st.Body) if (!L.TargetSet.Contains(c)) return false;
            if (L.Core >= 0 && st.Head != L.Core) return false;
            return true;
        }

        /// 목표 중 지금 몸이 덮고 있는 칸 수 (화면에 "몇 칸 남았는지" 보여주려고)
        public static int Filled(Level L, State st)
        {
            int n = 0;
            foreach (int c in st.Body) if (L.TargetSet.Contains(c)) n++;
            return n;
        }

        /// 한 걸음. 못 가면 false (아무 일도 안 일어난다).
        public static bool Step(Level L, State st, Dir dir, out State result)
        {
            result = null;
            var (dx, dy) = Delta[(int)dir];
            int hx = L.X(st.Head) + dx, hy = L.Y(st.Head) + dy;
            if (hx < 0 || hy < 0 || hx >= L.W || hy >= L.H) return false;

            int nh = hy * L.W + hx;
            if (L.IsWall(nh)) return false;

            int fi = L.Foods.IndexOf(nh);
            bool grows = fi >= 0 && !IsEaten(st, fi);

            // 꼬리는 비켜나니 밟아도 된다 — 단 이번에 자라지 않을 때만
            int blocked = grows ? st.Body.Count : st.Body.Count - 1;
            for (int i = 0; i < blocked; i++) if (st.Body[i] == nh) return false;

            var body = new List<int>(st.Body.Count + 1) { nh };
            body.AddRange(st.Body);
            if (!grows) body.RemoveAt(body.Count - 1);
            if (!Settle(L, body)) return false;          // 🔬 중력 — 떨어질 데가 없으면 못 간다

            result = new State(body, grows ? (st.Fm | (1 << fi)) : st.Fm);
            return true;
        }
    }
}
