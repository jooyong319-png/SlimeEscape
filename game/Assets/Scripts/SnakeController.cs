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
        public const int BoardW = 20, BoardH = 12;

        static readonly Color BgCol   = new Color32(0x0f, 0x16, 0x14, 0xff);
        static readonly Color Floor   = new Color32(0x18, 0x22, 0x1e, 0xff);
        static readonly Color Rock    = new Color32(0x25, 0x32, 0x2c, 0xff);
        static readonly Color Grid    = new Color32(0x2e, 0x3d, 0x36, 0xff);
        static readonly Color HeadCol = new Color32(0xb8, 0xeb, 0xd3, 0xff);
        static readonly Color BodyCol = new Color32(0x8d, 0xce, 0xb0, 0xff);
        static readonly Color FoodCol = new Color32(0xf3, 0x8a, 0x04, 0xff);
        // 목표 홈은 바닥보다 어둡게 파인 것처럼 + 민트 테두리. 심은 놋쇠빛.
        static readonly Color HoleCol = new Color32(0x14, 0x20, 0x1b, 0xff);
        static readonly Color HoleEdge = new Color32(0x3d, 0x5c, 0x4d, 0xff);
        static readonly Color CoreCol = new Color32(0xe0, 0xb3, 0x56, 0xff);

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
        bool _won;
        float _wonAt;

        /// 🔬 중력 실험. G로 켜고 끈다.
        /// 이 판은 솔버로 양쪽 다 검증했다 — 중력 켬 36걸음 / 끔 35걸음, 둘 다 최단해 유일.
        bool _gravity;

        // 마디마다 화면 위치를 따로 굴린다 — 뒷마디가 조금 늦게 출발한다
        readonly List<Vector2> _segPos = new List<Vector2>();   // 지금 그려지는 자리
        readonly List<Vector2> _from = new List<Vector2>();     // 이번 걸음을 시작한 자리
        float _pop, _stepT;
        bool _showPanel;

        /// 마디 하나당 출발 지연 (걸음 시간 기준). follow가 1이면 0 = 동시에 움직인다.
        float Lag => (1f - Mathf.Clamp01(K.follow)) * 0.28f;

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

            _L = SnakeEngine.Parse(TestMap(), "test", _gravity);
            BuildBoard();
            Restart();
        }

        /// 테스트 맵 하나. 🔴 판 설계가 아니다 — 움직임만 보려고 둔 것이다.
        /// 넓은 빈 방 + 기둥 몇 개 + 먹이 여럿.
        /// 🔴 솔버로 검증한 판 (tools/gmap.js "H3").
        ///    중력 켬 36걸음 · 끔 35걸음 — 둘 다 최단해가 유일하다.
        ///    조각 5개 + 시작 길이 1 = 6 = 목표 칸 수.
        static string[] TestMap() => new[]
        {
            "####################",
            "#..................#",
            "#..................#",
            "#..................#",
            "#............==*...#",
            "#............===...#",
            "#...........#####..#",
            "#......+...........#",
            "#....#####.........#",
            "#..+...............#",
            "#.S..+...+...+.....#",
            "####################",
        };

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
                    var sr = NewSprite(_L.IsWall(c) ? "Wall" : "Floor", -2);
                    sr.transform.position = CellPos(c);
                    sr.color = _L.IsWall(c) ? Rock : Floor;
                }

            // 🔴 목표 홈 — 바닥보다 어둡게 파인 것처럼. 몸이 덮으면 한 칸씩 빛이 찬다
            _holes.Clear();
            foreach (int c in _L.Target)
            {
                var sr = NewSprite("Hole", -1);
                sr.transform.position = CellPos(c);
                sr.transform.localScale = new Vector3(0.94f, 0.94f, 1);
                sr.color = HoleCol;
                var edge = NewSprite("HoleEdge", -1);
                edge.transform.position = CellPos(c);
                edge.transform.localScale = new Vector3(0.94f, 0.06f, 1);
                edge.transform.position += new Vector3(0, -0.44f, 0);
                edge.color = HoleEdge;
                _holes[c] = sr;
            }

            // 심 — 머리가 마지막에 있어야 할 자리
            if (_L.Core >= 0)
            {
                var core = NewSprite("Core", 1);
                core.transform.position = CellPos(_L.Core);
                core.transform.localScale = Vector3.one * 0.26f;
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

            _cam.transform.position = new Vector3(BoardW * 0.5f, -BoardH * 0.5f, -10);
            _cam.orthographicSize = Mathf.Max(BoardH * 0.5f + 0.6f,
                                              (BoardW * 0.5f + 0.6f) / Mathf.Max(0.1f, _cam.aspect));
        }

        void GridLine(int i, bool vertical)
        {
            var sr = NewSprite("Grid", 0);
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
            _segPos.Clear(); _from.Clear();
            _segPos.Add(CellPos(_st.Head)); _from.Add(_segPos[0]);
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
            var body = new HashSet<int>(_st.Body);
            foreach (var kv in _holes)
                kv.Value.color = body.Contains(kv.Key)
                    ? new Color32(0x24, 0x3c, 0x31, 0xff)
                    : HoleCol;
        }

        // ---------------- 입력 ----------------
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.K)) _showPanel = !_showPanel;
            if (Input.GetKeyDown(KeyCode.G))
            {
                _gravity = !_gravity;
                _L = SnakeEngine.Parse(TestMap(), "test", _gravity);
                BuildBoard();
                Restart();
                return;
            }
            if (Input.GetKeyDown(KeyCode.R)) { Restart(); return; }
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
            if (!SnakeEngine.Step(_L, _st, dir, out var ns)) return;   // 막히면 아무 일도 안 일어난다
            _undo.Push(_st);
            _st = ns;
            if (SnakeEngine.IsWin(_L, _st)) { _won = true; _wonAt = Time.time; }

            if (_st.Length > before)
            {
                _pop = K.growPop;
                _segPos.Add(_segPos[_segPos.Count - 1]);               // 새 꼬리는 옛 꼬리 자리에서 시작
            }

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
            _st = _undo.Pop();
            _won = false;
            while (_segPos.Count > _st.Length) _segPos.RemoveAt(_segPos.Count - 1);
            while (_segPos.Count < _st.Length) _segPos.Add(_segPos[_segPos.Count - 1]);
            for (int i = 0; i < _st.Length; i++) _segPos[i] = CellPos(_st.Body[i]);   // 되돌리기는 딱 붙인다
            _from.Clear(); _from.AddRange(_segPos);
            _stepT = 999f;
            _pop = 0f;
            SyncViews();
        }

        // ---------------- 움직임 ----------------
        void Animate(float dt)
        {
            // 다 끝났으면 더 안 키운다 (가만히 두면 값이 무한정 커진다)
            float done = 1f + Lag * Mathf.Max(0, _st.Length - 1);
            if (_stepT < done + 1f) _stepT += dt / Mathf.Max(0.02f, K.stepTime);
            _pop = Mathf.Max(0f, _pop - dt / 0.18f);

            while (_segPos.Count < _st.Length) _segPos.Add(_segPos[_segPos.Count - 1]);
            while (_segPos.Count > _st.Length) _segPos.RemoveAt(_segPos.Count - 1);
            while (_from.Count < _segPos.Count) _from.Add(_segPos[_from.Count]);
            while (_from.Count > _segPos.Count) _from.RemoveAt(_from.Count - 1);

            // 🔴 마디 i는 '출발선 -> 자기 칸'으로만 간다. 앞 마디 쪽으로 끌어당기지 않는다.
            //    끌리는 느낌은 **출발이 늦는 것**으로 낸다 — 그래야 끝났을 때 정확히 칸에 앉는다.
            float lag = Lag;
            for (int i = 0; i < _st.Length; i++)
            {
                float t = Mathf.Clamp01(_stepT - i * lag);
                _segPos[i] = Vector2.Lerp(_from[i], CellPos(_st.Body[i]), Ease(t));
            }

            for (int i = 0; i < _st.Length; i++)
            {
                float s = K.segmentSize;
                if (i == _st.Length - 1) s *= 1f + _pop;               // 새로 붙은 꼬리가 한 번 부푼다
                _segs[i].transform.position = _segPos[i];
                _segs[i].transform.localScale = new Vector3(s, s, 1);
                _segs[i].color = i == 0 ? HeadCol : BodyCol;
            }
        }

        static float Ease(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

        // ---------------- 임시 UI ----------------
        void LoadKnobs()
        {
            var json = PlayerPrefs.GetString(KnobKey, "");
            if (!string.IsNullOrEmpty(json)) { try { K = JsonUtility.FromJson<Knobs>(json) ?? new Knobs(); } catch { K = new Knobs(); } }
        }

        void OnGUI()
        {
            GUI.Box(new Rect(12, 12, 480, 76), GUIContent.none);
            GUILayout.BeginArea(new Rect(22, 20, 460, 60));
            int filled = SnakeEngine.Filled(_L, _st);
            int need = _L.Target.Count;
            bool onCore = _L.Core < 0 || _st.Head == _L.Core;
            GUILayout.Label($"rev.4 — 홈 {filled}/{need} 채움   ·   길이 {_st.Length}/{need}" +
                            (_L.Core >= 0 ? (onCore ? "   ·   머리가 심에 있다" : "   ·   심에 머리를 두어야 한다") : ""));
            GUILayout.Label(_won
                ? "열렸다 —  R 다시"
                : $"← ↑ ↓ →  이동   Z 되돌리기   R 다시   K 손맛   G 중력 {(_gravity ? "켬" : "끔")}");
            if (_gravity)
                GUILayout.Label("중력 켬 — 몸이 통째로 떨어진다. 위로 k칸 오르려면 길이가 k+1 이상이어야 한다");
            GUILayout.EndArea();

            if (!_showPanel) return;
            var r = new Rect(Screen.width - 322, 12, 310, 168);
            GUI.Box(r, GUIContent.none);
            GUILayout.BeginArea(new Rect(r.x + 12, r.y + 10, r.width - 24, r.height - 20));
            GUILayout.Label("손맛 조절 — K로 닫기");
            K.stepTime = Row("한 칸 시간", K.stepTime, 0.03f, 0.30f);
            K.follow = Row("따라붙는 정도", K.follow, 0f, 1f);
            K.segmentSize = Row("마디 크기", K.segmentSize, 0.5f, 1f);
            K.growPop = Row("길어질 때 부풂", K.growPop, 0f, 0.6f);
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
