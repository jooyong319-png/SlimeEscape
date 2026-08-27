using System;
using System.Collections.Generic;

namespace SlimeEscape
{
    /// <summary>
    /// 슬라임 퍼즐 규칙. <c>tools/engine.js</c>를 그대로 옮긴 것이다.
    ///
    /// 🔴 두 벌이 존재하므로 어긋날 수 있다. <c>EngineConformanceTests</c>가
    ///    levels.json의 정답 수순을 재생해서 두 벌이 같은 답을 내는지 검사한다.
    ///    규칙을 고칠 땐 <b>양쪽을 같이</b> 고치고 테스트를 돌릴 것.
    ///
    /// 규칙
    ///  - 슬라임은 크기 N인 N×N 덩어리. (X, Y) = 왼쪽 아래 칸. 몸은 발밑을 딛고 위로 선다
    ///  - 중력: 발밑이 비면 떨어진다. 낙하는 공짜 — 크기가 안 변한다
    ///  - 좌우 이동: 크기 −1. 앞이 막혔으면 N−1칸까지 턱을 오른다 (오르는 것도 한 걸음)
    ///  - 몸은 가는 쪽으로 흐른다: 먹고 자라면 앞이 부풀고, 줄면 뒤가 딸려온다
    ///  - 덮은 칸의 먹이를 전부 먹는다: 하나당 +1. 불은 끄면서 −FireCost
    ///  - 크기가 0 이하가 되거나 몸이 안 들어가는 걸음은 불가 (죽지 않는다. 못 갈 뿐)
    ///  - 덩어리가 출구 칸을 덮으면 클리어
    /// </summary>
    public static class SlimeEngine
    {
        /// 먹이·불은 int 비트마스크로 추적한다. 판당 30개까지 (JS 솔버와 같은 한계).
        public const int MaxMarkers = 30;

        public struct State
        {
            public int X, Y, N;      // 왼쪽 아래 칸, 크기
            public int Fm, Gm;       // 먹은 먹이 / 끈 불
            public override string ToString() => $"({X},{Y}) n={N} fm={Fm} gm={Gm}";
        }

        public sealed class Level
        {
            public string Id, Name;
            public int StartSize = 3;
            public int FireCost = 3;
            public int W, H;
            public bool[] Wall;                  // [y * W + x]
            public int StartX, StartY, ExitCell;
            public List<int> Foods = new List<int>();
            public List<int> Fires = new List<int>();

            public bool IsWall(int x, int y) =>
                x < 0 || y < 0 || x >= W || y >= H || Wall[y * W + x];
        }

        /// 문자 격자를 판으로 읽는다. # 벽 · . 빈칸 · o 먹이 · f 불 · S 시작 · E 출구
        public static Level Parse(string[] grid, string id, string name, int startSize, int fireCost)
        {
            if (grid == null || grid.Length == 0) throw new ArgumentException("빈 격자");
            var L = new Level
            {
                Id = id, Name = name, StartSize = startSize, FireCost = fireCost,
                H = grid.Length, W = grid[0].Length,
            };
            L.Wall = new bool[L.W * L.H];
            L.StartX = -1; L.ExitCell = -1;

            for (int y = 0; y < L.H; y++)
            {
                if (grid[y].Length != L.W)
                    throw new ArgumentException($"{id}: {y}번째 줄 길이가 다르다 ({grid[y].Length} != {L.W})");
                for (int x = 0; x < L.W; x++)
                {
                    int c = y * L.W + x;
                    switch (grid[y][x])
                    {
                        case '#': L.Wall[c] = true; break;
                        case 'S': L.StartX = x; L.StartY = y; break;
                        case 'E': L.ExitCell = c; break;
                        case 'o': L.Foods.Add(c); break;
                        case 'f': L.Fires.Add(c); break;
                    }
                }
            }
            if (L.StartX < 0) throw new ArgumentException($"{id}: S가 없다");
            if (L.ExitCell < 0) throw new ArgumentException($"{id}: E가 없다");
            if (L.Foods.Count > MaxMarkers || L.Fires.Count > MaxMarkers)
                throw new ArgumentException($"{id}: 먹이/불이 {MaxMarkers}개를 넘는다");
            return L;
        }

        public static bool Fits(Level L, int xL, int y, int n)
        {
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    if (L.IsWall(xL + i, y - j)) return false;
            return true;
        }

        /// 덩어리가 덮는 칸들. 렌더링에도 쓴다.
        public static void Covered(Level L, int xL, int y, int n, List<int> into)
        {
            into.Clear();
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    into.Add((y - j) * L.W + (xL + i));
        }

