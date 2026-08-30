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

        // 🔴 둘째 문은 색을 달리한다. 같은 민트로 두면 두 문이 한 덩어리로 보인다.
        static readonly Color Hole2Edge = new Color32(0x7f, 0x9c, 0xd8, 0xff);   // 테두리(푸른빛)
        static readonly Color Hole2Fill = new Color32(0x33, 0x44, 0x66, 0xff);
        /// 문 번호 -> 테두리 색
        Color EdgeOf(int door) => door == 0 ? HoleEdge : Hole2Edge;
        Color FillOf(int door) => door == 0 ? HoleFill : Hole2Fill;

        // 🔴 문벽 — 짝이 되는 문을 열기 전엔 벽. 열면 사라진다.
        //    벽(Rock)과 색을 달리해서 "이건 열릴 수 있는 벽"으로 읽히게 한다.
        static readonly Color GateCol  = new Color32(0x4a, 0x3f, 0x2e, 0xff);
        static readonly Color GateEdge = new Color32(0x8a, 0x74, 0x4a, 0xff);
        /// 🔴 두고 온 몸 — 자물쇠에 남아 굳은 것. 밟고 지나갈 수 있다.
        /// 🔴 두고 온 몸 — 자물쇠에 남아 굳은 것. **딛고 설 수 있다**(2026-08-30).
        //     그래서 홈 색이 아니라 **벽(Rock)과 같은 계열**로 그린다.
        //     디딜 수 있는 것은 디딜 수 있는 것처럼 보여야 한다.
        static readonly Color SpentCol = new Color32(0x3a, 0x4a, 0x43, 0xff);   // 굳은 몸
        static readonly Color SpentTop = new Color32(0x66, 0x7d, 0x72, 0xff);   // 윗면 — 여기 설 수 있다

        readonly Dictionary<int, SpriteRenderer> _gateViews = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, int> _gateOf = new Dictionary<int, int>();   // 칸 -> 문 번호
        readonly Dictionary<int, SpriteRenderer> _gateInner = new Dictionary<int, SpriteRenderer>();
        int _shownDm = -1;    // 지금 화면에 그려진 "열린 문" 상태
        float _gateFade = 1f;

        // ---- 방 ----
        // 🔴 지도 전체를 한 번에 보여주면 "방"이 안 생긴다. 넓은 판 하나로 보일 뿐이다.
        //    문벽을 **다 닫아놓고** 이어진 덩어리를 찾으면 그게 곧 방이다 — 따로 적을 필요가 없다.
        int[] _roomOf;                       // 칸 -> 방 번호 (-1이면 벽)
        readonly List<Rect> _roomBox = new List<Rect>();
        int _room = -1;                      // 지금 비추는 방
        Vector3 _camWant; float _camSizeWant;
        const float CamEase = 6f;            // 방을 옮길 때 미끄러지는 빠르기

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
        /// 이웃한 목표 칸의 속을 이어 붙이는 조각. (칸 두 개, 그림 하나)
        readonly List<(int a, int b, SpriteRenderer sr)> _bridges = new List<(int, int, SpriteRenderer)>();
        readonly Dictionary<int, int> _doorOf = new Dictionary<int, int>();   // 칸 -> 문 번호
        /// 굳은 몸의 윗면. 문이 열린 뒤에만 보인다.
        readonly Dictionary<int, SpriteRenderer> _spentTop = new Dictionary<int, SpriteRenderer>();
        const float SlotInner = 0.82f;   // 홈 속의 크기. 나머지가 테두리로 보인다
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

        // ---- 정답 재생 (개발자 전용, F3) ----
        //    "이게 진짜 깨져?"에 답하는 제일 빠른 방법은 게임이 직접 두는 것이다.
        //    재생 중에는 기록을 안 남긴다 — 검증 데이터가 더러워지면 안 된다.
        bool _replay;
        bool _allDone;   // 마지막 판까지 깼다 — 결과 화면

        // 🔴 어느 문으로 나갔느냐가 다음 방을 정한다. 지도 화면이 필요 없는 갈래다.
        int _wonBy = -1;

        /// 🔴 모은 문양 획 / 전체. 깬 판 목록에서 센다 — 따로 저장하지 않는다.
        (int got, int all) Marks()
        {
            int got = 0, all = 0;
            foreach (var l in _set.levels)
            {
                if (!l.mark) continue;
                all++;
                if (_cleared.Contains(l.id)) got++;
            }
            return (got, all);
        }
        readonly List<string> _trail = new List<string>();   // 지나온 방

        // ---- 안내 화면 ----
        // 🔴 친구 검증에서 나온 첫 마디가 "설명도 없이 하라고만 나온다"였다 (2026-08-29).
        //    브리프 §6이 금지하는 건 **스토리**지 **규칙을 설명하는 그림**이 아니다.
        //    글로 늘어놓지 않고, 실제로 화면에 나오는 색을 그대로 보여주며 한 줄씩 붙인다.
        bool _intro = true;
        const string IntroKey = "snakeSeenIntro";
        readonly Dictionary<Color, Texture2D> _chips = new Dictionary<Color, Texture2D>();

        // ---- 손가락 조작 ----
        // 🔴 휴대폰엔 키보드가 없다. 그리고 한 판에 100걸음을 넘기기도 하므로
        //    스와이프만 두면 손이 아프다 — 방향 패드를 같이 둔다.
        bool Touchy => Application.isMobilePlatform || Input.touchSupported;
        Vector2 _swipeFrom;
        bool _swiping;
        float _uiScale = 1f;
        int _styledAt = -1;          // 화면 높이가 바뀌면 글씨 크기를 다시 잡는다
        int _replayAt;
        float _replayNext;
        const float ReplayStep = 0.32f;
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
            _intro = PlayerPrefs.GetInt(IntroKey, 0) == 0;

            int start = 0;
            while (start < _set.levels.Length && _cleared.Contains(_set.levels[start].id)) start++;
            Load(start >= _set.levels.Length ? 0 : start);
        }

        /// 테스트 맵 하나. 🔴 판 설계가 아니다 — 움직임만 보려고 둔 것이다.
        /// 넓은 빈 방 + 기둥 몇 개 + 먹이 여럿.

        SnakeLevelJson Def => _set.levels[_index];

        int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < _set.levels.Length; i++) if (_set.levels[i].id == id) return i;
            Debug.LogWarning("[길] 모르는 방: " + id);
            return -1;
        }

        void Load(int i)
        {
            _index = Mathf.Clamp(i, 0, _set.levels.Length - 1);
            _wonBy = -1;
            if (_trail.Count == 0 || _trail[_trail.Count - 1] != _set.levels[_index].id)
                _trail.Add(_set.levels[_index].id);
            _L = SnakeLevels.ToLevel(Def, _gravity);
            BuildBoard();

            // 🔴 "여기서부턴 못 이긴다"를 미리 계산해 둔다. 지금 판은 수백 상태라 눈 깜짝할 새다.
            //    플레이어에게 보여주려는 게 아니라, 헛되이 쓴 시간을 기록하려는 것이다.
            _lostSet = new LostSet(_L);
            StartRun();

            Restart();
            AimCamera(true);       // 판을 열 때는 딱 맞춘다
        }

        void StartReplay()
        {
            var sol = Def.sol;
            if (string.IsNullOrEmpty(sol)) { Debug.LogWarning("[재생] 정답 수순이 없다"); return; }
            EndRun();                 // 사람이 한 것까지만 기록하고
            _run = null;              // 재생은 기록에 안 남긴다
            Restart();
            _replay = true;
            _replayAt = 0;
            _replayNext = Time.time + 0.4f;
        }

        void StepReplay()
        {
            var sol = Def.sol;
            if (_replayAt >= sol.Length) { _replay = false; return; }
            char c = sol[_replayAt++];
            _replayNext = Time.time + ReplayStep;
            switch (c)
            {
                case '↑': Step(SnakeEngine.Dir.Up); break;
                case '↓': Step(SnakeEngine.Dir.Down); break;
                case '←': Step(SnakeEngine.Dir.Left); break;
                case '→': Step(SnakeEngine.Dir.Right); break;
                default: Debug.LogWarning("[재생] 모르는 글자 " + c); _replay = false; break;
            }
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

        /// 문벽을 다 닫은 채로 이어진 칸끼리 묶는다 = 방
        void SplitRooms()
        {
            int total = _L.W * _L.H;
            _roomOf = new int[total];
            for (int i = 0; i < total; i++) _roomOf[i] = -1;
            _roomBox.Clear();

            var q = new Queue<int>();
            for (int s = 0; s < total; s++)
            {
                if (_roomOf[s] >= 0 || _L.IsBlocked(s, 0)) continue;
                int id = _roomBox.Count;
                int minX = _L.W, maxX = 0, minY = _L.H, maxY = 0;
                _roomOf[s] = id; q.Enqueue(s);
                while (q.Count > 0)
                {
                    int c = q.Dequeue();
                    int x = _L.X(c), y = _L.Y(c);
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                    int[] nb = { c - 1, c + 1, c - _L.W, c + _L.W };
                    for (int k = 0; k < 4; k++)
                    {
                        int m = nb[k];
                        if (m < 0 || m >= total) continue;
                        if (k < 2 && _L.Y(m) != y) continue;        // 가로로 넘어가지 않게
                        if (_roomOf[m] >= 0 || _L.IsBlocked(m, 0)) continue;
                        _roomOf[m] = id; q.Enqueue(m);
                    }
                }
                _roomBox.Add(new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
            }
            _room = -1;
        }

        /// 지금 핵이 있는 방에 카메라를 맞춘다
        void AimCamera(bool snap)
        {
            int r = (_roomOf != null && _st != null && _roomOf[_st.Head] >= 0) ? _roomOf[_st.Head] : -1;
            // 🔴 방이 하나뿐이면 예전처럼 판 전체를 잡는다 — 지금까지의 판이 안 바뀐다
            bool whole = _roomBox.Count <= 1 || r < 0;
            float asp = Mathf.Max(0.1f, _cam.aspect);
            float cx, cy, halfW, halfH;
            if (whole) { cx = _L.W * 0.5f; cy = -_L.H * 0.5f; halfW = _L.W * 0.5f; halfH = _L.H * 0.5f; }
            else
            {
                var b = _roomBox[r];
                cx = b.x + b.width * 0.5f; cy = -(b.y + b.height * 0.5f);
                halfW = b.width * 0.5f; halfH = b.height * 0.5f;
            }
            float fit = Mathf.Max(halfH + 0.9f, (halfW + 0.9f) / asp);
            // 작은 화면에선 읽기를 택한다 (2026-08-29)
            float refFit = Mathf.Max(BoardH * 0.5f + 0.6f, (BoardW * 0.5f + 0.6f) / asp);
            float size = Mathf.Max(whole ? refFit : 0f, fit);
            if (Screen.height / (2f * size) < 30f) size = fit;

            _camWant = new Vector3(cx, cy, -10);
            _camSizeWant = size;
            _room = r;
            if (snap) { _cam.transform.position = _camWant; _cam.orthographicSize = _camSizeWant; }
        }

        void AddBridge(int a, int b, Vector3 scale, Vector2 offset)
        {
            var sr = NewSprite("HoleBridge", 0);
            sr.transform.position = CellPos(a) + offset;   // Vector2 + Vector2
            sr.transform.localScale = scale;
            sr.color = HoleCol;
            _bridges.Add((a, b, sr));
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
            _bridges.Clear();

            // 🔴 목표는 칸 여럿이 아니라 **몸이 들어갈 한 덩어리 홈**이다.
            //    칸마다 테두리를 치면 이어진 뱀 모양이 "구멍 여러 개"로 보인다.
            //    실제로 사람이 61초 동안 못 읽었다 (2026-08-29 검증).
            //    그래서 민트를 칸 가득 깔아 서로 붙게 하고, 어두운 속만 안쪽에 둔다.
            //    이웃한 칸 사이는 속끼리 다리로 이어 붙여 테두리가 안 끼게 한다.
            _doorOf.Clear();
            for (int di = 0; di < _L.Doors.Count; di++)
                foreach (int c in _L.Doors[di].Cells) _doorOf[c] = di;

            foreach (var kvDoor in _doorOf)
            {
                int c = kvDoor.Key;
                var edge = NewSprite("HoleEdge", -1);
                edge.transform.position = CellPos(c);
                edge.transform.localScale = new Vector3(1f, 1f, 1);   // 칸 가득 — 이웃과 붙는다
                edge.color = EdgeOf(kvDoor.Value);

                var inner = NewSprite("HoleInner", 0);
                inner.transform.position = CellPos(c);
                inner.transform.localScale = new Vector3(SlotInner, SlotInner, 1);
                inner.color = HoleCol;

                _holes[c] = inner;
                _holeEdges[c] = edge;

                // 🔴 굳은 몸의 윗면 — 평소엔 숨어 있다가 문이 열리면 나타난다.
                //    벽의 윗면과 같은 신호라, 여기 설 수 있다는 게 바로 읽힌다.
                var top = NewSprite("SpentTop", 1);
                top.transform.position = CellPos(c) + new Vector2(0f, 0.42f);
                top.transform.localScale = new Vector3(1f, 0.16f, 1);
                top.color = SpentTop;
                top.enabled = false;
                _spentTop[c] = top;
            }

            // 속끼리 잇는 다리 — 오른쪽·아래 이웃만 보면 중복 없이 다 이어진다
            // 🔴 다리는 **같은 문끼리만** 잇는다. 다른 문끼리 이으면 한 덩어리로 보인다.
            foreach (var kvDoor in _doorOf)
            {
                int c = kvDoor.Key, mine = kvDoor.Value;
                int x = _L.X(c), y = _L.Y(c);
                if (x + 1 < _L.W && _doorOf.TryGetValue(c + 1, out var r) && r == mine)
                    AddBridge(c, c + 1, new Vector3(1f - SlotInner, SlotInner, 1),
                              new Vector2(0.5f, 0f));
                if (y + 1 < _L.H && _doorOf.TryGetValue(c + _L.W, out var d) && d == mine)
                    AddBridge(c, c + _L.W, new Vector3(SlotInner, 1f - SlotInner, 1),
                              new Vector2(0f, -0.5f));
            }

            // 심 — 머리가 마지막에 있어야 할 자리
            if (_L.Core >= 0)
            {
                foreach (var dd in _L.Doors)
                {
                    if (dd.Core < 0) continue;
                    var ring = NewSprite("CoreRing", 1);
                    ring.transform.position = CellPos(dd.Core);
                    ring.transform.localScale = Vector3.one * 0.52f;
                    ring.color = CoreRing;

                    var core = NewSprite("Core", 2);
                    core.transform.position = CellPos(dd.Core);
                    core.transform.localScale = Vector3.one * 0.30f;
                    core.color = CoreCol;
                }
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

            // 🔴 카메라는 AimCamera가 잡는다 — 방이 여럿이면 **지금 방만** 비춘다.
            //    지도를 통째로 보여주면 "방"이 안 생긴다 (2026-08-29 확인).

            // 🔴 문벽 — 열리면 사라진다. 벽보다 밝고 테두리가 있어 "열릴 수 있는 벽"으로 보인다.
            _gateViews.Clear(); _gateOf.Clear(); _shownDm = -1;
            for (int gi = 0; gi < _L.Gates.Length; gi++)
                foreach (int c in _L.Gates[gi])
                {
                    var edge = NewSprite("Gate", -2);
                    edge.transform.position = CellPos(c);
                    edge.transform.localScale = new Vector3(0.98f, 0.98f, 1);
                    edge.color = GateEdge;
                    var inner = NewSprite("GateIn", -1);
                    inner.transform.position = CellPos(c);
                    inner.transform.localScale = new Vector3(0.78f, 0.78f, 1);
                    inner.color = GateCol;
                    _gateViews[c] = edge;
                    _gateOf[c] = gi;
                    _gateInner[c] = inner;
                }

            SplitRooms();
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
            // 🔴 문이 열리면 그 문벽이 사라지고, 그 홈은 "두고 온 몸"이 된다.
            //    화면을 매번 다시 만들지 않고 색만 바꾼다 — 몸이 움직이는 중에도 안 튄다.
            // 🔴 문벽이 툭 사라지면 허전하다 — 스르르 꺼지게 한다
            if (_shownDm != _st.Dm) { _shownDm = _st.Dm; _gateFade = 0f; }
            _gateFade = Mathf.Min(1f, _gateFade + Time.deltaTime / 0.45f);
            foreach (var kv in _gateViews)
            {
                bool open = (_st.Dm & (1 << _gateOf[kv.Key])) != 0;
                float a = open ? 1f - _gateFade : 1f;
                var ec = GateEdge; ec.a = a;
                kv.Value.color = ec;
                kv.Value.enabled = a > 0.01f;
                if (_gateInner.TryGetValue(kv.Key, out var gin))
                {
                    var ic = GateCol; ic.a = a;
                    gin.color = ic;
                    gin.enabled = a > 0.01f;
                    float s = Mathf.Lerp(0.78f, 0.2f, open ? _gateFade : 0f);
                    gin.transform.localScale = new Vector3(s, s, 1);
                }
            }

            // 🔴 안 채운 홈만 눈에 띄게 둔다 — 남은 칸이 저절로 세어진다
            var body = new HashSet<int>(_st.Body);
            foreach (var kv in _holes)
            {
                bool covered = body.Contains(kv.Key);
                int door = _doorOf.TryGetValue(kv.Key, out var dn) ? dn : 0;
                // 🔴 이미 연 문은 몸을 두고 온 자리다 — 굳은 색으로 남긴다
                bool spent = (_st.Dm & (1 << door)) != 0;
                kv.Value.color = spent ? SpentCol : covered ? FillOf(door) : HoleCol;

                // 윗면은 **덩어리의 맨 위 칸**에만 켠다. 속에까지 그리면 줄무늬가 된다.
                if (_spentTop.TryGetValue(kv.Key, out var topSr))
                {
                    bool above = _doorOf.TryGetValue(kv.Key - _L.W, out var upDoor)
                                 && (_st.Dm & (1 << upDoor)) != 0;
                    topSr.enabled = spent && !above;
                }
                if (_holeEdges.TryGetValue(kv.Key, out var e))
                {
                    var ec = EdgeOf(door);
                    // 🔴 굳으면 테두리도 같은 돌색이 된다 — 홈이 아니라 **지형**이 된 것이다
                    e.color = spent ? SpentCol
                        : covered ? new Color(ec.r, ec.g, ec.b, 0.30f) : ec;
                }
            }
            // 다리는 양쪽이 다 덮였을 때만 같이 물든다 — 안 그러면 끊겨 보인다
            foreach (var br in _bridges)
            {
                int door = _doorOf.TryGetValue(br.a, out var dn) ? dn : 0;
                bool spent = (_st.Dm & (1 << door)) != 0;
                br.sr.color = spent ? SpentCol
                    : (body.Contains(br.a) && body.Contains(br.b)) ? FillOf(door) : HoleCol;
            }
        }

        // ---------------- 입력 ----------------
        void Update()
        {
            // 🔴 이미 진 상태에서 보낸 시간을 잰다. 화면엔 아무 표시도 안 한다.
            if (_run != null && !_won && _lostSet != null && _lostSet.IsLost(_st))
                _run.lostSeconds += Time.deltaTime;

            if (_intro)
            {
                if (Input.anyKeyDown || Input.touchCount > 0) CloseIntro();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F1)) { _dev = !_dev; if (!_dev) _showPanel = false; }

            if (_dev && Input.GetKeyDown(KeyCode.F3)) { StartReplay(); return; }
            if (_replay)
            {
                if (Input.anyKeyDown && !Input.GetKeyDown(KeyCode.F3)) { _replay = false; return; }
                if (Time.time >= _replayNext) StepReplay();
                return;                                  // 재생 중엔 사람 입력을 안 받는다
            }
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
            if (_won && Time.time > _wonAt + NextLevelDelay)
            {
                string want = _wonBy == 1 ? Def.next2 : Def.next1;
                int to = IndexOf(want);
                if (to >= 0) { Load(to); return; }
                if (!_allDone) { EndRun(); _allDone = true; }   // 길이 끝났다
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                if (_allDone) { _allDone = false; _trail.Clear(); Load(0); return; }
                if (_run != null) _run.restart++;
                Restart(); return;
            }
            if (_dev && Input.GetKeyDown(KeyCode.N)) { Load(_index + 1); return; }
            if (_dev && Input.GetKeyDown(KeyCode.P)) { Load(_index - 1); return; }
            if (Input.GetKeyDown(KeyCode.Z)) { Undo(); return; }

            if (Swipe(out var sdir)) { Step(sdir); return; }

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
            int door = SnakeEngine.WonDoor(_L, _st);
            if (door >= 0)
            {
                _wonBy = door;
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

        /// 손가락을 그은 방향. 화면 짧은 쪽의 6%를 넘겨야 한 걸음으로 친다.
        bool Swipe(out SnakeEngine.Dir dir)
        {
            dir = SnakeEngine.Dir.Up;
            if (Input.touchCount == 0) { _swiping = false; return false; }
            var tch = Input.GetTouch(0);
            if (tch.phase == TouchPhase.Began) { _swipeFrom = tch.position; _swiping = true; return false; }
            if (!_swiping || (tch.phase != TouchPhase.Ended && tch.phase != TouchPhase.Moved)) return false;

            var d = (Vector2)tch.position - _swipeFrom;
            float need = Mathf.Min(Screen.width, Screen.height) * 0.06f;
            if (d.magnitude < need) return false;

            _swipeFrom = tch.position;                 // 이어서 그으면 계속 간다
            if (Mathf.Abs(d.x) > Mathf.Abs(d.y))
                dir = d.x > 0 ? SnakeEngine.Dir.Right : SnakeEngine.Dir.Left;
            else
                dir = d.y > 0 ? SnakeEngine.Dir.Up : SnakeEngine.Dir.Down;   // 화면 y는 위가 +
            return true;
        }

        // ---------------- 움직임 ----------------
        void Animate(float dt)
        {
            // 다 끝났으면 더 안 키운다 (가만히 두면 값이 무한정 커진다)
            // 🔴 방이 바뀌면 카메라가 미끄러져 따라간다. 문을 지나는 게 이걸로 읽힌다.
            if (_roomOf != null && _st != null)
            {
                int r = _roomOf[_st.Head];
                if (r != _room) AimCamera(false);
                float k = 1f - Mathf.Exp(-CamEase * dt);
                _cam.transform.position = Vector3.Lerp(_cam.transform.position, _camWant, k);
                _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _camSizeWant, k);
            }

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
                // 🔴 떨어지며 먹으면 다음 걸음에 길어진다 — 그 사이에 아무 표시가 없으면
                //    "먹었는데 사라졌다"로 보인다. 사람이 실제로 그렇게 헤맸다 (2026-08-29).
                //    꼬리를 조각 색으로 물들여 "여기서 길어진다"를 미리 보여준다.
                var col = i == 0 ? HeadCol : BodyCol;
                if (_st.Pg > 0 && i == _st.Length - 1)
                {
                    float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
                    col = Color.Lerp(col, FoodCol, 0.45f + 0.35f * beat);
                }
                _segs[i].color = col;
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

        /// 🔴 WebGL 빌드에서는 유니티 기본 글꼴이 **운영체제 글꼴에 기대기 때문에**
        ///    브라우저에 한글이 없어 전부 네모로 깨진다. 에디터에선 절대 재현 안 된다.
        ///    그래서 한글이 든 폰트를 프로젝트 안에 넣고 GUIStyle마다 직접 지정한다.
        ///    ⚠ Resources/Fonts/kr.ttf 는 지금 재배포 불가 폰트다 — 그 폴더의 README 참고.
        Font _krFont;

        /// 손가락용 방향 패드 + 되돌리기/다시. 🔴 판 하나에 100걸음을 넘기므로
        /// 버튼이 커야 한다 — 작은 버튼은 그 자체가 난이도가 된다.
        void Pad(float w, float h)
        {
            float b = Mathf.Clamp(Mathf.Min(w, h) * 0.12f, 46f, 108f);   // 버튼 한 변
            float m = b * 0.28f;                                          // 가장자리 여백
            var big = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(20 * _uiScale) };
            var small = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(13 * _uiScale) };

            // 왼쪽 아래 십자
            float px = m, py = h - m - b * 3f;
            if (GUI.Button(new Rect(px + b, py, b, b), "↑", big)) Step(SnakeEngine.Dir.Up);
            if (GUI.Button(new Rect(px, py + b, b, b), "←", big)) Step(SnakeEngine.Dir.Left);
            if (GUI.Button(new Rect(px + b * 2f, py + b, b, b), "→", big)) Step(SnakeEngine.Dir.Right);
            if (GUI.Button(new Rect(px + b, py + b * 2f, b, b), "↓", big)) Step(SnakeEngine.Dir.Down);

            // 오른쪽 아래 — 되돌리기 · 다시
            float qx = w - m - b * 1.6f;
            if (GUI.Button(new Rect(qx, py + b, b * 1.6f, b), "되돌리기", small)) Undo();
            if (GUI.Button(new Rect(qx, py + b * 2f + m * 0.4f, b * 1.6f, b * 0.8f), "다시", small))
            {
                if (_run != null) _run.restart++;
                Restart();
            }
        }

        void CloseIntro()
        {
            _intro = false;
            PlayerPrefs.SetInt(IntroKey, 1);
            PlayerPrefs.Save();
        }

        /// 설명에 쓸 색 조각. 화면에 실제로 쓰는 색을 그대로 보여준다 —
        /// 말로 "민트색 테두리"라고 하는 것보다 그 색을 보여주는 쪽이 짧다.
        Texture2D Chip(Color c)
        {
            if (_chips.TryGetValue(c, out var tex) && tex != null) return tex;
            tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, c);
            tex.Apply();
            _chips[c] = tex;
            return tex;
        }

        void Row(float x, float y, float s, Color box, Color inner, string text, GUIStyle st)
        {
            GUI.DrawTexture(new Rect(x, y, s, s), Chip(box));
            if (inner.a > 0f)
                GUI.DrawTexture(new Rect(x + s * 0.16f, y + s * 0.16f, s * 0.68f, s * 0.68f), Chip(inner));
            var t2 = new GUIStyle(st) { alignment = TextAnchor.MiddleLeft };
            GUI.Label(new Rect(x + s * 1.5f, y - 2, 620, s + 4), text, t2);
        }

        void Intro(float w, float h)
        {
            float s = Mathf.Clamp(h * 0.055f, 22f, 52f);          // 색 조각 한 변
            float gap = s * 1.5f;
            float x = w * 0.5f - Mathf.Min(w * 0.42f, 320f);
            float y = h * 0.5f - gap * 2.6f;

            GUI.Label(new Rect(0, y - gap * 2.0f, w, s * 1.4f), "슬라임 탈출", _sBig);

            Row(x, y, s, BodyCol, new Color(0, 0, 0, 0), "이게 나. 한 칸씩 움직인다", _sMid);
            y += gap;
            Row(x, y, s, Floor, FoodCol, "조각을 먹으면 몸이 길어진다", _sMid);
            y += gap;
            Row(x, y, s, HoleEdge, HoleCol, "여기를 몸으로 채운다  —  남아도 모자라도 안 된다", _sMid);
            y += gap;
            Row(x, y, s, CoreRing, CoreCol, "머리는 이 칸에서 끝나야 한다", _sMid);
            y += gap * 1.5f;

            GUI.Label(new Rect(0, y, w, s),
                Touchy ? "버튼으로 움직인다  ·  되돌리기는 몇 번이든 된다"
                       : "← ↑ ↓ →  움직이기      Z  되돌리기      R  처음부터", _sMid);
            GUI.Label(new Rect(0, h - s * 2.2f, w, s),
                Touchy ? "화면을 누르면 시작" : "아무 키나 누르면 시작", _sSmall);
        }

        void Styles()
        {
            // 🔴 휴대폰은 화면이 작다. 21px 글씨는 안 보인다 — 높이에 맞춰 키운다.
            if (_sBig != null && _styledAt == Screen.height) return;
            _styledAt = Screen.height;
            _uiScale = Mathf.Clamp(Screen.height / 720f, 0.85f, 2.2f);
            _krFont = Resources.Load<Font>("Fonts/kr");
            if (_krFont == null) Debug.LogWarning("[글꼴] Resources/Fonts/kr 를 못 찾았다 — WebGL에서 한글이 깨진다");
            _sBig = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(21 * _uiScale), alignment = TextAnchor.MiddleCenter };
            _sMid = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(16 * _uiScale), alignment = TextAnchor.MiddleCenter };
            _sSmall = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * _uiScale), alignment = TextAnchor.MiddleCenter };
            _sBig.normal.textColor = new Color(1f, 1f, 1f, 0.88f);
            _sMid.normal.textColor = new Color(1f, 1f, 1f, 0.70f);
            _sSmall.normal.textColor = new Color(1f, 1f, 1f, 0.40f);

            // 🔴 IMGUI는 GUIStyle마다 지정해야 한다. 하나라도 빠뜨리면 거기만 깨진다.
            if (_krFont != null)
            {
                _sBig.font = _sMid.font = _sSmall.font = _krFont;
                GUI.skin.font = _krFont;      // GUILayout.Label 등 기본 스타일까지
            }
        }

        void OnGUI()
        {
            Styles();
            float w = Screen.width, h = Screen.height;
            var def = Def;
            int filled = SnakeEngine.Filled(_L, _st);
            int need = _L.Target.Count;

            if (_intro)
            {
                Intro(w, h);
                if (Event.current.type == EventType.MouseDown) CloseIntro();
                return;
            }

            // ---- 다 깼으면 결과만 보여준다 ----
            //    🔴 WebGL은 파일을 못 쓴다. 이 화면이 기록을 돌려받는 유일한 통로다.
            if (_allDone)
            {
                GUI.Label(new Rect(0, 40, w, 30), "다 깼습니다. 고맙습니다!", _sBig);
                GUI.Label(new Rect(0, 74, w, 24),
                    "이 화면을 찍어서 보내주세요", _sMid);
                var box = new Rect(w * 0.5f - 250, 110, 500, 230);
                GUI.Box(box, GUIContent.none);
                GUILayout.BeginArea(new Rect(box.x + 16, box.y + 12, box.width - 32, box.height - 24));
                GUILayout.Label(SnakeLog.Table());
                GUILayout.Label(SnakeLog.Summary());
                GUILayout.Label("지나온 길: " + string.Join(" › ", _trail));
                GUILayout.EndArea();
                GUI.Label(new Rect(0, h - 56, w, 22),
                    "재미있었는지 · 어디서 막혔는지 · 그만두고 싶었는지 한 줄만 적어주시면 큰 도움이 됩니다", _sMid);
                GUI.Label(new Rect(0, h - 32, w, 20), "R  처음부터 다시", _sSmall);
                return;
            }

            // ---- 플레이어가 보는 것 : 이름 · 남은 홈 · 조작. 그게 전부다 ----
            GUI.Label(new Rect(0, 12, w, 28), def.name, _sBig);

            // 남은 홈을 점으로 — 숫자보다 한눈에 들어온다
            string dots = "";
            for (int i = 0; i < need; i++) dots += (i < filled ? "●" : "○") + " ";
            // 🔴 문이 여럿이면 남은 문도 보여준다
            if (_L.Doors.Count > 1)
            {
                int openCount = 0;
                for (int i = 0; i < _L.Doors.Count; i++) if ((_st.Dm & (1 << i)) != 0) openCount++;
                dots += "     문 " + openCount + "/" + _L.Doors.Count;
            }
            GUI.Label(new Rect(0, 42, w, 22), dots, _sMid);

            // 🔴 모은 획 — 유적을 얼마나 열었는지가 한눈에 온다
            var mk = Marks();
            if (mk.all > 0)
            {
                string ms = "";
                for (int i = 0; i < mk.all; i++) ms += (i < mk.got ? "◆" : "◇") + " ";
                GUI.Label(new Rect(0, h - 62, w, 22), ms, _sMid);
            }

            // 🔴 지도 대신 **지나온 길**을 보여준다. 어디서 갈렸는지가 남는다.
            if (_trail.Count > 1)
                GUI.Label(new Rect(0, 66, w, 20),
                    string.Join(" › ", _trail.GetRange(Mathf.Max(0, _trail.Count - 6), Mathf.Min(6, _trail.Count))),
                    _sSmall);

            if (_won)
            {
                string via = _wonBy == 1 ? "푸른 문이 열렸다" : "민트 문이 열렸다";
                GUI.Label(new Rect(0, h - 40, w, 24), via, _sMid);
            }
            else
            {
                if (!Touchy)
                    GUI.Label(new Rect(0, h - 34, w, 22),
                        "← ↑ ↓ →      Z  되돌리기      R  처음부터", _sSmall);

                // 🔴 다시 볼 길을 남긴다. 한 번 보고 잊으면 물어볼 데가 없다.
                var qs = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(15 * _uiScale) };
                float qb = Mathf.Clamp(28f * _uiScale, 28f, 56f);
                if (GUI.Button(new Rect(w - qb - 10, 10, qb, qb), "?", qs)) _intro = true;

                // 🔴 안내는 필요한 순간에만 뜬다. 늘 떠 있으면 아무도 안 읽는다.
                if (_index == 0 && !_cleared.Contains(def.id))
                    GUI.Label(new Rect(0, h - 62, w, 22),
                        "표시된 칸을 몸으로 정확히 채우면 문이 열린다", _sMid);
                else if (_L.Core >= 0 && filled == need && _st.Length == need && _st.Head != _L.Core)
                    GUI.Label(new Rect(0, h - 62, w, 22),
                        "머리가 노란 칸에서 끝나야 한다", _sMid);
            }

            if (_replay)
                GUI.Label(new Rect(0, 70, w, 22),
                    $"정답 재생 중  {_replayAt}/{def.sol.Length}   (아무 키나 누르면 멈춤)", _sMid);

            if (Touchy && !_replay) Pad(w, h);

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
                            $"      N/P 판   K 손맛   F3 정답재생   G 중력 {(_gravity ? "켬" : "끔")}");
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
