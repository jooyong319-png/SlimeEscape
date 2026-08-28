using System.Collections.Generic;
using UnityEngine;

namespace SlimeEscape
{
    /// <summary>
    /// rev.4 — 뱀 움직임만. 판 설계는 아직 안 한다 (테스트 맵 하나뿐).
    ///
    /// 이번에 볼 것은 둘뿐이다:
    ///   1. 머리를 4방향으로 미는 감이 괜찮은가
    ///   2. 먹어서 길어질 때 몸이 따라오는 게 자연스러운가
    ///
    /// 화면은 전부 런타임에 만든다. 그레이박스 — 네모와 격자선만.
    /// </summary>
    public class SnakeController : MonoBehaviour
    {
        // ⚠ 판 크기는 이제 고정이 아니다. 판마다 grid에서 읽는다(_L.W/_L.H).
        //    이 상수는 판이 없을 때의 기본값으로만 쓴다.
        public const int BoardW = 20, BoardH = 12;

        static readonly Color BgCol   = new Color32(0x0f, 0x16, 0x14, 0xff);
        static readonly Color Floor   = new Color32(0x18, 0x22, 0x1e, 0xff);
        static readonly Color Rock    = new Color32(0x25, 0x32, 0x2c, 0xff);
        static readonly Color Grid    = new Color32(0x2e, 0x3d, 0x36, 0xff);
        static readonly Color HeadCol = new Color32(0xb8, 0xeb, 0xd3, 0xff);
        static readonly Color BodyCol = new Color32(0x8d, 0xce, 0xb0, 0xff);
        static readonly Color FoodCol = new Color32(0xf3, 0x8a, 0x04, 0xff);
        // 🔴 목표 홈 — 바닥(0x18221e)과 색이 거의 같아 안 보였다.
        //    속은 훨씬 어둡게 파고, 민트 테두리를 둘러 확실히 띄운다.
        static readonly Color HoleCol   = new Color32(0x0a, 0x11, 0x0e, 0xff);  // 파인 속
        static readonly Color HoleEdge  = new Color32(0x7c, 0xc0, 0x9f, 0xff);  // 테두리(민트)
        static readonly Color HoleFill  = new Color32(0x35, 0x5c, 0x49, 0xff);  // 몸이 덮은 칸
        static readonly Color CoreCol   = new Color32(0xf0, 0xc0, 0x5a, 0xff);  // 심
        static readonly Color CoreRing  = new Color32(0x8a, 0x66, 0x22, 0xff);

        // ---- 손맛 수치 (게임 안에서 K로 조절) ----
        [System.Serializable]
        public class Knobs
        {
            public float stepTime = 0.10f;    // 한 칸 가는 시간
            /// 뒷마디가 얼마나 늦게 출발하나. 1이면 머리와 동시, 0에 가까울수록 채찍처럼 끌린다.
            /// 🔴 위치를 어긋내는 게 아니라 **시간차**다 — 끝나면 마디는 정확히 자기 칸에 앉는다.
            public float follow = 0.72f;
            public float segmentSize = 0.86f; // 마디를 칸보다 얼마나 작게 그리나
            public float growPop = 0.25f;     // 길어질 때 꼬리가 한 번 부푸는 정도

            /// 🔴 한 칸 떨어지는 데 걸리는 시간. 여러 칸이면 √칸수로 늘어난다.
            ///    이게 없을 때는 몇 칸을 떨어지든 stepTime 안에 끝나서 순간이동처럼 보였다.
            public float fallTime = 0.16f;
            /// 착지할 때 납작해지는 정도. 🔴 세로로만 눌린다 — 넓어지면 한 칸을 넘는다.
            public float landSquash = 0.5f;
        }
        public Knobs K = new Knobs();
        const string KnobKey = "snakeKnobs";

        SnakeEngine.Level _L;
        SnakeEngine.State _st;
        readonly Stack<SnakeEngine.State> _undo = new Stack<SnakeEngine.State>();

        Camera _cam;
        Transform _root;
        readonly List<SpriteRenderer> _segs = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _foodViews = new List<SpriteRenderer>();
        readonly Dictionary<int, SpriteRenderer> _holes = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, SpriteRenderer> _holeEdges = new Dictionary<int, SpriteRenderer>();
        bool _won;
        float _wonAt;

