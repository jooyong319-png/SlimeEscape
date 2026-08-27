using System.Collections.Generic;
using UnityEngine;

namespace SlimeEscape
{
    /// <summary>
    /// 2단계(수직 슬라이스). 화면 요소는 전부 런타임에 만든다 — 씬에 배치한 게 없어서
    /// 판을 고쳐도 씬을 안 건드린다.
    ///
    /// 🔴 움직임은 한 걸음을 <b>마디로 쪼개서</b> 굴린다 (옆으로 → 떨어짐 → 몸 변화).
    ///    엔진이 마디를 알려주고(SlimeEngine.TraceStep), 여기서 마디마다 다르게 움직인다.
    ///    한꺼번에 보간하면 계단을 내려가는 게 아니라 대각선으로 흘러내린다.
    ///
    /// ⚠️ HUD와 조절 패널은 임시로 OnGUI다. 4단계(마감)에서 교체한다.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        // 되살린 팔레트 (art/PALETTE.md)
        static readonly Color Rock   = new Color32(0x25, 0x32, 0x2c, 0xff);
        static readonly Color RockHi = new Color32(0x33, 0x43, 0x3b, 0xff);
        static readonly Color Floor  = new Color32(0x18, 0x22, 0x1e, 0xff);
        static readonly Color FoodCol = new Color32(0xb8, 0xeb, 0xd3, 0xff);
        static readonly Color ExitCol = new Color32(0x8d, 0xce, 0xb0, 0xff);
        static readonly Color BgCol   = new Color32(0x0f, 0x16, 0x14, 0xff);

        const float FrameSeconds = 0.1f;      // 원본 GIF가 10 FPS였다
        const string ProgressKey = "cleared";
        const float NextLevelDelay = 1.1f;

        /// 🔴 모든 판은 이 크기다. 카메라를 여기에 맞춰 고정하므로
        ///    **한 칸이 화면에서 항상 같은 크기**가 된다.
        ///    판마다 크기가 다르면 카메라가 판에 맞추느라 칸 크기가 달라진다.
        ///    (판 데이터는 tools/normalize.js 가 이 크기로 맞춘다)
        public const int BoardW = 20, BoardH = 12;

        /// 🔴 원본 도트는 **왼쪽을 보고 있다** — 눈이 왼쪽에 있고 꼬리가 오른쪽으로 끌린다.
        ///    그래서 오른쪽으로 갈 때 뒤집는다. (art/README.md 참고)
        const bool SpriteFacesLeft = true;

        /// 🔴 그레이박스. 도트를 빼고 네모로만 그린다.
        ///    "한 칸"을 정하는 동안은 그림이 판단을 흐린다 — 스프라이트 비율(63×52)에
        ///    축소까지 겹치면 몸이 몇 칸인지 눈으로 셀 수가 없다. G로 켜고 끈다.
        bool _useArt = false;

        static readonly Color GridLine = new Color32(0x2e, 0x3d, 0x36, 0xff);
        static readonly Color SlimeBox = new Color32(0x8d, 0xce, 0xb0, 0xff);
        static readonly Color FireBox  = new Color32(0xd4, 0x51, 0x3d, 0xff);

        Tuning _tune;

        LevelJson[] _defs;
        int _index;
        SlimeEngine.Level _L;
        SlimeEngine.State _st;
        readonly Stack<(SlimeEngine.State st, int moves)> _undo = new Stack<(SlimeEngine.State, int)>();
        readonly HashSet<string> _cleared = new HashSet<string>();
        int _moves;
        bool _won;
        float _wonAt;

        PixelSprites.Sheet _idle, _run, _fireSheet;
        Transform _boardRoot;
        SpriteRenderer _slime, _footprint;
        readonly List<SpriteRenderer> _foods = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _fires = new List<SpriteRenderer>();
        Camera _cam;

        int _frame;
        float _frameT, _runUntil;
        int _facing = 1;

