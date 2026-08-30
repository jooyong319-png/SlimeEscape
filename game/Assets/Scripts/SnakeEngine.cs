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

        /// 🔴 Drop = 받침대에 몸을 놓는다. 걸음이 아니라 **행동**이지만,
        ///    되돌리기·기록·솔버가 전부 걸음 단위라 같은 자리에 둔다.
        public enum Dir { Up, Down, Left, Right, Drop }
        public static readonly (int dx, int dy)[] Delta = { (0, -1), (0, 1), (-1, 0), (1, 0), (0, 0) };

        public sealed class Level
        {
            public string Id = "test";
            public int W, H;
            public bool[] Wall;               // [y * W + x]
            public int Start;
            public List<int> Foods = new List<int>();

            /// <summary>
            /// 🔴 문 하나 = 채워야 할 칸 묶음 + 심(머리가 끝날 자리).
            /// 방에 문이 여럿일 수 있다. 규칙 문장은 그대로다 —
            /// "표시된 칸을 몸으로 정확히 채우면 문이 열린다". 달라지는 건 **어느 문이**뿐.
            /// 🔴 길이 = 목표 칸 수인데 조각은 한 벌뿐이라 **문마다 칸 수가 같아야 한다.**
            /// </summary>
            public sealed class Door
            {
                public List<int> Cells = new List<int>();
                public HashSet<int> Set = new HashSet<int>();
                public int Core = -1;
            }
            public List<Door> Doors = new List<Door>();

            /// <summary>
            /// 🔴 문벽 — 짝이 되는 문을 열기 전엔 벽, 열면 사라진다. 기호 '1' '2' '3'.
            /// 이게 있어야 "저긴 아직 못 가"가 생기고 되돌아올 이유가 생긴다.
            /// </summary>
            public HashSet<int>[] Gates = { new HashSet<int>(), new HashSet<int>(), new HashSet<int>() };
            public bool HasGates;

            /// <summary>
            /// 🔴 맵을 넘는 자리. 밟을 수 있는 바닥이고 **퍼즐엔 아무 영향이 없다** —
            /// 게임이 여기서 다음 맵으로 넘긴다. 기호 '&gt;' 오른쪽 · '&lt;' 왼쪽.
            /// 들어온 출구의 **반대편**에서 나오게 해서 위치가 이어지게 한다.
            /// </summary>
            /// <summary>
            /// 🔴 별 'o'. 먹어도 **몸이 안 자란다** — 자라면 문 계산이 어긋난다.
            /// 안 먹어도 판은 깨진다. 별 둘/셋을 받으려면 먹어야 한다.
            /// </summary>
            public int Star = -1;
            /// <summary>
            /// 🔴 받침대 'T' — 몸을 여기 두고 핵만 갈 수 있다. 두고 온 몸은 딛고 설 지형이 된다.
            /// "이 몸을 홈에 낼 것인가, 계단으로 쓸 것인가" 가 이 게임의 결정이다.
            /// 아무 데나 놓게 하면 '어디에 놓았나'가 상태에 들어가 솔버가 터진다.
            /// 자리를 정해두면 받침대 칸 수만큼의 비트로 끝난다.
            /// </summary>
            /// <summary>
            /// 🔴 화살표 표지판 '^ v &lt; &gt;' — **순수 장식.** 퍼즐엔 아무 영향이 없다.
            /// 가르치는 판에서 "여기서 이 키" 를 맵 안에 박아두는 용도다.
            /// (0 위 · 1 아래 · 2 왼 · 3 오른 — Dir 과 같은 순서)
            /// </summary>
            public List<(int cell, int dir)> Signs = new List<(int, int)>();
            public List<int> Pads = new List<int>();
            public Dictionary<int, int> PadIdx = new Dictionary<int, int>();

            /// "any" — 아무 문이나 열면 끝 (지금까지의 판)
            /// "all" — 🔴 문을 다 열어야 끝. 열 때마다 몸을 자물쇠에 두고 핵만 간다
            public string Clear = "any";
            public int AllDoors;

            /// 🔴 몸으로 정확히 채워야 할 칸 (첫째 문). 문이 하나뿐인 판을 위해 남겨둔 이름.
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
            /// 🔴 조각이 맞아야 애초에 풀 수 있다.
            ///    any — 문 하나만 채운다: 조각 = 칸 수 - 1
            ///    all — 문마다 채우고 몸을 두고 간다: 조각 = 문마다 (칸 수 - 1)의 합
            public int FoodsNeeded
            {
                get
                {
                    if (Doors.Count == 0) return Foods.Count;
                    if (Clear == "all")
                    {
                        int s = 0;
                        foreach (var d in Doors) s += d.Cells.Count - 1;
                        return s;
                    }
                    return Doors[0].Cells.Count - 1;
                }
            }
            /// 🔴 모자라면 못 푼다. 하지만 **남는 건 괜찮다** — 그게 곧 "먹으면 안 되는 조각"이다.
            ///    더 먹으면 몸이 길어져 홈에 안 맞고, 먹은 건 못 뱉는다 = 그 자리에서 진다.
            ///    (08-30: 여기를 '정확히 같아야 한다'로 묶어둔 게 난이도의 족쇄였다)
            public bool LengthAddsUp => Doors.Count == 0 || Foods.Count >= FoodsNeeded;

            /// <summary>
            /// 🔴 이미 연 문의 칸인가 — 굳은 몸이 박혀 있다.
            /// 지나갈 수는 있지만 **딛고 설 수도 있다** (2026-08-30).
            /// 문을 열면 그 자리가 영구히 지형이 된다 — 내가 세계를 조각한다.
            /// </summary>
            public bool IsSpent(int cell, int dm)
            {
                for (int i = 0; i < Doors.Count; i++)
                    if ((dm & (1 << i)) != 0 && Doors[i].Set.Contains(cell)) return true;
                return false;
            }

            /// 🔴 지금 이 칸이 막혀 있나 — 문벽은 짝이 되는 문을 열면 사라진다.
            public bool IsBlocked(int cell, int dm)
            {
                if (Wall[cell]) return true;
                if (!HasGates) return false;
                for (int i = 0; i < Gates.Length; i++)
                    if (Gates[i].Contains(cell) && (dm & (1 << i)) == 0) return true;
                return false;
            }
        }

        /// 몸은 머리부터 꼬리 순. Fm = 먹은 먹이 비트마스크.
        public sealed class State
        {
            public List<int> Body;
            public int Fm;
            /// 🔴 낙하 중에 먹어서 아직 몸에 안 붙은 성장 횟수. 다음 걸음마다 하나씩 갚는다.
            public int Pg;
            /// 🔴 열린 문 비트마스크
            public int Dm;
            /// 🔴 별을 주웠나. 이기는 것과는 상관없고, 별 개수에만 쓴다.
            public int Sc;
            /// 🔴 받침대 중 어디에 몸을 놓았나 (비트마스크)
            public int Pm;
            public State(List<int> body, int fm, int pg = 0, int dm = 0, int sc = 0, int pm = 0)
            { Body = body; Fm = fm; Pg = pg; Dm = dm; Sc = sc; Pm = pm; }
            public int Head => Body[0];
            public int Length => Body.Count;
            public State Clone() => new State(new List<int>(Body), Fm, Pg, Dm, Sc, Pm);
        }

        /// 문자 격자를 판으로.
        ///   # 벽 · . 빈칸 · S 시작(머리) · + 조각 · = 채워야 할 칸 · * 심(채워야 할 칸이면서 끝나는 자리)
        public static Level Parse(string[] grid, string id = "test", bool gravity = false, string clear = "any")
        {
            var L = new Level { Id = id, H = grid.Length, W = grid[0].Length, Start = -1, Gravity = gravity,
                                Clear = string.IsNullOrEmpty(clear) ? "any" : clear };
            L.Wall = new bool[L.W * L.H];
            var d0 = new Level.Door();     // = *
            var d1 = new Level.Door();     // - %
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
                        case '=': d0.Cells.Add(c); break;
                        case '*': d0.Cells.Add(c); d0.Core = c; break;
                        case '-': d1.Cells.Add(c); break;
                        case '%': d1.Cells.Add(c); d1.Core = c; break;
                        case '1': case '2': case '3':
                            L.Gates[grid[y][x] - '1'].Add(c); L.HasGates = true; break;
                        case 'o': L.Star = c; break;
                        case 'T': L.PadIdx[c] = L.Pads.Count; L.Pads.Add(c); break;
                        case '^': L.Signs.Add((c, 0)); break;
                        case 'v': L.Signs.Add((c, 1)); break;
                        case '<': L.Signs.Add((c, 2)); break;
                        case '>': L.Signs.Add((c, 3)); break;
                    }
                }
            }
            if (L.Start < 0) throw new System.ArgumentException($"{id}: S가 없다");
            if (L.Foods.Count > MaxFoods) throw new System.ArgumentException($"{id}: 조각이 {MaxFoods}개를 넘는다");

            foreach (var d in new[] { d0, d1 })
                if (d.Cells.Count > 0) { d.Set = new HashSet<int>(d.Cells); L.Doors.Add(d); }
            // 🔴 문마다 칸 수 제약은 Clear에 달렸다 (JS engine.js와 같아야 한다).
            //    "any" — 아무 문이나 하나 채우고 끝난다. 길이는 한 번뿐이니 칸 수가 같아야 한다
            //    "all" — 문을 차례로 채운다. 채울 때마다 몸을 두고 다시 먹으므로 **달라도 된다**
            if (L.Clear == "any")
                for (int i = 1; i < L.Doors.Count; i++)
                    if (L.Doors[i].Cells.Count != L.Doors[0].Cells.Count)
                        throw new System.ArgumentException(
                            $"{id}: 문마다 칸 수가 다르다 ({L.Doors[0].Cells.Count} vs {L.Doors[i].Cells.Count}) — 아무 문이나 여는 판은 길이가 하나뿐이다");

            L.AllDoors = (1 << L.Doors.Count) - 1;
            if (L.Doors.Count > 0) { L.Target = L.Doors[0].Cells; L.Core = L.Doors[0].Core; }
            L.TargetSet = new HashSet<int>(L.Target);
            // 🔴 길이가 안 맞으면 애초에 못 푸는 판이다. 판을 만들 때 바로 알아야 한다.
            if (!L.LengthAddsUp)
                throw new System.ArgumentException(
                    $"{id}: 조각이 모자란다 — {L.FoodsNeeded}개가 필요한데 {L.Foods.Count}개다");
            return L;
        }

        /// 몸의 어느 칸이든 바로 아래가 벽이면 버틴다.
        static bool Supported(Level L, List<int> body, int dm, int pm)
        {
            foreach (int c in body)
            {
                int below = c + L.W;
                if (below >= L.W * L.H || L.IsBlocked(below, dm)) return true;
                if (L.IsSpent(below, dm)) return true;    // 🔴 두고 온 몸은 디딤돌이다
                // 받침대에 놓고 온 몸도 디딤돌이다
                if (L.PadIdx.TryGetValue(below, out int pi) && (pm & (1 << pi)) != 0) return true;
            }
            return false;
        }

        /// 지지될 때까지 떨어뜨린다. 떨어질 데가 없으면 false = 그 걸음은 불가.
        /// <summary>
        /// 🔴 떨어지는 동안에도 **머리가 지나가는 조각을 먹는다** (2026-08-29).
        /// 전에는 걸음에만 구현돼 있어서 조각 위로 떨어지면 안 먹혔다. 사람이 바로 부딪혔다.
        /// 다만 낙하 중엔 꼬리가 물러날 자리가 없다 — 거기에 마디를 붙이면 몸이 겹친다.
        /// 그래서 **다음 걸음에 꼬리가 안 물러나는 것**으로 갚는다 (pg, 고전 스네이크와 같다).
        /// </summary>
        static bool Settle(Level L, List<int> body, ref int fm, ref int pg, int dm, ref int sc, int pm)
        {
            if (L.Star >= 0 && body[0] == L.Star) sc = 1;
            if (!L.Gravity) return true;
            for (int guard = 0; guard < 64; guard++)
            {
                if (Supported(L, body, dm, pm)) return true;
                for (int i = 0; i < body.Count; i++)
                {
                    int next = body[i] + L.W;
                    if (next >= L.W * L.H) return false;
                    body[i] = next;
                }
                int fi = L.Foods.IndexOf(body[0]);       // 머리가 새로 들어간 칸
                if (fi >= 0 && (fm & (1 << fi)) == 0) { fm |= (1 << fi); pg++; }
                if (L.Star >= 0 && body[0] == L.Star) sc = 1;   // 떨어지며 지나가도 줍는다
            }
            return false;
        }

        public static State StartState(Level L)
        {
            var body = new List<int> { L.Start };
            int fm = 0, pg = 0, sc = 0;
            Settle(L, body, ref fm, ref pg, 0, ref sc, 0);   // 시작하자마자 떨어지며 먹을 수도 있다
            return new State(body, fm, pg, 0, sc);
        }

        public static bool IsEaten(State st, int foodIndex) => (st.Fm & (1 << foodIndex)) != 0;

        /// 🔴 클리어 — 몸이 목표 칸을 **정확히** 덮고, 심이 있으면 **머리가 거기** 있어야 한다.
        ///    남아도 안 되고 모자라도 안 된다.
        /// <summary>지금 몸이 어느 문에 딱 맞나. 아니면 -1.</summary>
        public static int MatchDoor(Level L, State st)
        {
            for (int i = 0; i < L.Doors.Count; i++)
            {
                var d = L.Doors[i];
                if (st.Body.Count != d.Cells.Count) continue;
                bool all = true;
                foreach (int c in st.Body) if (!d.Set.Contains(c)) { all = false; break; }
                if (!all) continue;
                if (d.Core >= 0 && st.Head != d.Core) continue;
                return i;
            }
            return -1;
        }

        /// <summary>이 판이 끝났나.</summary>
        public static bool IsWin(Level L, State st)
        {
            bool doors = L.Clear == "all"
                ? (L.AllDoors != 0 && st.Dm == L.AllDoors)
                : (st.Dm != 0 || MatchDoor(L, st) >= 0);
            if (!doors) return false;
            // 🔴 홈을 채우면 **거기서 끝난다.** 출구까지 걸어가는 건 없앴다 (08-30 사장님).
            //    출구 그림은 남아 있지만 연출일 뿐이다 — 밟으러 갈 필요가 없다.
            return true;
        }

        /// <summary>어느 문이 열렸는지 물을 때 (화면용).</summary>
        public static int WonDoor(Level L, State st) => MatchDoor(L, st);

        /// 목표 중 지금 몸이 덮고 있는 칸 수 (화면에 "몇 칸 남았는지" 보여주려고)
        public static int Filled(Level L, State st)
        {
            int n = 0;
            foreach (int c in st.Body) if (L.TargetSet.Contains(c)) n++;
            return n;
        }

        /// 한 걸음. 못 가면 false (아무 일도 안 일어난다).
        /// <summary>
        /// 🔴 받침대에 몸을 놓는다. **몸 전체가 받침대 안에** 있어야 하고, 통째로 놓는다.
        /// 일부만 떼게 하면 덤 조각을 먹고도 살아나서 난이도가 통째로 사라진다.
        /// </summary>
        public static bool Drop(Level L, State st, out State result)
        {
            result = null;
            if (L.Pads.Count == 0 || st.Body.Count < 2) return false;
            int mask = 0;
            foreach (int c in st.Body)
            {
                if (!L.PadIdx.TryGetValue(c, out int i)) return false;   // 하나라도 밖이면 못 놓는다
                if ((st.Pm & (1 << i)) != 0) return false;               // 이미 찬 자리엔 못 놓는다
                mask |= 1 << i;
            }
            int pm = st.Pm | mask;
            var body = new List<int> { st.Head };
            int fm = st.Fm, pg = 0, sc = st.Sc;
            if (!Settle(L, body, ref fm, ref pg, st.Dm, ref sc, pm)) return false;
            result = new State(body, fm, pg, st.Dm, sc, pm);
            return true;
        }

        public static bool Step(Level L, State st, Dir dir, out State result)
        {
            if (dir == Dir.Drop) return Drop(L, st, out result);
            result = null;
            var (dx, dy) = Delta[(int)dir];
            int hx = L.X(st.Head) + dx, hy = L.Y(st.Head) + dy;
            if (hx < 0 || hy < 0 || hx >= L.W || hy >= L.H) return false;

            int nh = hy * L.W + hx;
            if (L.IsBlocked(nh, st.Dm)) return false;

            int fi = L.Foods.IndexOf(nh);
            bool grows = fi >= 0 && !IsEaten(st, fi);

            // 꼬리는 비켜나니 밟아도 된다 — 단 이번에 자라지 않을 때만
            int blocked = grows ? st.Body.Count : st.Body.Count - 1;
            for (int i = 0; i < blocked; i++) if (st.Body[i] == nh) return false;

            var body = new List<int>(st.Body.Count + 1) { nh };
            body.AddRange(st.Body);
            int pg = st.Pg;
            // 자라는 걸음이면 꼬리를 그대로 둔다. 아니면 낙하 중에 진 빚(pg)부터 갚는다.
            if (!grows) { if (pg > 0) pg--; else body.RemoveAt(body.Count - 1); }

            int fm = grows ? (st.Fm | (1 << fi)) : st.Fm;
            int sc = st.Sc;
            if (L.Star >= 0 && nh == L.Star) sc = 1;
            if (!Settle(L, body, ref fm, ref pg, st.Dm, ref sc, st.Pm)) return false;   // 🔬 중력 — 떨어질 데가 없으면 못 간다

            var ns = new State(body, fm, pg, st.Dm, sc, st.Pm);

            // 🔴 문이 채워졌나 — 채워졌으면 **몸을 자물쇠에 두고 핵만 남는다.**
            int opened = MatchDoor(L, ns);
            if (opened >= 0 && (ns.Dm & (1 << opened)) == 0)
            {
                ns.Dm |= (1 << opened);
                if (L.Clear == "all")
                {
                    int core = L.Doors[opened].Core >= 0 ? L.Doors[opened].Core : ns.Body[0];
                    ns.Body = new List<int> { core };
                    ns.Pg = 0;                       // 두고 온 몸과 함께 미룬 성장도 사라진다
                }
                // 🔴 문벽이 사라지면 발밑이 없어질 수 있다 — 그러면 떨어진다
                int f2 = ns.Fm, p2 = ns.Pg, s2 = ns.Sc;
                if (!Settle(L, ns.Body, ref f2, ref p2, ns.Dm, ref s2, ns.Pm)) return false;
                ns.Fm = f2; ns.Pg = p2; ns.Sc = s2;
            }

            result = ns;
            return true;
        }
    }
}