        /// 🔴 중력은 이제 기본이다 (2026-08-28 확정 — 사장님이 직접 해보고 "중력 있는 게 더 나을 듯").
        ///    G는 비교용으로 남겨 둔다. 이 판은 솔버로 양쪽 다 검증했다 (켬 36걸음 / 끔 35걸음).
        bool _gravity = true;

        // ---- 판 진행 ----
        SnakeLevelSetJson _set;
        int _index;
        // 🔴 판을 통째로 갈면 키도 바꾼다. 안 그러면 id를 재사용한 판이
        //    "이미 깬 판"으로 떠서 건너뛴다. (2026-08-28 rev.5 판 교체)
        const string ProgressKey = "snakeCleared_rev5";   // 깬 판 id를 쉼표로
        readonly HashSet<string> _cleared = new HashSet<string>();
        const float NextLevelDelay = 1.0f;

        // 마디마다 화면 위치를 따로 굴린다 — 뒷마디가 조금 늦게 출발한다
        readonly List<Vector2> _segPos = new List<Vector2>();   // 지금 그려지는 자리
        readonly List<Vector2> _from = new List<Vector2>();     // 이번 걸음을 시작한 자리
        float _pop, _stepT;

        // ---- 낙하 ----
        // 🔴 걸음과 낙하는 다른 동작이다. Step()은 둘을 한 번에 끝내 버리므로,
        //    화면에서는 걸음 -> 낙하 두 구간으로 나눠 그린다.
        int _fall;                                              // 이번 걸음에 떨어진 칸 수
        readonly List<Vector2> _mid = new List<Vector2>();      // 떨어지기 직전 자리
        float _land;                                            // 착지 눌림 (1 -> 0)
        bool _landed = true;                                    // 이번 낙하의 착지를 이미 쳤나
        bool _showPanel;

        // 🔴 검증할 때 테스터에게 개발자 정보가 보이면 안 된다.
        //    "최단 14걸음"을 보면 행동이 바뀌고, N을 누르면 판을 건너뛴다.
        //    F1로만 열린다. 기본은 꺼짐.
        bool _dev;

        // ---- 🔴 게이트 4 기록 ----
        //    플레이어에게는 아무것도 안 보여준다. 파일에만 쌓는다.
        LostSet _lostSet;
        SnakeLog.Run _run;
        float _runStart;
        GUIStyle _sBig, _sMid, _sSmall;

        /// 마디 하나당 출발 지연 (걸음 시간 기준). follow가 1이면 0 = 동시에 움직인다.
        float Lag => (1f - Mathf.Clamp01(K.follow)) * 0.28f;

        /// 낙하에 쓸 시간 — 걸음 시간을 1로 본 값. 🔴 √칸수라 높을수록 길어지되 무한정은 아니다.
        float FallSpan => _fall <= 0 ? 0f
            : Mathf.Sqrt(_fall) * Mathf.Max(0f, K.fallTime) / Mathf.Max(0.02f, K.stepTime);

        void Awake()
        {
            LoadKnobs();

            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _cam = go.AddComponent<Camera>();
            }
            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = BgCol;

            _set = SnakeLevels.Load();
            _gravity = _set.gravity;
            LoadProgress();

            int start = 0;
            while (start < _set.levels.Length && _cleared.Contains(_set.levels[start].id)) start++;
            Load(start >= _set.levels.Length ? 0 : start);
        }

        /// 테스트 맵 하나. 🔴 판 설계가 아니다 — 움직임만 보려고 둔 것이다.
        /// 넓은 빈 방 + 기둥 몇 개 + 먹이 여럿.

        SnakeLevelJson Def => _set.levels[_index];

        void Load(int i)
        {
            _index = Mathf.Clamp(i, 0, _set.levels.Length - 1);
            _L = SnakeLevels.ToLevel(Def, _gravity);
            BuildBoard();

            // 🔴 "여기서부턴 못 이긴다"를 미리 계산해 둔다. 지금 판은 수백 상태라 눈 깜짝할 새다.
            //    플레이어에게 보여주려는 게 아니라, 헛되이 쓴 시간을 기록하려는 것이다.
            _lostSet = new LostSet(_L);
            StartRun();

            Restart();
        }