        // ---- 움직임 ----
        // 🔴 위치는 '발밑'으로 다룬다 (x = 가운데, y = 바닥). 몸 크기가 변해도 발이 안 뜬다.
        struct Leg { public Vector2 foot; public float size; public SlimeEngine.Leg kind; }
        readonly Queue<Leg> _legs = new Queue<Leg>();
        readonly List<SlimeEngine.TraceStep> _trace = new List<SlimeEngine.TraceStep>();
        Vector2 _visPos;          // 발밑
        float _visSize;
        Vector2 _legFrom; float _legT;
        bool _legActive;
        float _fallSpeed, _squash, _pop, _bump, _bumpDir;
        bool _showPanel;

        // ---- 연습장 ----
        // 판에서는 먹이가 떨어지면 못 움직여서 손맛을 볼 수가 없다.
        // T로 켜면 크기가 안 줄어서 걷고·오르고·떨어지는 걸 실컷 해볼 수 있다.
        bool _practice;
        int _practiceSize = 2;      // 사장님 기준 크기 (2026-08-28)
        SlimeEngine.Level _practiceLevel;

        void Awake()
        {
            _tune = Tuning.Load();

            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _cam = go.AddComponent<Camera>();
            }
            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = BgCol;

            _idle = PixelSprites.Load("Art/slime_idle_sheet", 4);
            _run = PixelSprites.Load("Art/slime_move_sheet", 3);
            _fireSheet = PixelSprites.Load("Art/fire_idle_sheet", 4);

            _defs = LevelSet.LoadAll();
            LoadProgress();