        /// 크기가 n -> n2로 바뀔 때 몸을 어디에 놓을지. 가는 쪽으로 흐른다.
        static int Reseat(Level L, int xL, int y, int n, int n2, int dir)
        {
            int keepLeft = xL;                       // 뒤쪽(왼쪽) 고정
            int keepRight = (xL + n - 1) - n2 + 1;   // 앞쪽(오른쪽) 고정
            int a, b;
            if (dir > 0) { if (n2 > n) { a = keepLeft; b = keepRight; } else { a = keepRight; b = keepLeft; } }
            else if (dir < 0) { if (n2 > n) { a = keepRight; b = keepLeft; } else { a = keepLeft; b = keepRight; } }
            else { a = keepLeft; b = keepRight; }
            if (Fits(L, a, y, n2)) return a;
            if (Fits(L, b, y, n2)) return b;
            return int.MinValue;
        }

        static readonly List<int> _cov = new List<int>(64);

        /// 한 걸음이 어떤 마디로 이루어졌는가. 화면에서 마디마다 다르게 움직이려고 쓴다.
        /// ⚠️ 규칙에는 영향이 없다 — 보기만 하는 값이라 JS 엔진에는 없다.
        public enum Leg { Step, Fall, Resize }

        public struct TraceStep
        {
            public State St;
            public Leg Leg;
        }

        /// 떨어지고 -> 먹고 -> 크기 변하고 를 안정될 때까지. 실패하면 false.
        /// trace를 주면 마디마다 상태를 쌓아 준다 (실패하면 내용은 버릴 것).
        public static bool Settle(Level L, State st, int dir, out State result, List<TraceStep> trace = null)
        {
            int x = st.X, y = st.Y, n = st.N, fm = st.Fm, gm = st.Gm;
            for (int guard = 0; guard < 32; guard++)
            {
                bool fell = false;
                while (Fits(L, x, y + 1, n)) { y++; fell = true; }
                if (fell) trace?.Add(new TraceStep {
                    St = new State { X = x, Y = y, N = n, Fm = fm, Gm = gm }, Leg = Leg.Fall });

                int delta = 0; bool ate = false;
                Covered(L, x, y, n, _cov);
                foreach (int c in _cov)
                {
                    int fi = L.Foods.IndexOf(c);
                    if (fi >= 0 && (fm & (1 << fi)) == 0) { fm |= 1 << fi; delta += 1; ate = true; continue; }
                    int gi = L.Fires.IndexOf(c);
                    if (gi >= 0 && (gm & (1 << gi)) == 0) { gm |= 1 << gi; delta -= L.FireCost; ate = true; }
                }

                if (!ate)
                {
                    if (fell) continue;
                    result = new State { X = x, Y = y, N = n, Fm = fm, Gm = gm };
                    return true;
                }

                int n2 = n + delta;
                if (n2 < 1) { result = default; return false; }
                if (n2 != n)
                {
                    int nx = Reseat(L, x, y, n, n2, dir);
                    if (nx == int.MinValue) { result = default; return false; }
                    x = nx; n = n2;
                    trace?.Add(new TraceStep {
                        St = new State { X = x, Y = y, N = n, Fm = fm, Gm = gm }, Leg = Leg.Resize });
                }
            }
            result = default;
            return false;
        }

        public static bool StartState(Level L, out State st) =>
            Settle(L, new State { X = L.StartX, Y = L.StartY, N = L.StartSize, Fm = 0, Gm = 0 }, 0, out st);

        /// dx = -1 | 1. 못 가면 false.
        /// trace를 주면 한 걸음이 어떤 마디로 이루어졌는지 순서대로 담아 준다.
        public static bool Move(Level L, State st, int dx, out State result, List<TraceStep> trace = null)
        {
            int hy = int.MinValue;
            for (int h = 0; h <= st.N - 1; h++)
                if (Fits(L, st.X + dx, st.Y - h, st.N)) { hy = st.Y - h; break; }
            if (hy == int.MinValue) { result = default; return false; }

            int n2 = st.N - 1;
            if (n2 < 1) { result = default; return false; }

            int shifted = st.X + dx;
            int x2 = dx > 0 ? (shifted + st.N - 1) - n2 + 1 : shifted;
            if (!Fits(L, x2, hy, n2)) { result = default; return false; }

            var stepped = new State { X = x2, Y = hy, N = n2, Fm = st.Fm, Gm = st.Gm };
            trace?.Clear();
            trace?.Add(new TraceStep { St = stepped, Leg = Leg.Step });
            return Settle(L, stepped, dx, out result, trace);
        }

        public static bool IsWin(Level L, State st)
        {
            int ex = L.ExitCell % L.W, ey = L.ExitCell / L.W;
            return ex >= st.X && ex <= st.X + st.N - 1
                && ey <= st.Y && ey >= st.Y - st.N + 1;
        }

        public static bool IsEaten(State st, int foodIndex) => (st.Fm & (1 << foodIndex)) != 0;
        public static bool IsOut(State st, int fireIndex) => (st.Gm & (1 << fireIndex)) != 0;
    }
}