        void StartRun()
        {
            EndRun();                                  // 앞 판을 안 깨고 넘어갔어도 남긴다
            _run = new SnakeLog.Run { level = Def.id, best = Def.best };
            _runStart = Time.time;
        }

        void EndRun()
        {
            if (_run == null) return;
            if (_run.seconds <= 0f) _run.seconds = Time.time - _runStart;   // 클리어 때 이미 찍었으면 그대로
            if (_run.moves > 0 || _run.cleared) SnakeLog.Add(_run);
            _run = null;
        }

        void OnApplicationQuit() => EndRun();

        void LoadProgress()
        {
            _cleared.Clear();
            foreach (var id in PlayerPrefs.GetString(ProgressKey, "").Split(','))
                if (!string.IsNullOrEmpty(id)) _cleared.Add(id);
        }

        void SaveProgress()
        {
            PlayerPrefs.SetString(ProgressKey, string.Join(",", _cleared));
            PlayerPrefs.Save();
        }

        // ---------------- 화면 ----------------
        Vector2 CellPos(int cell) => new Vector2(_L.X(cell) + 0.5f, -(_L.Y(cell) + 0.5f));

        void BuildBoard()
        {
            if (_root != null) Destroy(_root.gameObject);
            _root = new GameObject("Board").transform;
            _segs.Clear(); _foodViews.Clear();

            for (int y = 0; y < _L.H; y++)
                for (int x = 0; x < _L.W; x++)
                {
                    int c = y * _L.W + x;
                    var sr = NewSprite(_L.IsWall(c) ? "Wall" : "Floor", -3);
                    sr.transform.position = CellPos(c);
                    sr.color = _L.IsWall(c) ? Rock : Floor;
                }

            // 🔴 목표 홈 — 바닥보다 어둡게 파인 것처럼. 몸이 덮으면 한 칸씩 빛이 찬다
            _holes.Clear();
            _holeEdges.Clear();
            foreach (int c in _L.Target)
            {
                // 테두리(민트) 위에 속(아주 어두운 색)을 덮어 프레임처럼 보이게 한다
                var edge = NewSprite("HoleEdge", -1);
                edge.transform.position = CellPos(c);
                edge.transform.localScale = new Vector3(0.96f, 0.96f, 1);
                edge.color = HoleEdge;

                var inner = NewSprite("HoleInner", 0);
                inner.transform.position = CellPos(c);
                inner.transform.localScale = new Vector3(0.80f, 0.80f, 1);
                inner.color = HoleCol;

                _holes[c] = inner;
                _holeEdges[c] = edge;
            }

            // 심 — 머리가 마지막에 있어야 할 자리
            if (_L.Core >= 0)
            {
                var ring = NewSprite("CoreRing", 1);
                ring.transform.position = CellPos(_L.Core);
                ring.transform.localScale = Vector3.one * 0.52f;
                ring.color = CoreRing;

                var core = NewSprite("Core", 2);
                core.transform.position = CellPos(_L.Core);
                core.transform.localScale = Vector3.one * 0.30f;
                core.color = CoreCol;
            }

            for (int x = 1; x < _L.W; x++) GridLine(x, true);
            for (int y = 1; y < _L.H; y++) GridLine(y, false);

            foreach (int c in _L.Foods)
            {
                var sr = NewSprite("Food", 1);
                sr.transform.position = CellPos(c);
                sr.transform.localScale = Vector3.one * 0.4f;
                sr.color = FoodCol;
                _foodViews.Add(sr);
            }

            // 🔴 칸 크기는 판이 달라져도 그대로 둔다.
            //    매번 화면에 꽉 맞추면 9x7이든 20x12든 똑같아 보여서
            //    **판이 넓어지는 걸 플레이어가 못 느낀다.** 작은 판은 가운데 작게 앉는다.
            //    기준(BoardW x BoardH)보다 큰 판이 오면 그때만 물러난다.
            float asp = Mathf.Max(0.1f, _cam.aspect);
            float fitLevel = Mathf.Max(_L.H * 0.5f + 0.6f, (_L.W * 0.5f + 0.6f) / asp);
            float fitRef   = Mathf.Max(BoardH * 0.5f + 0.6f, (BoardW * 0.5f + 0.6f) / asp);
            _cam.transform.position = new Vector3(_L.W * 0.5f, -_L.H * 0.5f, -10);
            _cam.orthographicSize = Mathf.Max(fitRef, fitLevel);
        }