            int start = 0;
            while (start < _defs.Length && _cleared.Contains(_defs[start].id)) start++;
            Load(start >= _defs.Length ? 0 : start);
        }

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

        void Load(int i)
        {
            _practice = false;
            _index = Mathf.Clamp(i, 0, _defs.Length - 1);
            _L = LevelSet.ToLevel(_defs[_index]);
            BuildBoard();
            Restart();
        }

        // ---------------- 연습장 ----------------
        /// 걷기 · 2칸 오르기 · 3칸 낙하 · 1칸 턱을 한 줄에 넣은 지형.
        /// 먹이가 없고 크기가 안 줄어서 손맛만 본다.
        static SlimeEngine.Level MakePracticeLevel()
        {
            // 연습장도 판과 같은 크기다 — 칸 크기가 달라지면 연습장에서 본 감이 안 맞는다
            const int W = BoardW, H = BoardH;
            int Surface(int x) =>
                x <= 4 ? 10 :          // 평지 (시작)
                x <= 8 ? 8 :           // 2칸 오르기
                x <= 11 ? 6 :          // 2칸 더 오르기
                x <= 14 ? 9 :          // 3칸 떨어지기
                x <= 16 ? 8 :          // 1칸 턱
                10;                    // 2칸 떨어지기

            var rows = new string[H];
            for (int y = 0; y < H; y++)
            {
                var line = new char[W];
                for (int x = 0; x < W; x++)
                {
                    bool border = x == 0 || x == W - 1 || y == 0 || y == H - 1;
                    line[x] = border || y > Surface(x) ? '#' : '.';
                }
                rows[y] = new string(line);
            }
            // S는 왼쪽 평지, E는 아무도 안 닿는 천장 구석 (엔진이 하나는 요구한다)
            rows[10] = rows[10].Remove(1, 1).Insert(1, "S");
            rows[1] = rows[1].Remove(W - 2, 1).Insert(W - 2, "E");
            return SlimeEngine.Parse(rows, "practice", "연습장", 3, 3);
        }

        void TogglePractice()
        {
            _practice = !_practice;
            if (_practice)
            {
                _practiceLevel ??= MakePracticeLevel();
                _L = _practiceLevel;
                BuildBoard();
                Restart();
                RestoreSize();
            }
            else Load(_index);
        }

        /// 연습장에서는 크기를 되돌려 준다 — 안 줄어야 실컷 움직여 볼 수 있다
        void RestoreSize()
        {
            if (!_practice) return;
            for (int d = 0; d <= _practiceSize; d++)
                for (int sgn = 1; sgn >= -1; sgn -= 2)
                {
                    int nx = _st.X + d * sgn;
                    if (!SlimeEngine.Fits(_L, nx, _st.Y, _practiceSize)) { if (d == 0) break; continue; }
                    var want = new SlimeEngine.State { X = nx, Y = _st.Y, N = _practiceSize, Fm = 0, Gm = 0 };
                    if (SlimeEngine.Settle(_L, want, 0, out var settled)) { _st = settled; return; }
                    if (d == 0) break;
                }
        }

        void Restart()
        {
            if (!SlimeEngine.StartState(_L, out _st))
            {
                Debug.LogError($"[{_L.Id}] 시작하자마자 막힌다 — 판 데이터를 확인할 것");
                return;
            }
            _undo.Clear(); _moves = 0; _won = false; _facing = 1;
            _legs.Clear(); _legActive = false; _legT = 0;
            _fallSpeed = _squash = _pop = _bump = 0;
            RestoreSize();                 // 연습장이면 정한 크기로
            _visPos = Foot(_st); _visSize = _st.N;
            SyncMarkers();
            ApplyVisual();
        }

        // 데이터는 y가 아래로, 유니티는 위로. 덩어리의 '발밑'을 낸다 (x 가운데, y 바닥).
        static Vector2 Foot(SlimeEngine.State s) =>
            new Vector2(s.X + s.N * 0.5f, -(s.Y + 1));
        Vector3 CellCenter(int x, int y) => new Vector3(x + 0.5f, -(y + 0.5f), 0);

        // ---------------- 판 만들기 ----------------
        void BuildBoard()
        {
            if (_boardRoot != null) Destroy(_boardRoot.gameObject);
            _boardRoot = new GameObject("Board").transform;
            _foods.Clear(); _fires.Clear();

            for (int y = 0; y < _L.H; y++)
                for (int x = 0; x < _L.W; x++)
                {
                    bool wall = _L.IsWall(x, y);
                    var sr = NewSprite(wall ? "Wall" : "Floor", PixelSprites.Solid(), wall ? -1 : -2);
                    sr.transform.SetParent(_boardRoot, false);
                    sr.transform.position = CellCenter(x, y);
                    sr.color = wall ? Rock : Floor;
                    if (wall && !_L.IsWall(x, y - 1))
                    {
                        var top = NewSprite("WallTop", PixelSprites.Solid(), 0);
                        top.transform.SetParent(_boardRoot, false);
                        top.transform.position = CellCenter(x, y) + new Vector3(0, 0.45f, 0);
                        top.transform.localScale = new Vector3(1, 0.1f, 1);
                        top.color = RockHi;
                    }
                }

            int ex = _L.ExitCell % _L.W, ey = _L.ExitCell / _L.W;
            var exit = NewSprite("Exit", PixelSprites.Solid(), -1);
            exit.transform.SetParent(_boardRoot, false);
            exit.transform.position = CellCenter(ex, ey);
            exit.transform.localScale = new Vector3(0.86f, 0.86f, 1);
            exit.color = new Color(ExitCol.r, ExitCol.g, ExitCol.b, 0.30f);

            foreach (int c in _L.Foods)
            {
                var sr = NewSprite("Food", PixelSprites.Disc(), 1);
                sr.transform.SetParent(_boardRoot, false);
                sr.transform.position = CellCenter(c % _L.W, c / _L.W);
                sr.transform.localScale = Vector3.one * 0.44f;
                sr.color = FoodCol;
                _foods.Add(sr);
            }

            foreach (int c in _L.Fires)
            {
                int fx = c % _L.W, fy = c / _L.W;
                SpriteRenderer sr;
                if (_useArt)
                {
                    sr = NewSprite("Fire", _fireSheet.Frames[0], 2);
                    float h = _fireSheet.UnitH / _fireSheet.UnitW;
                    sr.transform.position = CellCenter(fx, fy) + new Vector3(0, (h - 1f) * 0.5f, 0);
                    sr.transform.localScale = Vector3.one / _fireSheet.UnitW;
                }
                else
                {
                    sr = NewSprite("Fire", PixelSprites.Solid(), 2);
                    sr.transform.position = CellCenter(fx, fy);
                    sr.transform.localScale = new Vector3(0.8f, 0.8f, 1);
                    sr.color = FireBox;
                }
                sr.transform.SetParent(_boardRoot, true);
                _fires.Add(sr);
            }

            // 🔴 격자선 — 몇 칸인지 셀 수 있어야 "한 칸"을 정할 수 있다
            for (int x = 1; x < _L.W; x++) AddGridLine(x, 0, true);
            for (int y = 1; y < _L.H; y++) AddGridLine(0, y, false);

            // 몸이 차지하는 N×N 칸. 그림을 작게 그리니 이게 없으면 왜 못 지나가는지 안 보인다.
            _footprint = NewSprite("Footprint", PixelSprites.Solid(), 2);
            _footprint.transform.SetParent(_boardRoot, false);

            _slime = NewSprite("Slime", _useArt ? _idle.Frames[0] : PixelSprites.Solid(), 3);
            _slime.transform.SetParent(_boardRoot, false);

            // 🔴 카메라는 '이 판'이 아니라 '고정 판 크기'에 맞춘다.
            //    그래야 판이 바뀌어도 한 칸이 같은 크기로 보인다.
            if (_L.W != BoardW || _L.H != BoardH)
                Debug.LogWarning($"[{_L.Id}] 판이 {_L.W}x{_L.H} 다 — 고정 크기는 {BoardW}x{BoardH}. " +
                                 "node tools/normalize.js --write 로 맞출 것");

            const float margin = 0.6f;
            _cam.transform.position = new Vector3(BoardW * 0.5f, -BoardH * 0.5f, -10);
            _cam.orthographicSize = Mathf.Max(BoardH * 0.5f + margin,
                                              (BoardW * 0.5f + margin) / Mathf.Max(0.1f, _cam.aspect));
        }

        /// 판 전체를 가로지르는 얇은 선 하나. vertical이면 세로선.
        void AddGridLine(int x, int y, bool vertical)
        {
            // 벽(-1)보다 위에 그린다 — 벽 위에서도 칸이 세어져야 한다
            var sr = NewSprite("Grid", PixelSprites.Solid(), 0);
            sr.transform.SetParent(_boardRoot, false);
            float t = 0.035f;
            if (vertical)
            {
                sr.transform.position = new Vector3(x, -_L.H * 0.5f, 0);
                sr.transform.localScale = new Vector3(t, _L.H, 1);
            }
            else
            {
                sr.transform.position = new Vector3(_L.W * 0.5f, -y, 0);
                sr.transform.localScale = new Vector3(_L.W, t, 1);
            }
            sr.color = GridLine;
        }

        static SpriteRenderer NewSprite(string name, Sprite s, int order)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.sortingOrder = order;
            return sr;
        }

        void SyncMarkers()
        {
            for (int i = 0; i < _foods.Count; i++) _foods[i].enabled = !SlimeEngine.IsEaten(_st, i);
            for (int i = 0; i < _fires.Count; i++) _fires[i].enabled = !SlimeEngine.IsOut(_st, i);
        }

        // ---------------- 입력 ----------------
        void Update()
        {
            _frameT += Time.deltaTime;
            if (_frameT >= FrameSeconds) { _frameT -= FrameSeconds; _frame++; }
            if (_useArt)
            {
                var sheet = Time.time < _runUntil ? _run : _idle;
                _slime.sprite = sheet.Frames[_frame % sheet.Frames.Length];
                _slime.color = Color.white;
            }
            else
            {
                _slime.sprite = PixelSprites.Solid();
                _slime.color = SlimeBox;
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                _useArt = !_useArt;
                BuildBoard();
                SyncMarkers();     // 판을 다시 그렸으니 먹은 것·끈 것을 다시 맞춘다
            }

            if (Input.GetKeyDown(KeyCode.K)) _showPanel = !_showPanel;
            if (Input.GetKeyDown(KeyCode.T)) TogglePractice();
            if (_practice)
            {
                if (Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.Equals))
                { _practiceSize = Mathf.Min(6, _practiceSize + 1); RestoreSize(); }
                if (Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.Minus))
                { _practiceSize = Mathf.Max(1, _practiceSize - 1); RestoreSize(); }
            }

            if (!_practice && _won && _index < _defs.Length - 1 && _legs.Count == 0 && Time.time > _wonAt + NextLevelDelay)
            {
                Load(_index + 1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Step(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Step(1);
            else if (Input.GetKeyDown(KeyCode.Z)) Undo();
            else if (Input.GetKeyDown(KeyCode.R)) Restart();
            else if (Input.GetKeyDown(KeyCode.N)) Load(_index + 1);
            else if (Input.GetKeyDown(KeyCode.P)) Load(_index - 1);

            Animate(Time.deltaTime);
            ApplyVisual();
        }

        void Step(int dx)
        {
            if (_won) return;
            _facing = dx;

            if (!SlimeEngine.Move(_L, _st, dx, out var ns, _trace))
            {
                _bump = 1f; _bumpDir = dx;      // 막혔다 — 벽 쪽으로 살짝 밀렸다 돌아온다
                return;
            }

            _undo.Push((_st, _moves));
            _st = ns; _moves++;
            RestoreSize();                 // 연습장에서만 — 크기를 되돌려 계속 움직일 수 있게
            _runUntil = Time.time + 0.26f;

            // 마디를 순서대로 큐에 넣는다. 밀리면 뚝 끊지 않고 Animate가 전체를 빠르게 돌린다.
            foreach (var t in _trace)
                _legs.Enqueue(new Leg { foot = Foot(t.St), size = t.St.N, kind = t.Leg });
            while (_legs.Count > 16) FastForward();   // 손이 아주 빠를 때만 걸리는 안전장치

            SyncMarkers();
            if (!_practice && SlimeEngine.IsWin(_L, _st))
            {
                _won = true; _wonAt = Time.time;
                _cleared.Add(_defs[_index].id);
                SaveProgress();
            }
        }

        void FastForward()
        {
            var leg = _legs.Dequeue();
            _visPos = leg.foot; _visSize = leg.size;
            _legT = 0; _legActive = false; _fallSpeed = 0;
        }

        void Undo()
        {
            if (_undo.Count == 0) return;
            var (st, m) = _undo.Pop();
            _st = st; _moves = m; _won = false;
            _legs.Clear(); _legT = 0; _legActive = false; _fallSpeed = 0;
            _visPos = Foot(_st); _visSize = _st.N;
            SyncMarkers();
        }

        // ---------------- 움직임 ----------------
        void Animate(float dt)
        {
            // 🔴 밀리면 뚝 끊지 않고 전체를 빠르게 돌린다. 순간이동보다 낫다.
            float budget = dt * (1f + 0.7f * Mathf.Max(0, _legs.Count - 2));

            // 🔴 한 프레임에 마디를 여러 개 소화하고 남은 시간을 이어서 쓴다.
            //    마디마다 프레임을 한 장씩 쓰면 한 걸음이 뚝뚝 끊긴다.
            int guard = 0;
            while (budget > 1e-5f && _legs.Count > 0 && guard++ < 12)
                budget = AdvanceLeg(_legs.Peek(), budget);

            // 🔴 몸 크기는 마디와 따로 목표를 계속 쫓는다 — 먹은 순간 바로 불어나 보인다.
            //    단 그레이박스에서는 **항상 정확히 N칸**이다. 한 칸을 정하는 중에
            //    몸이 2.4칸 같은 어중간한 크기면 격자가 깨져 보인다.
            float target = _legs.Count > 0 ? _legs.Peek().size : _st.N;
            _visSize = _useArt
                ? Mathf.Lerp(_visSize, target, 1f - Mathf.Exp(-_tune.sizeChase * dt))
                : target;

            // 지수로 사그라들게 (MoveTowards는 끝에서 툭 끊긴다)
            _squash *= Mathf.Exp(-_tune.squashRecover * dt);
            _pop *= Mathf.Exp(-dt / Mathf.Max(0.02f, _tune.resizeTime));
            _bump = Mathf.MoveTowards(_bump, 0, dt / Mathf.Max(0.02f, _tune.bumpTime));
        }

        /// 이 마디를 budget초만큼 진행하고 남은 시간을 돌려준다.
        float AdvanceLeg(Leg leg, float budget)
        {
            if (!_legActive)
            {
                _legFrom = _visPos; _legT = 0f; _legActive = true;
                if (leg.kind == SlimeEngine.Leg.Fall) _fallSpeed = 0f;
                else if (leg.kind == SlimeEngine.Leg.Step) _squash = Mathf.Max(_squash, _tune.stepSquash);
                else if (leg.size > _visSize) _pop = Mathf.Max(_pop, _tune.growPop);
            }

            if (leg.kind == SlimeEngine.Leg.Fall)
            {
                float g = Mathf.Max(1f, _tune.gravity);
                float remain = _visPos.y - leg.foot.y;
                if (remain <= 1e-5f) { Land(_fallSpeed); FinishLeg(); return budget; }

                float v0 = Mathf.Min(_fallSpeed, _tune.maxFall);
                float dy = v0 * budget + 0.5f * g * budget * budget;
                if (dy < remain)
                {
                    _fallSpeed = Mathf.Min(v0 + g * budget, _tune.maxFall);
                    _visPos = new Vector2(ChaseX(leg.foot.x, budget), _visPos.y - dy);
                    return 0f;
                }
                // 바닥에 닿는 순간까지만 쓰고 나머지는 다음 마디에 넘긴다
                float t = (Mathf.Sqrt(v0 * v0 + 2f * g * remain) - v0) / g;
                _fallSpeed = Mathf.Min(v0 + g * t, _tune.maxFall);
                _visPos = new Vector2(ChaseX(leg.foot.x, t), leg.foot.y);
                Land(_fallSpeed);
                FinishLeg();
                return budget - t;
            }

            // Step / Resize — 시간 기반
            float dur = Mathf.Max(0.02f, leg.kind == SlimeEngine.Leg.Step ? _tune.stepTime : _tune.resizeTime);
            float need = (1f - _legT) * dur;
            if (budget < need)
            {
                _legT += budget / dur;
                _visPos = Vector2.Lerp(_legFrom, leg.foot, Ease(_legT, leg.kind));
                return 0f;
            }
            _visPos = leg.foot;
            FinishLeg();
            return budget - need;
        }

        float Ease(float t, SlimeEngine.Leg kind)
        {
            t = Mathf.Clamp01(t);
            return kind == SlimeEngine.Leg.Step
                ? 1f - Mathf.Pow(1f - t, _tune.stepEase)   // 확 나갔다 붙는다
                : t * t * (3f - 2f * t);
        }

        float ChaseX(float targetX, float dt) =>
            Mathf.Lerp(_visPos.x, targetX, 1f - Mathf.Exp(-25f * dt));

        void Land(float speed)
        {
            _squash = Mathf.Max(_squash, _tune.landSquash * Mathf.Clamp01(speed / Mathf.Max(1f, _tune.maxFall)));
            _fallSpeed = 0f;
        }

        void FinishLeg()
        {
            _legs.Dequeue(); _legT = 0f; _legActive = false;
        }

        void ApplyVisual()
        {
            // 눌림: 넓어지고 낮아진다 / 부풂: 양쪽 다 커진다 / 막힘: 벽 쪽으로 살짝
            // 🔴 그레이박스에서는 몸 = 칸 그대로다 (inset 없음). 한 칸을 정하는 중이라 속이면 안 된다.
            float inset = _useArt ? Mathf.Clamp(_tune.spriteInset, 0.3f, 1f) : 1f;
            float uW = _useArt ? _idle.UnitW : 1f;
            float uH = _useArt ? _idle.UnitH : 1f;

            // 🔴 눌림·늘어남은 '그림'일 때만 건다.
            //    그레이박스에서 이걸 걸면 네모가 2.07칸 × 1.9칸이 되어 격자가 깨진다.
            float s = _useArt ? _squash : 0f;
            float pop = _useArt ? 1f + _pop : 1f;
            float w = _visSize * inset * (1f + s * 0.7f) * pop;
            float h = _visSize * inset * (1f - s) * pop;

            float bumpOffset = _bumpDir * _tune.bumpDistance * Mathf.Sin(_bump * Mathf.PI);
            // _visPos는 발밑이다 — 크기가 변해도 발이 땅에서 안 뜬다
            var pos = new Vector3(_visPos.x + bumpOffset, _visPos.y + h * 0.5f, 0);

            bool flip = _useArt && (SpriteFacesLeft ? _facing > 0 : _facing < 0);
            _slime.transform.position = pos;
            _slime.transform.localScale = new Vector3(
                (w / uW) * (flip ? -1 : 1),
                h / uH, 1);

            // 차지하는 칸 — 그림이 작을 때만 필요하다 (그레이박스는 몸이 곧 칸이다)
            if (_footprint != null)
            {
                float a = _useArt ? Mathf.Clamp01(_tune.footprintAlpha) : 0f;
                _footprint.enabled = a > 0.001f;
                if (_footprint.enabled)
                {
                    _footprint.transform.position =
                        new Vector3(_visPos.x + bumpOffset, _visPos.y + _visSize * 0.5f, 0);
                    _footprint.transform.localScale = new Vector3(_visSize, _visSize, 1);
                    _footprint.color = new Color(ExitCol.r, ExitCol.g, ExitCol.b, a);
                }
            }
        }

        // ---------------- 임시 UI ----------------
        void OnGUI()
        {
            GUI.Box(new Rect(12, 12, 520, 96), GUIContent.none);
            GUILayout.BeginArea(new Rect(22, 20, 500, 80));
            if (_practice)
            {
                GUILayout.Label("연습장 — 크기가 안 줄어든다. 손맛만 본다");
                GUILayout.Label($"크기 {_st.N}    걷기 → 2칸 오르기 → 2칸 더 → 3칸 낙하 → 1칸 턱 → 2칸 낙하");
                GUILayout.Label($"← → 이동   [ ] 크기   R 처음   K 조절   G 그림 {(_useArt ? "켬" : "끔(그레이박스)")}   T 판으로");
            }
            else
            {
                var def = _defs[_index];
                GUILayout.Label($"{_index + 1}/{_defs.Length}  {def.name}" + (_cleared.Contains(def.id) ? "   (깬 판)" : ""));
                GUILayout.Label($"크기 {_st.N}    걸음 {_moves} / 최단 {def.best}    " +
                                $"되돌아가기 {def.back}{(def.back == 0 ? "  ← 오른쪽만 누르면 풀린다" : "")}");
                GUILayout.Label(_won
                    ? (_index < _defs.Length - 1 ? "빠져나왔다" : "여기까지가 지금 있는 전부입니다")
                    : $"← → 이동   Z 되돌리기   R 다시   N/P 판   K 조절   G 그림 {(_useArt ? "켬" : "끔")}   T 연습장");
            }
            GUILayout.EndArea();

            if (_showPanel) DrawTuningPanel();
        }

        void DrawTuningPanel()
        {
            var knobs = _tune.Knobs();
            float h = 62 + knobs.Length * 30;
            var r = new Rect(Screen.width - 332, 12, 320, h);
            GUI.Box(r, GUIContent.none);
            GUILayout.BeginArea(new Rect(r.x + 12, r.y + 10, r.width - 24, r.height - 20));
            GUILayout.Label("움직임 조절  —  K로 닫기");
            foreach (var (label, min, max, get, set) in knobs)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{label}", GUILayout.Width(112));
                float v = GUILayout.HorizontalSlider(get(), min, max, GUILayout.Width(120));
                set(v);
                GUILayout.Label(v.ToString(v < 1f ? "0.00" : "0.0"), GUILayout.Width(46));
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("저장")) _tune.Save();
            if (GUILayout.Button("기본값")) { Tuning.Reset(); _tune = new Tuning(); }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
