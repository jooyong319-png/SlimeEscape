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
            /// 🔴 무중력 구역 — 벽이 아니라 **뒷벽**이다. 기호 ':' (rev.10, 2026-09-05).
            /// 지나갈 수 있고, 몸의 한 칸이라도 여기 걸쳐 있으면 **안 떨어진다.**
            /// 중력이 이 게임의 축이라, 그걸 국소적으로 끄면 오르기 규칙(길이 ≥ k+1)이
            /// 통째로 비켜간다. 🔴 tools/engine.js 의 zoneSet 과 **같아야 한다.**
            /// </summary>
            public HashSet<int> Zone = new HashSet<int>();

            /// <summary>
            /// 🔴 미는 상자의 **처음 자리**. 기호 'B' (rev.10, 2026-09-05).
            /// 가로로만 민다. 떨어지고, **딛고 설 수 있다** — 발판을 스스로 만드는 것이 요점이다.
            /// ⚠️ 지금 자리는 판이 아니라 <see cref="State.Bx"/> 에 있다. 상태가 커진다.
            /// 🔴 tools/engine.js 의 boxes 와 **같아야 한다.**
            /// </summary>
            public List<int> Boxes = new List<int>();

            /// <summary>
            /// 🔴 얼음 'I' (rev.12) — **좌우로 들어가면 멈출 수 없다.** 막힐 때까지 같은 쪽으로 간다.
            /// 원하는 칸에 서려면 **먼저 뭔가를 갖다 놔야** 한다 — 그 '뭔가'가 자기 몸이다.
            /// 🔴 tools/engine.js 의 iceSet 과 **같아야 한다.**
            /// </summary>
            public HashSet<int> Ice = new HashSet<int>();

            /// <summary>
            /// 🔴 금 간 바닥 'X' (rev.12) — **벽처럼 딛고 서지만 몸이 지나가면 부서진다.**
            /// 되돌아갈 수 없으니 순서를 처음부터 정해야 한다. 죽는 건 없다 — 계획 문제다.
            /// ⚠️ 부서진 자리가 상태(<see cref="State.Cm"/>)에 들어간다. 한 판에 열 칸을 넘기지 않는다.
            /// 🔴 tools/engine.js 의 crack 과 **같아야 한다.**
            /// </summary>
            public List<int> Crack = new List<int>();
            public Dictionary<int, int> CrackIdx = new Dictionary<int, int>();
            public bool HasCrack;

            /// 🔴 지금 몸이 얹고 있는 금 간 바닥들의 비트. (몸 칸의 **바로 아래**가 금 간 바닥이면 얹힌 것)
            public int LoadMask(List<int> body)
            {
                int m = 0;
                foreach (int c in body)
                    if (CrackIdx.TryGetValue(c + W, out int i)) m |= 1 << i;
                return m;
            }

            /// <summary>
            /// 🔴 포탈 (rev.11) — 칸 -> (나갈 칸, 몇 번 홈이 켜는가).
            /// **그 홈을 채워야 켜진다.** 꺼져 있으면 그냥 바닥이다.
            /// P↔Q 는 홈 1 · p↔q 는 홈 2. 홈 셋짜리는 안 만든다 —
            /// 마지막 홈을 채우면 판이 끝나서 쓸 데가 없다 (문벽이 1·2 만 뜻이 있는 것과 같다).
            /// 🔴 tools/engine.js 의 portal 과 **같아야 한다.**
            /// </summary>
            public Dictionary<int, (int To, int Door)> Portal = new Dictionary<int, (int, int)>();

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
            public bool IsBlocked(int cell, int dm, int cm = 0)
            {
                if (Wall[cell]) return true;
                //  🔴 금 간 바닥 — 아직 안 부서졌으면 **벽이다.** 부서지면 그냥 빈칸이 된다.
                if (HasCrack && CrackIdx.TryGetValue(cell, out int ci) && (cm & (1 << ci)) == 0)
                    return true;
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
            /// 🔴 지금 상자들이 있는 자리 (오름차순). 없으면 빈 목록.
            public List<int> Bx = new List<int>();
            /// 🔴 이미 부서진 금 간 바닥 (비트마스크). 한 번 부서지면 되돌아오지 않는다.
            public int Cm;
            /// 🔴 얼음 위를 계속 미끄러질 수 있는 걸음이었나 (화면·미끄러짐 판정용, 상태 열쇠엔 안 들어간다)
            public bool Sl;
            public State(List<int> body, int fm, int pg = 0, int dm = 0, int sc = 0, int pm = 0,
                         List<int> bx = null, int cm = 0)
            { Body = body; Fm = fm; Pg = pg; Dm = dm; Sc = sc; Pm = pm; Cm = cm;
              Bx = bx != null ? new List<int>(bx) : new List<int>(); }
            public int Head => Body[0];
            public int Length => Body.Count;
        }

        /// 문자 격자를 판으로.
        ///   # 벽 · . 빈칸 · S 시작(머리) · + 조각 · = 채워야 할 칸 · * 심(채워야 할 칸이면서 끝나는 자리)
        public static Level Parse(string[] grid, string id = "test", bool gravity = false, string clear = "any")
        {
            var L = new Level { Id = id, H = grid.Length, W = grid[0].Length, Start = -1, Gravity = gravity,
                                Clear = string.IsNullOrEmpty(clear) ? "any" : clear };
            L.Wall = new bool[L.W * L.H];
            //  포탈 짝 — port[i,0] ↔ port[i,1], i 번 홈이 켠다
            var port = new int[2, 2] { { -1, -1 }, { -1, -1 } };
            var d0 = new Level.Door();     // = *
            var d1 = new Level.Door();     // - %
            var d2 = new Level.Door();     // ~ @
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
                        case '~': d2.Cells.Add(c); break;
                        case '@': d2.Cells.Add(c); d2.Core = c; break;
                        case '1': case '2': case '3':
                            L.Gates[grid[y][x] - '1'].Add(c); L.HasGates = true; break;
                        case ':': L.Zone.Add(c); break;
                        case 'B': L.Boxes.Add(c); break;
                        case 'I': L.Ice.Add(c); break;
                        case 'X': L.CrackIdx[c] = L.Crack.Count; L.Crack.Add(c);
                                  L.HasCrack = true; break;
                        case 'P': port[0, 0] = c; break;
                        case 'Q': port[0, 1] = c; break;
                        case 'p': port[1, 0] = c; break;
                        case 'q': port[1, 1] = c; break;
                        case 'o': L.Star = c; break;
                        case 'T': L.PadIdx[c] = L.Pads.Count; L.Pads.Add(c); break;
                        case '^': L.Signs.Add((c, 0)); break;
                        case 'v': L.Signs.Add((c, 1)); break;
                        case '<': L.Signs.Add((c, 2)); break;
                        case '>': L.Signs.Add((c, 3)); break;
                    }
                }
            }
            for (int i = 0; i < 2; i++)
            {
                int a = port[i, 0], b = port[i, 1];
                if (a < 0 || b < 0) continue;         // 짝이 하나만 있으면 안 쓴다
                L.Portal[a] = (b, i); L.Portal[b] = (a, i);
            }
            if (L.Start < 0) throw new System.ArgumentException($"{id}: S가 없다");
            if (L.Foods.Count > MaxFoods) throw new System.ArgumentException($"{id}: 조각이 {MaxFoods}개를 넘는다");

            foreach (var d in new[] { d0, d1, d2 })
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
        /// 🔴 상자를 받쳐주는 것: 판 밖 · 벽 · 닫힌 문벽 · 굳은 몸 · 다른 상자 · **슬라임 몸**.
        /// ⚠️ 슬라임은 자기 몸을 못 딛지만 **상자는 슬라임을 딛는다.**
        ///    안 그러면 몸 위로 상자가 통과해 내려가 그림이 무너진다.
        /// 🔴 tools/engine.js 의 boxHeld 와 **같아야 한다.**
        static bool BoxHeld(Level L, int c, int dm, List<int> bx, List<int> body, int cm)
        {
            int below = c + L.W;
            if (below >= L.W * L.H) return true;
            if (L.IsBlocked(below, dm, cm)) return true;
            if (L.IsSpent(below, dm)) return true;
            if (bx.Contains(below)) return true;
            if (body.Contains(below)) return true;
            return false;
        }

        static bool Supported(Level L, List<int> body, int dm, int pm, List<int> bx, int cm)
        {
            // 🔴 **포탈에 걸쳐 있는 동안은 안 떨어진다** (rev.11, 09-05 사장님 신고).
            //    몸이 포탈을 지나는 동안 두 덩이로 갈리는데, 중력은 몸을 **한 덩어리로** 떨어뜨린다.
            //    그래서 이쪽이 발판을 잃으면 **저쪽 머리까지 포탈 밖으로 흘러내렸다.**
            //    갈려 있는 동안은 포탈이 붙들고 있는 것으로 본다.
            //    🔴 tools/engine.js 의 supported 와 **같아야 한다.**
            if (L.Portal.Count > 0)
            {
                for (int i = 1; i < body.Count; i++)
                {
                    int a = body[i - 1], b = body[i];
                    int dx = System.Math.Abs(L.X(a) - L.X(b));
                    int dy = System.Math.Abs(L.Y(a) - L.Y(b));
                    if (dx + dy != 1) return true;      // 사슬이 끊겨 있다 = 걸쳐 있다
                }
            }
            foreach (int c in body)
            {
                //  🔴 무중력 구역에 한 칸이라도 걸쳐 있으면 안 떨어진다 (rev.10)
                if (L.Zone.Count > 0 && L.Zone.Contains(c)) return true;
                // 🔴 상자 위에는 설 수 있다 (rev.10)
                if (bx != null && bx.Contains(c + L.W)) return true;
                int below = c + L.W;
                if (below >= L.W * L.H || L.IsBlocked(below, dm, cm)) return true;
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
        static bool Settle(Level L, List<int> body, ref int fm, ref int pg, int dm, ref int sc, int pm,
                           List<int> bx, ref int cm, int lp)
        {
            //  🔴 금 간 바닥: lp = **이 걸음 전에** 몸이 얹고 있던 칸들. 거기서 벗어나면 부서진다.
            if (lp < 0) lp = L.HasCrack ? L.LoadMask(body) : 0;
            if (L.Star >= 0 && body[0] == L.Star) sc = 1;
            if (!L.Gravity) return true;
            for (int guard = 0; guard < 128; guard++)
            {
                //  🔴 벗어난 금 간 바닥을 여기서 부순다 — 떨어지는 도중에도 계속 무너진다.
                if (L.HasCrack) { int now = L.LoadMask(body); cm |= lp & ~now; lp = now; }
                if (!Supported(L, body, dm, pm, bx, cm))
                {
                    for (int i = 0; i < body.Count; i++)
                    {
                        int next = body[i] + L.W;
                        if (next >= L.W * L.H) return false;
                        body[i] = next;
                    }
                    int fi = L.Foods.IndexOf(body[0]);       // 머리가 새로 들어간 칸
                    if (fi >= 0 && (fm & (1 << fi)) == 0) { fm |= (1 << fi); pg++; }
                    if (L.Star >= 0 && body[0] == L.Star) sc = 1;   // 떨어지며 지나가도 줍는다
                    continue;
                }
                //  🔴 몸이 멈춘 뒤에 상자를 떨어뜨린다. **아래 것부터** — 아래가 비켜야 위가 내려온다.
                if (bx == null || bx.Count == 0) return true;
                int pick = -1;
                for (int i = 0; i < bx.Count; i++)
                    if (!BoxHeld(L, bx[i], dm, bx, body, cm) && (pick < 0 || bx[i] > bx[pick])) pick = i;
                if (pick < 0) return true;
                int nb = bx[pick] + L.W;
                if (nb >= L.W * L.H) return false;
                bx[pick] = nb;
            }
            return false;
        }

        public static State StartState(Level L)
        {
            var body = new List<int> { L.Start };
            int fm = 0, pg = 0, sc = 0;
            var bx = new List<int>(L.Boxes);
            int cm = 0;
            Settle(L, body, ref fm, ref pg, 0, ref sc, 0, bx, ref cm, -1);   // 시작하자마자 떨어지며 먹을 수도 있다
            return new State(body, fm, pg, 0, sc, 0, bx, cm);
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
            var bxd = new List<int>(st.Bx);
            //  🔴 놓고 온 몸이 얹고 있던 금 간 바닥은 무너진다
            int cmd = st.Cm;
            if (!Settle(L, body, ref fm, ref pg, st.Dm, ref sc, pm, bxd, ref cmd,
                        L.HasCrack ? L.LoadMask(st.Body) : 0)) return false;
            result = new State(body, fm, pg, st.Dm, sc, pm, bxd, cmd);
            return true;
        }

        /// 한 칸 움직인다. 얼음 미끄러짐은 Step() 이 이걸 여러 번 부른다.
        static bool Move1(Level L, State st, Dir dir, out State result)
        {
            result = null;
            var (dx, dy) = Delta[(int)dir];
            int hx = L.X(st.Head) + dx, hy = L.Y(st.Head) + dy;
            if (hx < 0 || hy < 0 || hx >= L.W || hy >= L.H) return false;

            if (L.IsBlocked(hy * L.W + hx, st.Dm, st.Cm)) return false;

            // 🔴 포탈 (rev.11) — **걸어 들어갈 때만** 통한다.
            //    ⚠️ 떨어져 들어가도 통하게 하면 *떨어짐 → 나감 → 또 떨어짐* 의 무한 고리가 된다.
            //       그래서 Settle 은 포탈을 안 본다. 포탈 위에 내려앉는 건 그냥 그 칸에 서는 것이다.
            //    ⚠️ 한 번만 통한다 — 나간 자리가 또 포탈이어도 다시 안 보낸다.
            int nh = hy * L.W + hx;
            bool warped = false;
            if (L.Portal.Count > 0 && L.Portal.TryGetValue(nh, out var hop)
                && (st.Dm & (1 << hop.Door)) != 0)
            {
                nh = hop.To;
                warped = true;
                if (L.IsBlocked(nh, st.Dm, st.Cm)) return false;
            }

            // 🔴 상자를 민다 — **가로로만.** 위아래로 밀면 중력과 뜻이 겹쳐 읽히지 않는다.
            //    🔴 tools/engine.js 의 같은 자리와 규칙이 **한 글자도 달라선 안 된다.**
            var bx = new List<int>(st.Bx);
            bool pushed = false;
            int bi = bx.IndexOf(nh);
            if (bi >= 0)
            {
                if (dir != Dir.Left && dir != Dir.Right) return false;   // 위·아래로는 못 민다
                //  🔴 포탈로 나온 자리에서는 못 민다 — 어느 쪽으로 미는지가 뜻을 잃는다
                if (warped) return false;
                int tx = hx + dx;
                if (tx < 0 || tx >= L.W) return false;
                int tc = hy * L.W + tx;
                if (L.IsBlocked(tc, st.Dm, st.Cm)) return false;
                if (L.IsSpent(tc, st.Dm)) return false;      // 굳은 몸이 박힌 칸엔 못 넣는다
                if (bx.Contains(tc)) return false;           // 상자 둘은 한 번에 못 민다
                if (st.Body.Contains(tc)) return false;
                bx[bi] = tc;
                pushed = true;
            }

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
            int cm = st.Cm;
            if (!Settle(L, body, ref fm, ref pg, st.Dm, ref sc, st.Pm, bx, ref cm,
                        L.HasCrack ? L.LoadMask(st.Body) : 0)) return false;   // 🔬 중력 — 떨어질 데가 없으면 못 간다

            var ns = new State(body, fm, pg, st.Dm, sc, st.Pm, bx, cm);
            bool fell = body[0] != nh;

            // 🔴 **포탈로 나온 자리에서 도로 떨어져 오면 그 걸음은 막힌다** (09-05 사장님:
            //    "포탈을 밑에서 위로 가면 이상해"). 나온 자리가 허공이면 그대로 떨어져
            //    **출발 자리로 돌아온다** — 판 반대편으로 번쩍했다 끌려오는 걸로 보인다.
            //    ⚠️ 몸이 길면 떨어진 자리가 **제 몸과 겹친다** — 자기 몸 검사를 포탈 나온
            //       칸에서만 했기 때문이다. 🔴 tools/engine.js 와 **같아야 한다.**
            if (warped)
            {
                if (ns.Body[0] == st.Body[0]) return false;
                for (int i = 1; i < ns.Body.Count; i++)
                    for (int j = 0; j < i; j++)
                        if (ns.Body[i] == ns.Body[j]) return false;
                // 🔴 **사슬은 한 군데만 끊길 수 있다** (09-05 사장님).
                //    몸이 포탈에 걸쳐 있는데 머리가 **또** 들어가면 두 군데가 끊긴다:
                //      ㅁㅁ(포탈)ㅁㅇ  ← 왼쪽 →  ㅁㅇ(포탈)ㅁㅁ
                //    한 몸이 판 여기저기 흩어지는 셈이라 무슨 일이 난 건지 안 읽힌다.
                //    🔴 tools/engine.js 와 **같아야 한다.**
                int breaks = 0;
                for (int i = 1; i < ns.Body.Count; i++)
                {
                    int a = ns.Body[i - 1], b = ns.Body[i];
                    if (System.Math.Abs(L.X(a) - L.X(b)) + System.Math.Abs(L.Y(a) - L.Y(b)) != 1) breaks++;
                }
                if (breaks > 1) return false;
            }

            // 🔴 문이 채워졌나 — 채워졌으면 **몸을 자물쇠에 두고 핵만 남는다.**
            int opened = MatchDoor(L, ns);
            if (opened >= 0 && (ns.Dm & (1 << opened)) == 0)
            {
                var before = ns.Body;
                ns.Dm |= (1 << opened);
                if (L.Clear == "all")
                {
                    int core = L.Doors[opened].Core >= 0 ? L.Doors[opened].Core : ns.Body[0];
                    ns.Body = new List<int> { core };
                    ns.Pg = 0;                       // 두고 온 몸과 함께 미룬 성장도 사라진다
                }
                // 🔴 문벽이 사라지면 발밑이 없어질 수 있다 — 그러면 떨어진다
                int f2 = ns.Fm, p2 = ns.Pg, s2 = ns.Sc, c2 = ns.Cm;
                if (!Settle(L, ns.Body, ref f2, ref p2, ns.Dm, ref s2, ns.Pm, ns.Bx, ref c2,
                            L.HasCrack ? L.LoadMask(before) : 0)) return false;
                ns.Fm = f2; ns.Pg = p2; ns.Sc = s2; ns.Cm = c2;
            }

            //  🔴 얼음을 계속 탈 수 있는 걸음인가. 걸리는 건 전부 "붙잡을 것이 생겼다" 는 뜻이다.
            ns.Sl = !(grows || pushed || warped || fell || opened >= 0);
            result = ns;
            return true;
        }

        /// <summary>
        /// 🔴 얼음 (rev.12) — **좌우로 들어가면 멈출 수 없다.** 막힐 때까지 같은 쪽으로 간다.
        /// 한 번 누른 것이므로 **걸음 수는 1**이다.
        /// ⚠️ 위아래로는 안 미끄러진다 — 세로는 이미 중력이 뜻을 차지하고 있다.
        /// 🔴 tools/engine.js 의 step 과 **같아야 한다.**
        /// </summary>
        public static bool Step(Level L, State st, Dir dir, out State result)
        {
            if (dir == Dir.Drop) return Drop(L, st, out result);
            if (!Move1(L, st, dir, out result)) return false;
            if (L.Ice.Count > 0 && (dir == Dir.Left || dir == Dir.Right))
            {
                for (int guard = 0; guard < 96; guard++)
                {
                    if (!result.Sl || !L.Ice.Contains(result.Head)) break;
                    if (!Move1(L, result, dir, out State nx)) break;
                    result = nx;
                }
            }
            return true;
        }
    }
}