        void GridLine(int i, bool vertical)
        {
            var sr = NewSprite("Grid", -2);
            if (vertical) { sr.transform.position = new Vector3(i, -_L.H * 0.5f, 0); sr.transform.localScale = new Vector3(0.035f, _L.H, 1); }
            else { sr.transform.position = new Vector3(_L.W * 0.5f, -i, 0); sr.transform.localScale = new Vector3(_L.W, 0.035f, 1); }
            sr.color = Grid;
        }

        SpriteRenderer NewSprite(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelSprites.Solid();
            sr.sortingOrder = order;
            return sr;
        }

        void Restart()
        {
            _st = SnakeEngine.StartState(_L);
            _undo.Clear();
            _won = false;
            _pop = 0; _stepT = 999f;
            _fall = 0; _land = 0f; _landed = true;
            _segPos.Clear(); _from.Clear(); _mid.Clear();
            _segPos.Add(CellPos(_st.Head)); _from.Add(_segPos[0]); _mid.Add(_segPos[0]);
            SyncViews();
        }

        /// 마디 스프라이트 개수를 몸 길이에 맞춘다
        void SyncViews()
        {
            while (_segs.Count < _st.Length)
            {
                var sr = NewSprite("Seg", 3 - _segs.Count % 2);   // 머리가 위로
                _segs.Add(sr);
            }
            for (int i = 0; i < _segs.Count; i++) _segs[i].enabled = i < _st.Length;
            for (int i = 0; i < _foodViews.Count; i++) _foodViews[i].enabled = !SnakeEngine.IsEaten(_st, i);

            // 목표 홈에 몸이 들어간 칸은 밝아진다 — "몇 칸 남았는지"가 눈에 보이게
            // 🔴 안 채운 홈만 눈에 띄게 둔다 — 남은 칸이 저절로 세어진다
            var body = new HashSet<int>(_st.Body);
            foreach (var kv in _holes)
            {
                bool covered = body.Contains(kv.Key);
                kv.Value.color = covered ? HoleFill : HoleCol;
                if (_holeEdges.TryGetValue(kv.Key, out var e))
                    e.color = covered
                        ? new Color(HoleEdge.r, HoleEdge.g, HoleEdge.b, 0.30f)
                        : HoleEdge;
            }
        }

        // ---------------- 입력 ----------------
        void Update()
        {
            // 🔴 이미 진 상태에서 보낸 시간을 잰다. 화면엔 아무 표시도 안 한다.
            if (_run != null && !_won && _lostSet != null && _lostSet.IsLost(_st))
                _run.lostSeconds += Time.deltaTime;

            if (Input.GetKeyDown(KeyCode.F1)) { _dev = !_dev; if (!_dev) _showPanel = false; }
            if (_dev && Input.GetKeyDown(KeyCode.K)) _showPanel = !_showPanel;
            // 검증용 — 테스터를 바꿀 때 진행을 지운다
            if (_dev && Input.GetKeyDown(KeyCode.F2))
            {
                PlayerPrefs.DeleteKey(ProgressKey); PlayerPrefs.Save();
                _cleared.Clear(); Load(0); return;
            }
            if (_dev && Input.GetKeyDown(KeyCode.G))
            {
                _gravity = !_gravity;
                Load(_index);
                return;
            }
            // 열렸으면 잠깐 두고 다음 방으로 — 손이 멈추지 않게
            if (_won && _index < _set.levels.Length - 1 && Time.time > _wonAt + NextLevelDelay)
            {
                Load(_index + 1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.R)) { if (_run != null) _run.restart++; Restart(); return; }
            if (_dev && Input.GetKeyDown(KeyCode.N)) { Load(_index + 1); return; }
            if (_dev && Input.GetKeyDown(KeyCode.P)) { Load(_index - 1); return; }
            if (Input.GetKeyDown(KeyCode.Z)) { Undo(); return; }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Step(SnakeEngine.Dir.Up);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Step(SnakeEngine.Dir.Down);
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Step(SnakeEngine.Dir.Left);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Step(SnakeEngine.Dir.Right);

            Animate(Time.deltaTime);
        }

        void Step(SnakeEngine.Dir dir)
        {
            if (_won) return;                 // 열렸으면 그대로 둔다
            int before = _st.Length;
            int fromHead = _st.Head;
            if (!SnakeEngine.Step(_L, _st, dir, out var ns))
            {
                if (_run != null) _run.blocked++;      // 막힌 입력도 답답함의 신호다
                return;
            }
            if (_run != null) _run.moves++;
            _undo.Push(_st);
            _st = ns;
            if (SnakeEngine.IsWin(_L, _st))
            {
                _won = true; _wonAt = Time.time;
                _cleared.Add(Def.id);
                SaveProgress();
                // 🔴 시간은 "열린 순간"에 찍는다. 다음 판으로 넘어가는 1초가 섞이면 안 된다.
                if (_run != null) { _run.cleared = true; _run.seconds = Time.time - _runStart; }
            }

            if (_st.Length > before)
            {
                _pop = K.growPop;
                _segPos.Add(_segPos[_segPos.Count - 1]);               // 새 꼬리는 옛 꼬리 자리에서 시작
            }

            // 🔴 몇 칸을 떨어졌나. Settle은 몸 전체를 같은 칸수만큼 아래로 옮기므로,
            //    머리가 걸음만 했을 때의 줄보다 얼마나 아래에 있는지가 곧 낙하 칸수다.
            int steppedRow = fromHead / _L.W + SnakeEngine.Delta[(int)dir].dy;
            _fall = Mathf.Max(0, _st.Head / _L.W - steppedRow);
            _landed = _fall == 0;

            // 떨어지기 직전 자리 = 최종 자리에서 낙하한 만큼 도로 올린 것
            _mid.Clear();
            for (int i = 0; i < _st.Length; i++) _mid.Add(CellPos(_st.Body[i] - _fall * _L.W));

            // 🔴 이번 걸음의 '출발선'을 지금 그려지는 자리로 잡는다.
            //    (걸음이 끝나기 전에 또 누르면 튀지 않게)
            _from.Clear();
            _from.AddRange(_segPos);
            _stepT = 0f;
            SyncViews();
        }

        void Undo()
        {
            if (_undo.Count == 0) return;
            if (_run != null) _run.undo++;
            _st = _undo.Pop();
            _won = false;
            while (_segPos.Count > _st.Length) _segPos.RemoveAt(_segPos.Count - 1);
            while (_segPos.Count < _st.Length) _segPos.Add(_segPos[_segPos.Count - 1]);
            for (int i = 0; i < _st.Length; i++) _segPos[i] = CellPos(_st.Body[i]);   // 되돌리기는 딱 붙인다
            _from.Clear(); _from.AddRange(_segPos);
            _mid.Clear(); _mid.AddRange(_segPos);
            _stepT = 999f;
            _pop = 0f;
            _fall = 0; _land = 0f; _landed = true;
            SyncViews();
        }

        // ---------------- 움직임 ----------------
        void Animate(float dt)
        {
            // 다 끝났으면 더 안 키운다 (가만히 두면 값이 무한정 커진다)
            float fallSpan = FallSpan;
            float done = 1f + fallSpan + Lag * Mathf.Max(0, _st.Length - 1);
            if (_stepT < done + 1f) _stepT += dt / Mathf.Max(0.02f, K.stepTime);
            _pop = Mathf.Max(0f, _pop - dt / 0.18f);
            _land = Mathf.Max(0f, _land - dt / 0.20f);

            // 머리가 땅에 닿는 순간 한 번만 친다 (마디마다 치면 계속 떨린다)
            if (!_landed && _stepT >= 1f + fallSpan)
            {
                _landed = true;
                _land = Mathf.Clamp01(K.landSquash) * Mathf.Clamp01(_fall / 4f);
            }

            while (_segPos.Count < _st.Length) _segPos.Add(_segPos[_segPos.Count - 1]);
            while (_segPos.Count > _st.Length) _segPos.RemoveAt(_segPos.Count - 1);
            while (_from.Count < _segPos.Count) _from.Add(_segPos[_from.Count]);
            while (_from.Count > _segPos.Count) _from.RemoveAt(_from.Count - 1);
            while (_mid.Count < _segPos.Count) _mid.Add(CellPos(_st.Body[_mid.Count]));
            while (_mid.Count > _segPos.Count) _mid.RemoveAt(_mid.Count - 1);

            // 🔴 마디 i는 '출발선 -> 자기 칸'으로만 간다. 앞 마디 쪽으로 끌어당기지 않는다.
            //    끌리는 느낌은 **출발이 늦는 것**으로 낸다 — 그래야 끝났을 때 정확히 칸에 앉는다.
            float lag = Lag;
            for (int i = 0; i < _st.Length; i++)
            {
                float t = _stepT - i * lag;
                Vector2 end = CellPos(_st.Body[i]);
                if (fallSpan <= 0f)
                {
                    _segPos[i] = Vector2.Lerp(_from[i], end, Ease(Mathf.Clamp01(t)));
                }
                else if (t <= 1f)
                {
                    // 1구간: 걸음. 끝을 부드럽게 놓는다
                    _segPos[i] = Vector2.Lerp(_from[i], _mid[i], Ease(Mathf.Clamp01(t)));
                }
                else
                {
                    // 2구간: 낙하. 🔴 점점 빨라져야 한다 — 걸음의 곡선(감속)을 그대로 쓰면 안 된다
                    float u = Mathf.Clamp01((t - 1f) / fallSpan);
                    _segPos[i] = Vector2.Lerp(_mid[i], end, u * u);
                }
            }

            for (int i = 0; i < _st.Length; i++)
            {
                float s = K.segmentSize;
                if (i == _st.Length - 1) s *= 1f + _pop;               // 새로 붙은 꼬리가 한 번 부푼다
                _segs[i].transform.position = _segPos[i];
                // 🔴 착지 눌림은 세로로만 — 넓히면 한 칸을 넘는다
                _segs[i].transform.localScale = new Vector3(s, s * (1f - _land * 0.35f), 1);
                _segs[i].color = i == 0 ? HeadCol : BodyCol;
            }
        }

        static float Ease(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

        // ---------------- 임시 UI ----------------
        void LoadKnobs()
        {
            var json = PlayerPrefs.GetString(KnobKey, "");
            // 🔴 FromJson은 저장된 JSON에 없는 필드를 기본값이 아니라 0으로 채운다.
            //    손잡이를 새로 추가하면 예전 설정 때문에 그 값이 0이 되어 버린다.
            //    빈 Knobs 위에 덮어써야 없는 필드가 기본값으로 남는다.
            K = new Knobs();
            if (!string.IsNullOrEmpty(json)) { try { JsonUtility.FromJsonOverwrite(json, K); } catch { K = new Knobs(); } }
        }

        void Styles()
        {
            if (_sBig != null) return;
            _sBig = new GUIStyle(GUI.skin.label) { fontSize = 21, alignment = TextAnchor.MiddleCenter };
            _sMid = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            _sSmall = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            _sBig.normal.textColor = new Color(1f, 1f, 1f, 0.88f);
            _sMid.normal.textColor = new Color(1f, 1f, 1f, 0.70f);
            _sSmall.normal.textColor = new Color(1f, 1f, 1f, 0.40f);
        }

        void OnGUI()
        {
            Styles();
            float w = Screen.width, h = Screen.height;
            var def = Def;
            int filled = SnakeEngine.Filled(_L, _st);
            int need = _L.Target.Count;

            // ---- 플레이어가 보는 것 : 이름 · 남은 홈 · 조작. 그게 전부다 ----
            GUI.Label(new Rect(0, 12, w, 28), def.name, _sBig);

            // 남은 홈을 점으로 — 숫자보다 한눈에 들어온다
            string dots = "";
            for (int i = 0; i < need; i++) dots += (i < filled ? "●" : "○") + " ";
            GUI.Label(new Rect(0, 42, w, 22), dots, _sMid);

            if (_won)
            {
                GUI.Label(new Rect(0, h - 40, w, 24), "문이 열렸다", _sMid);
            }
            else
            {
                GUI.Label(new Rect(0, h - 34, w, 22),
                    "← ↑ ↓ →      Z  되돌리기      R  처음부터", _sSmall);

                // 🔴 안내는 필요한 순간에만 뜬다. 늘 떠 있으면 아무도 안 읽는다.
                if (_index == 0 && !_cleared.Contains(def.id))
                    GUI.Label(new Rect(0, h - 62, w, 22),
                        "표시된 칸을 몸으로 정확히 채우면 문이 열린다", _sMid);
                else if (_L.Core >= 0 && filled == need && _st.Length == need && _st.Head != _L.Core)
                    GUI.Label(new Rect(0, h - 62, w, 22),
                        "머리가 노란 칸에서 끝나야 한다", _sMid);
            }

            // ---- 개발자가 보는 것 : F1 ----
            if (!_dev) { GUI.Label(new Rect(w - 60, h - 22, 52, 18), "F1", _sSmall); return; }

            GUI.Box(new Rect(12, 12, 620, 82), GUIContent.none);
            GUILayout.BeginArea(new Rect(22, 18, 600, 70));
            bool onCore = _L.Core < 0 || _st.Head == _L.Core;
            GUILayout.Label($"{_index + 1}/{_set.levels.Length}  {def.id}  {_L.W}x{_L.H}" +
                            (_cleared.Contains(def.id) ? "  (깬 판)" : "") +
                            $"   ·   홈 {filled}/{need}   ·   길이 {_st.Length}/{need}" +
                            (_L.Core >= 0 ? (onCore ? "   ·   머리 심에 있음" : "") : ""));
            GUILayout.Label($"최단 {def.best}걸음 · 이미 진 상태 {def.lost:0.0}% · 헤맴 {def.wander}" +
                            (_lostSet != null && _lostSet.Ready ? $" · 상태 {_lostSet.States}" : " · (못 잼)") +
                            $"      N/P 판   K 손맛   G 중력 {(_gravity ? "켬" : "끔")}");
            GUILayout.Label("기록: " + SnakeLog.Summary() +
                            (_run != null ? $"   ·   지금 판 {Time.time - _runStart:0}초" +
                                            (_run.lostSeconds > 0.5f ? $" (이미 진 상태 {_run.lostSeconds:0}초)" : "") : ""));
            GUILayout.EndArea();

            if (!_showPanel) return;
            var r = new Rect(Screen.width - 322, 12, 310, 212);
            GUI.Box(r, GUIContent.none);
            GUILayout.BeginArea(new Rect(r.x + 12, r.y + 10, r.width - 24, r.height - 20));
            GUILayout.Label("손맛 조절 — K로 닫기");
            K.stepTime = Row("한 칸 시간", K.stepTime, 0.03f, 0.30f);
            K.follow = Row("따라붙는 정도", K.follow, 0f, 1f);
            K.segmentSize = Row("마디 크기", K.segmentSize, 0.5f, 1f);
            K.growPop = Row("길어질 때 부풂", K.growPop, 0f, 0.6f);
            K.fallTime = Row("떨어지는 시간", K.fallTime, 0.03f, 0.50f);
            K.landSquash = Row("착지 눌림", K.landSquash, 0f, 1f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("저장")) { PlayerPrefs.SetString(KnobKey, JsonUtility.ToJson(K)); PlayerPrefs.Save(); }
            if (GUILayout.Button("기본값")) { PlayerPrefs.DeleteKey(KnobKey); K = new Knobs(); }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static float Row(string label, float v, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110));
            v = GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(120));
            GUILayout.Label(v.ToString("0.00"), GUILayout.Width(42));
            GUILayout.EndHorizontal();
            return v;
        }
    }
}
