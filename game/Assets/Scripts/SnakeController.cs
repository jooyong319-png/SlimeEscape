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

        // ⚠️ 색 컨셉은 **아직 안 정해졌다** (09-02).
        //
        //    브리프 §86은 *"버려진 기계 유적 · 차가운 회청색 돌 + 놋쇠빛 홈 ·
        //    슬라임만 따뜻한 색"*이라고 적어둔 상태다. 그대로 한번 칠해보았고,
        //    사장님이 보신 뒤 **전 색(초록 굴)으로 되돌리셨다.** 지금 값이 그것이다.
        //
        //    🔴 다만 칠해보면서 찾은 문제는 그대로 남아 있다 —
        //    **돌·홈·슬라임이 전부 초록이라 색이 아무 일도 안 한다.**
        //    목표와 나를 가르는 일을 테두리 굵기가 혼자 떠맡고 있다.
        //    컨셉을 정할 때 이걸 먼저 풀 것 — 유적이든 굴이든,
        //    **지형·목표·나 셋은 서로 다른 색이어야 한다.**
        static readonly Color BgCol   = new Color32(0x0d, 0x13, 0x11, 0xff);
        // 🔴 빈 칸은 **어둡게**, 벽은 **밝은 덩어리**로. 예전엔 둘이 #18221e / #25322c 라
        //    거의 같아서 넓은 판에서 굴 모양이 아예 안 읽혔다 (08-30 사장님 전체화면).
        // 🔴 빈 칸이 한 색이라 굴 안이 **평평한 판**으로 보였다.
        //    빛이 위에서 들어온다고 치고 위아래로 기울인다. 규칙과 맞는 거짓말이다 —
        //    이 게임은 중력이 있고, 위가 곧 "올라가야 할 쪽"이다.
        //  🔴 아래쪽을 너무 어둡게 깎았더니 **빈 칸이 홈 속과 같은 색**이 됐다(09-02 화면).
        //     기울기는 위를 밝히는 쪽으로 준다 — 바닥은 홈보다 확실히 밝게 남긴다.
        static readonly Color Floor    = new Color32(0x11, 0x19, 0x16, 0xff);  // 빈 칸 = 공기 (중간)
        static readonly Color FloorLit = new Color32(0x1c, 0x28, 0x23, 0xff);  // 굴 위쪽 — 빛이 닿는다
        static readonly Color FloorDim = new Color32(0x10, 0x17, 0x14, 0xff);  // 굴 바닥 — 가라앉는다
        static readonly Color Overhang = new Color32(0x06, 0x0a, 0x09, 0x90);  // 처마 밑 그늘
        static readonly Color Rock    = new Color32(0x2f, 0x3e, 0x37, 0xff);   // 벽 = 차가운 회청색 돌
        static readonly Color RockTop = new Color32(0x62, 0x7e, 0x70, 0xff);   // 딛고 설 수 있는 윗면
        static readonly Color RockDeep= new Color32(0x25, 0x31, 0x2b, 0xff);   // 돌 속 — 겉보다 어둡다
        static readonly Color RockRim = new Color32(0x45, 0x58, 0x4e, 0xff);   // 옆·아랫면의 옅은 테
        static readonly Color Grid    = new Color32(0x1c, 0x27, 0x22, 0xff);
        static readonly Color HeadCol = new Color32(0xb8, 0xeb, 0xd3, 0xff);   // 지도 위 "여기 있다" 표시
        static readonly Color BodyCol = new Color32(0x8d, 0xce, 0xb0, 0xff);   // 나
        static readonly Color FoodCol = new Color32(0xf3, 0x8a, 0x04, 0xff);   // 조각
        // 🔴 목표 홈 — 바닥(0x18221e)과 색이 거의 같아 안 보였다.
        //    속은 훨씬 어둡게 파고, 민트 테두리를 둘러 확실히 띄운다.
        //  🔴 홈 속은 **어느 빈 칸보다도 어두워야** 한다. 그래야 파인 것으로 읽힌다.
        static readonly Color HoleCol   = new Color32(0x04, 0x08, 0x06, 0xff);  // 파인 속
        static readonly Color HoleEdge  = new Color32(0x7c, 0xc0, 0x9f, 0xff);  // 테두리(민트)
        // 🔴 홈을 두르는 **놋쇠 틀** — 참고 그림의 그 틀이다.
        //    돌도 초록 · 슬라임도 초록인데 홈만 놋쇠라서 **목표가 색으로 뛰어나온다.**
        static readonly Color FrameCol  = new Color32(0xc9, 0x9a, 0x46, 0xff);  // 놋쇠 레일
        static readonly Color FrameDim  = new Color32(0x6f, 0x53, 0x22, 0xff);  // 그늘진 놋쇠
        static readonly Color SlotLine  = new Color32(0x25, 0x33, 0x2b, 0xff);  // 틀 안 칸을 나누는 선
        static readonly Color Frame2Col = new Color32(0x8f, 0xa8, 0xc4, 0xff);  // 둘째 틀 — 식은 은빛
        static readonly Color Frame2Dim = new Color32(0x4a, 0x5a, 0x70, 0xff);
        Color RailOf(int door) => door == 0 ? FrameCol : Frame2Col;
        Color RailDimOf(int door) => door == 0 ? FrameDim : Frame2Dim;
        static readonly Color HoleFill  = new Color32(0x35, 0x5c, 0x49, 0xff);  // 몸이 덮인 칸 — 자물쇠에 불이 든다
        static readonly Color CoreCol   = new Color32(0xf0, 0xc0, 0x5a, 0xff);  // 심 = 열쇠 구멍
        static readonly Color CoreRing  = new Color32(0x8a, 0x66, 0x22, 0xff);
        readonly List<(int door, SpriteRenderer ring, SpriteRenderer core)> _coreViews
            = new List<(int, SpriteRenderer, SpriteRenderer)>();

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
        // 🔴 홈을 채우면 몸이 **문으로 빨려들어간다.** 그냥 사라지면 "냈다"는 느낌이 안 난다.
        //    몸은 이미 문 칸을 정확히 덮고 있으므로, 그 칸들을 심 쪽으로 끌어당기면 된다.
        /// 🔴 빨려드는 덩어리 하나. 홈을 따라 심까지 **흘러간다** —
        ///    제자리에서 쪼그라들면 "사라졌다"로 보이지, "빨렸다"로 안 보인다.
        class Blob
        {
            public SpriteRenderer sr;
            public List<Vector2> path;   // 제 칸에서 심까지, 홈을 따라가는 길
            public float len;            // 그 길의 총 길이(칸)
            public float when;           // 출발 시각(연출 시작 기준)
        }
        readonly List<Blob> _blobs = new List<Blob>();
        SpriteRenderer _gulp;            // 심이 울컥하는 빛
        int _drainCore = -1;
        float _drainAt, _drainDone;
        float _lastGulp = -1f;           // 마지막으로 삼킨 시각

        static readonly Color SpentCol = new Color32(0x3a, 0x4a, 0x43, 0xff);   // 굳은 몸 — 돌이 됐다
        static readonly Color SpentTop = new Color32(0x66, 0x7d, 0x72, 0xff);   // 윗면 — 여기 설 수 있다

        // 🔴 출구 — 맵을 넘는 자리. 동작만 넣고 **그리는 걸 빼먹어서** 아무것도 안 보였다(08-30).
        //    문(홈)과 헷갈리면 안 되니 색을 아예 다르게 — 따뜻한 빛으로 둔다.
        static readonly Color StarLit   = new Color32(0xf0, 0xc9, 0x6b, 0xff);  // 땡 별  ⚠️ 심(CoreCol #f0c05a)과 거의 같은 금빛이다
        Vector2 _lookDir = Vector2.right;   // 마지막으로 간 쪽 — 눈이 그쪽을 본다
        static readonly Color PanelBg   = new Color32(0x14, 0x1c, 0x19, 0xff);  // 안내판 바탕
        static readonly Color StageBg   = new Color32(0x0a, 0x0f, 0x0d, 0xff);  // 안내판 속 무대
        // 표지판 — 맵에 떠 있는 화살표. 지형과 안 섞이게 푸른빛으로 둔다.
        static readonly Color SignFrame = new Color32(0xdc, 0xe6, 0xf2, 0xff);
        static readonly Color SignFill  = new Color32(0x24, 0x3a, 0x58, 0xff);
        static readonly Color SignArrow = new Color32(0x6f, 0xb0, 0xf0, 0xff);
        // 판 고르기 지도 — 굴 입구처럼 보이게
        static readonly Color NodeStone = new Color32(0x39, 0x4a, 0x42, 0xff);
        static readonly Color NodeTop   = new Color32(0x5e, 0x75, 0x69, 0xff);
        static readonly Color NodeLock  = new Color32(0x21, 0x2c, 0x27, 0xff);

        // 🔴 받침대 — **홈이 아니다.** 여기 놓은 몸은 점수가 안 되고 계단만 된다.
        //    홈(민트)과 색을 확실히 갈라놓아야 사람이 헷갈리지 않는다.
        static readonly Color PadCol  = new Color32(0x4e, 0x5a, 0x6b, 0xff);   // 빈 받침대 — 돌보다 확실히 푸르게
        static readonly Color PadEdge = new Color32(0x8e, 0x9e, 0xb5, 0xff);
        readonly Dictionary<int, SpriteRenderer> _padViews = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, SpriteRenderer> _padTops = new Dictionary<int, SpriteRenderer>();
        SpriteRenderer _starView, _starGlow;
        readonly List<SpriteRenderer> _foodExtra = new List<SpriteRenderer>();   // 조각의 빛·반짝임
        readonly List<SpriteRenderer> _links = new List<SpriteRenderer>();       // 마디를 잇는 이음매

        // 🔴 홈을 채우면 **홈이 곧 출구다.** 슬라임이 심 쪽으로 줄줄이 빨려든다.
        //    문을 따로 두고 걸어 나가게 했더니 지루하기만 했다 (08-30 사장님).
        const float DrainSpeed = 10f;      // 빨려드는 속도 (칸/초)
        const float DrainWave  = 0.05f;    // 바깥 칸이 먼저 딸려가는 간격
        const float DrainGulp  = 0.18f;    // 심이 울컥하는 시간
        /// 다 빨아들이고 결과 화면까지 두는 사이
        const float WinDelay = 0.5f;

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
            public float segmentSize = 0.94f; // 마디를 칸보다 얼마나 작게 그리나
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
        readonly Dictionary<int, SpriteRenderer> _holeLips = new Dictionary<int, SpriteRenderer>();  // 홈 **아래 안쪽**의 빛
        /// 놋쇠 틀 조각들. (문번호, 그림, 장식인가)
        readonly List<(int door, SpriteRenderer sr, bool gem)> _frame
            = new List<(int, SpriteRenderer, bool)>();
        readonly HashSet<int> _seated = new HashSet<int>();          // 지금 몸이 덮고 있는 홈 칸
        readonly Dictionary<int, float> _seatAt = new Dictionary<int, float>();  // 그 칸이 덮인 시각
        /// 이웃한 목표 칸의 속을 이어 붙이는 조각. (칸 두 개, 그림 하나)
        readonly List<(int a, int b, SpriteRenderer sr)> _bridges = new List<(int, int, SpriteRenderer)>();
        readonly Dictionary<int, int> _doorOf = new Dictionary<int, int>();   // 칸 -> 문 번호
        /// 굳은 몸의 윗면. 문이 열린 뒤에만 보인다.
        readonly Dictionary<int, SpriteRenderer> _spentTop = new Dictionary<int, SpriteRenderer>();
        const float SlotInner = 0.82f;   // 홈 속의 크기. 나머지가 테두리로 보인다
        const float SeatFlash = 0.30f;   // 홈 한 칸이 채워질 때 밝아졌다 가라앉는 시간
        const float KeyBlink  = 1.7f;    // 머리 열쇠가 번쎍이는 간격(초)
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
        // 🔴 "깼다"만 저장하던 걸 **판마다 최소 걸음**으로 바꿨다.
        //    별을 걸음 수로 주기로 했으니 기록 자체가 진행 상황이 된다 (08-30).
        //    id 에는 ':' 가 안 들어가므로 "id:걸음" 을 쉼표로 잇는다.
        // 🔴 판을 갈아치우면 이 이름을 올린다. 번호(1-1, 1-2…)가 같아서
        //    옛 기록이 **전혀 다른 판**에 그대로 붙어 버린다 —
        //    안 깬 판에 별 셋이 달려 있었다 (08-31 화면).
        const string ProgressKey = "snakeRecords_rev2";
        readonly Dictionary<string, int> _recs = new Dictionary<string, int>();

        bool Cleared(string id) => _recs.ContainsKey(id);

        /// 이 판에 오래 붙잡혀 있나 — 도움은 이때만 내민다
        // 🔴 앞 두 힌트는 저장된 정답만 읽으므로 **상태 계산이 필요 없다** —
        //    그래서 상태가 너무 커서 계산을 못 하는 큰 판에서도 뜬다.
        bool Stuck => !_won && !_replay && Time.time - _levelAt > StuckSeconds
                      && (_hint < 2 || (Lost() != null && Lost().Ready));

        /// <summary>
        /// 🔴 별 셋 (08-30 사장님 규칙):
        ///   ★     그냥 깼다
        ///   ★★   별을 먹고 깼다
        ///   ★★★ 별을 먹고 **커트라인 안에** 깼다
        /// 기록은 "별 먹었으면 걸음 수, 아니면 걸음 수 + StarBit" 한 숫자로 저장한다.
        /// </summary>
        const int StarBit = 100000;      // 이 자리에 "별 못 먹음"을 얹는다

        /// 이번에 이렇게 깼으면 별 몇 개인가
        int StarsFor(SnakeLevelJson d, int steps, bool gotStar)
        {
            if (!gotStar) return 1;
            int cut = d.cut > 0 ? d.cut : (d.bestStar > 0 ? d.bestStar * 2 : int.MaxValue);
            return steps <= cut ? 3 : 2;
        }

        /// 지금까지 이 판에서 받은 가장 좋은 별
        int Stars(SnakeLevelJson d)
        {
            if (d == null || !_recs.TryGetValue(d.id, out int rec)) return 0;
            bool gotStar = rec < StarBit;
            return StarsFor(d, gotStar ? rec : rec - StarBit, gotStar);
        }

        /// 이 판에서 가장 좋았던 결과만 남긴다 (별을 먹은 기록이 늘 이긴다)
        void Record(string id, int steps, bool star)
        {
            int now = star ? steps : steps + StarBit;
            if (!_recs.TryGetValue(id, out int old) || now < old) _recs[id] = now;
            SaveProgress();
        }

        /// <summary>
        /// 🔴 앞 판을 깨면 다음 판이 열린다. 다만 **세 판 앞까지 미리 열어둔다.**
        ///    퍼즐 난이도는 사람마다 튄다 — 한 판에 막혀 게임이 끝나면 안 된다.
        ///    (잘된 퍼즐 게임은 전부 여러 판을 동시에 열어둔다)
        ///    F1 개발 모드에서는 전부 열린다 — 사장님이 아무 판이나 보실 수 있게.
        /// </summary>
        bool Unlocked(int i)
        {
            if (_dev || i == 0) return true;
            int done = 0;
            for (int k = 0; k < _set.levels.Length; k++) if (Cleared(_set.levels[k].id)) done++;
            return i <= done + 3;
        }

        int _steps;      // 지금 시도의 걸음 수. 되돌리면 같이 줄어든다

        // ---- 막힌 사람 구하기 ----
        // 🔴 정답을 통째로 보여주면 그 판은 거기서 끝난다. **한 걸음씩만** 민다.
        //    막힌 사람은 그냥 나간다 — 나간 사람의 플레이타임은 0분이다.
        //    도움은 분량을 깎는 게 아니라, 0분이 될 뻔한 걸 살리는 것이다.
        const float StuckSeconds = 150f;   // 이만큼 못 깨면 도움을 내민다
        float _levelAt;                    // 이 판을 언제 열었나
        int _nudges;                       // 이 판에서 민 횟수 (셋째 힌트만 센다)

        // 🔴 힌트는 **종류**로 나뉜다. 횟수를 묶는 것보다 낫다 —
        //    필요한 만큼만 열어보고, 답까지 가는 건 스스로 정한다.
        //      1 어느 홈부터   — 저장된 정답에서 바로. 스포일러가 거의 없다
        //      2 안 먹는 조각  — 저장된 정답에서 바로. 제일 안 보이는 실패를 막는다
        //      3 다음 한 걸음  — 상태를 다 펼쳐야 한다. 이것만 별 셋을 못 받는다
        int _hint;                         // 지금까지 연 힌트 단계 (0~3)
        bool _tooBig;                      // 판이 너무 커서 다음 걸음을 못 알려준다
        int _hintDoor = -1;                // 먼저 채워야 할 홈
        readonly HashSet<int> _skipFood = new HashSet<int>();   // 먹으면 안 되는 조각 칸
        float _nudgeShow;                  // 민 방향을 화면에 띄워둘 시각
        SnakeEngine.Dir _nudgeDir;
        bool _menu;      // 판 고르기 화면

        // ---- 깬 뒤 결과 화면 ----
        // 🔴 깨자마자 다음 판으로 넘기면 "해냈다"가 없다. 별을 하나씩 찍어 보여주고 넘어간다.
        bool _result;
        float _resultAt;         // 화면이 뜬 시각 — 별이 나오는 박자를 여기서 잰다
        int _resultStars, _resultSteps;
        bool _resultStar;        // 이번에 별을 먹었나
        bool _resultBest;        // 최고 기록을 갈아치웠나
        int _shown;              // 지금까지 소리를 낸 별 개수
        const float StarBeat = 0.42f, StarPop = 0.30f, StarLead = 0.35f;
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
        bool _lostTried;          // 한 번 만들어 봤나 (실패해도 다시 안 만든다)

        /// <summary>
        /// 🔴 "이미 진 자리인가"를 알려면 상태를 전부 펼쳐야 한다. 큰 판은 39만 개다.
        /// 그래서 **막혀서 도움이 필요해진 순간에 한 번만** 만든다.
        /// 너무 크면 못 만들고, 그때는 도움 단추가 아예 안 뜬다.
        /// </summary>
        LostSet Lost()
        {
            if (!_lostTried) { _lostTried = true; _lostSet = new LostSet(_L); }
            return _lostSet;
        }
        SnakeLog.Run _run;
        float _runStart;

        // ---- 정답 재생 (개발자 전용, F3) ----
        //    "이게 진짜 깨져?"에 답하는 제일 빠른 방법은 게임이 직접 두는 것이다.
        //    재생 중에는 기록을 안 남긴다 — 검증 데이터가 더러워지면 안 된다.
        bool _replay;
        bool _allDone;   // 마지막 판까지 깼다 — 결과 화면

        // 🔴 어느 문으로 나갔느냐가 다음 방을 정한다. 지도 화면이 필요 없는 갈래다.
        int _wonBy = -1;

        // ---- 판 넘기 ----
        // 🔴 판 목록 게임이다. 홈을 다 채우면 문이 열리고, **출구까지 걸어가야** 다음 판이다.
        //    출구까지 가는 것 자체가 퍼즐이다 — 몸을 잘못 두고 오면 못 닿는다.
        //    (세계 지도·문양 획·양방향 이동은 08-30에 걷어냈다: RPG 부속이었고 퍼즐을 안 늘렸다)
        readonly List<string> _trail = new List<string>();   // 지나온 방

        // ---- 안내 화면 ----
        // 🔴 친구 검증에서 나온 첫 마디가 "설명도 없이 하라고만 나온다"였다 (2026-08-29).
        //    브리프 §6이 금지하는 건 **스토리**지 **규칙을 설명하는 그림**이 아니다.
        //    글로 늘어놓지 않고, 실제로 화면에 나오는 색을 그대로 보여주며 한 줄씩 붙인다.
        bool _intro = true;
        /// 🔴 초기화는 되돌릴 수 없다 — 한 번 더 묻는다
        bool _askReset;
        const string IntroKey = "snakeSeenIntro";
        readonly Dictionary<Color, Texture2D> _chips = new Dictionary<Color, Texture2D>();

        // ---- 손가락 조작 ----
        // 🔴 휴대폰엔 키보드가 없다. 그리고 한 판에 100걸음을 넘기기도 하므로
        //    스와이프만 두면 손이 아프다 — 방향 패드를 같이 둔다.
        bool Touchy => Application.isMobilePlatform || Input.touchSupported;
        Vector2 _swipeFrom;
        bool _swiping;
        float _uiScale = 1f;

        /// <summary>
        /// 🔴 글자 크기를 **11의 배수**로 떨어뜨린다.
        ///
        /// 갈무리11은 11픽셀에 맞춰 그려진 도트 글꼴이다. 13px·20px 처럼
        /// 어중간하게 쓰면 획 하나가 1.2픽셀이 되어 **글자만 흐릿해진다** —
        /// 화면 전체를 도트로 맞춰놨는데 글자에서 티가 나면 다 무너진다.
        ///
        /// 그래서 원하는 크기에서 가장 가까운 배수로 내린다. 22 아래는 11로 둔다
        /// (11픽셀이 한글 최소 크기다 — 더 줄이면 받침이 뭉갠다).
        /// </summary>
        static int Px(float want)
        {
            const int Base = 11;
            int k = Mathf.RoundToInt(want / Base);
            return Mathf.Max(1, k) * Base;
        }
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
            _cam.rect = new Rect(0f, 0f, 1f, 1f);
            // 🔴 듣는 귀가 없으면 소리가 안 난다. 별 딸 때 "땅!" 이 안 들리고 있었다 (08-31).
            if (_cam.GetComponent<AudioListener>() == null) _cam.gameObject.AddComponent<AudioListener>();

            _set = SnakeLevels.Load();
            _gravity = _set.gravity;
            LoadProgress();
            _intro = PlayerPrefs.GetInt(IntroKey, 0) == 0;
            _menu  = !_intro;    // 🔴 안내를 이미 본 사람은 바로 판 목록으로. 안 그러면 목록을 볼 길이 없다

            int start = 0;
            while (start < _set.levels.Length && Cleared(_set.levels[start].id)) start++;
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
            //  🔴 밖에서 상태를 알 수 있게 한 줄 남긴다. 화면을 눈으로 확인하지 않고도
            //     "지금 어느 판이 열렸는가"를 로그로 알 수 있어야 자동 확인이 된다 (09-02).
            //  🔴 표는 **아스키**로 둔다. 밖에서 로그를 찾을 때 한글 패턴은
            //     기기마다 글자판이 어긋나 안 잡힌다 (09-02에 이것 때문에 한참 헤맬다).
            Debug.Log("[LV] " + _set.levels[_index].id);
            _wonBy = -1;
            if (_trail.Count == 0 || _trail[_trail.Count - 1] != _set.levels[_index].id)
                _trail.Add(_set.levels[_index].id);
            // 🔴 가르치는 판에서는 카메라가 **왼쪽만** 쓴다. 오른쪽은 안내판 자리다.
            //    자리를 비워두지 않고 겹쳐 그렸다가 안내가 판에 잘려 나갔다 (08-31).
            //    Awake 에서 하면 안 된다 — 그때는 _set 이 아직 없어서 죽는다.
            if (_cam != null)
                _cam.rect = Def.tutorial ? new Rect(0f, 0f, 1f - GuideW, 1f)
                                         : new Rect(0f, 0f, 1f, 1f);

            _L = SnakeLevels.ToLevel(Def, _gravity);
            BuildBoard();

            // 🔴 "여기서부턴 못 이긴다"를 미리 계산해 둔다. 지금 판은 수백 상태라 눈 깜짝할 새다.
            //    플레이어에게 보여주려는 게 아니라, 헛되이 쓴 시간을 기록하려는 것이다.
            // 🔴 **판을 열 때 계산하지 않는다.** 큰 판은 상태가 39만 개라 몇 초씩 멈춘다.
            //    도움이 필요해진 순간(2분 30초 막힘)에 한 번만 만든다.
            _lostSet = null; _lostTried = false;
            _slide = 0; _slideAt = -99f;
            _levelAt = Time.time; _nudges = 0; _nudgeShow = -9f;
            _hint = 0; _hintDoor = -1; _tooBig = false; _skipFood.Clear();
            ReadSolution();   // 무엇을 알려줄 수 있는지 미리 안다 (정답 되짚기라 값이 싸다)
            StartRun();

            Restart();
            AimCamera(true);       // 판을 열 때는 딱 맞춘다
        }

        /// 🔴 출구를 밟았다 — 이 판을 깨고 목록의 다음 판으로. 마지막이면 끝낸다.
        void ClearAndAdvance()
        {
            bool star = _st.Sc != 0;
            // 🔴 밀어서 깼으면 별 셋은 못 준다. 안 그러면 밀기가 곧 정답 보기가 된다.
            //    깬 건 깬 거라 진행은 시켜주되, 걸음 수를 커트라인 밖으로 적는다.
            int keep = _nudges > 0 && Def.cut > 0 ? Mathf.Max(_steps, Def.cut + 1) : _steps;
            int had = _recs.TryGetValue(Def.id, out int old) ? old : int.MaxValue;
            int now = star ? keep : keep + StarBit;
            _resultBest = now < had;
            Record(Def.id, keep, star);

            _resultStar = star;
            _resultSteps = _steps;
            _resultStars = StarsFor(Def, keep, star);
            _result = true; _resultAt = Time.time; _shown = 0;
        }

        /// 결과 화면을 닫고 다음 판으로
        void AdvanceNow()
        {
            _result = false;
            if (_index + 1 < _set.levels.Length) { Load(_index + 1); return; }
            EndRun(); _allDone = true;
        }

        // 🔴 소리 파일을 두지 않는다 — 그때그때 만들어 쓴다. 빌드에 자산이 안 붙는다.
        AudioSource _audio;
        AudioClip[] _dings;

        static AudioClip MakeDing(float freq, float len)
        {
            const int rate = 44100;
            int n = Mathf.Max(1, (int)(rate * len));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float x = i / (float)rate;
                float env = Mathf.Exp(-x * 8f);                    // 땅 — 치고 빠진다
                data[i] = env * 0.30f * (Mathf.Sin(2f * Mathf.PI * freq * x)
                                       + 0.45f * Mathf.Sin(4f * Mathf.PI * freq * x));
            }
            var c = AudioClip.Create("ding", n, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }

        void Ding(int i)
        {
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                // 세 음이 올라간다 — A5 · C#6 · E6
                _dings = new[] { MakeDing(880f, 0.5f), MakeDing(1109f, 0.5f), MakeDing(1319f, 0.7f) };
            }
            _audio.PlayOneShot(_dings[Mathf.Clamp(i, 0, 2)]);
        }

        /// <summary>
        /// 🔴 다음 한 걸음만 민다. **처음부터의 정답이 아니라 지금 자리에서의 정답**이다 —
        /// 어디서 꼬였든 거기서부터 알려준다. 이미 진 자리면 되돌리라고 말해준다.
        /// </summary>
        void Nudge()
        {
            var ls = Lost();
            if (ls == null || !ls.Ready) { _nudgeDir = SnakeEngine.Dir.Up; _nudgeShow = -1f; _tooBig = true; return; }
            _tooBig = false;
            if (ls.IsLost(_st)) { _nudgeShow = Time.time; _nudges++; return; }
            if (!ls.Nudge(_L, _st, out var dir)) return;
            _nudges++;
            _nudgeDir = dir;
            _nudgeShow = Time.time;
        }

        /// <summary>
        /// 🔴 저장된 정답을 되짚어 **두 가지**를 알아낸다 —
        /// 어느 홈을 먼저 채우는가, 그리고 어느 조각을 안 먹는가.
        /// 상태를 펼치지 않으므로 아무리 큰 판에서도 된다.
        /// </summary>
        void ReadSolution()
        {
            _hintDoor = -1; _skipFood.Clear();
            string sol = Def.sol;
            if (string.IsNullOrEmpty(sol)) return;

            var st = SnakeEngine.StartState(_L);
            int dm = 0;
            foreach (char ch in sol)
            {
                SnakeEngine.Dir d;
                switch (ch)
                {
                    case '↑': d = SnakeEngine.Dir.Up; break;
                    case '↓': d = SnakeEngine.Dir.Down; break;
                    case '←': d = SnakeEngine.Dir.Left; break;
                    case '→': d = SnakeEngine.Dir.Right; break;
                    case '↧': d = SnakeEngine.Dir.Drop; break;
                    default: continue;
                }
                if (!SnakeEngine.Step(_L, st, d, out var ns)) break;
                if (_hintDoor < 0 && ns.Dm != dm)
                    for (int i = 0; i < _L.Doors.Count; i++)
                        if ((dm & (1 << i)) == 0 && (ns.Dm & (1 << i)) != 0) { _hintDoor = i; break; }
                dm = ns.Dm; st = ns;
            }
            // 정답이 끝까지 안 먹은 조각 = 먹으면 안 되는 조각
            for (int i = 0; i < _L.Foods.Count; i++)
                if (!SnakeEngine.IsEaten(st, i)) _skipFood.Add(_L.Foods[i]);
        }

        /// <summary>
        /// 🔴 그 판에서 **실제로 알려줄 게 있는** 단계만 센다.
        ///    홈이 하나면 "어느 홈부터"가 뜻이 없고, 조각을 다 먹어야 하는 판이면
        ///    "먹으면 안 되는 조각"이 뜻이 없다. 없는 걸 단추로 내밀면 안 된다 —
        ///    눌렀다가 조용히 셋째 단계로 떨어져 별 셋을 잃는다 (08-31 사장님 지적).
        /// </summary>
        bool HasHint(int lv) => lv == 1 ? _L.Doors.Count >= 2
                              : lv == 2 ? _skipFood.Count > 0
                              : true;      // 셋째는 눌렀을 때 되는지 본다

        /// 다음에 열 수 있는 단계. 없으면 0.
        int NextHint()
        {
            for (int lv = _hint + 1; lv <= 3; lv++) if (HasHint(lv)) return lv;
            return _hint >= 3 ? 3 : 0;     // 이미 셋째면 계속 한 걸음씩
        }

        /// 다음 힌트를 연다. 단계가 올라갈수록 알려주는 게 많아진다.
        void OpenHint()
        {
            int lv = NextHint();
            if (lv == 0) return;
            _hint = lv;
            if (lv == 3) Nudge();          // 셋째 단계부터는 누를 때마다 한 걸음
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
                case '↧': Step(SnakeEngine.Dir.Drop); break;
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
            _recs.Clear();
            foreach (var tok in PlayerPrefs.GetString(ProgressKey, "").Split(','))
            {
                if (string.IsNullOrEmpty(tok)) continue;
                int c = tok.LastIndexOf(':');
                if (c <= 0) continue;
                if (int.TryParse(tok.Substring(c + 1), out int v)) _recs[tok.Substring(0, c)] = v;
            }
        }

        void SaveProgress()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in _recs)
            {
                if (sb.Length > 0) sb.Append(',');
                sb.Append(kv.Key).Append(':').Append(kv.Value);
            }
            PlayerPrefs.SetString(ProgressKey, sb.ToString());
            PlayerPrefs.Save();
        }

        /// 문벽을 다 닫은 채로 이어진 칸끼리 묶는다 = 방
        void SplitRooms()
        {
            int total = _L.W * _L.H;
            _roomOf = new int[total];
            for (int i = 0; i < total; i++) _roomOf[i] = -1;
            _roomBox.Clear();

            // 🔴 방 나누기는 **문벽이 있을 때만** 뜻이 있다. 문벽을 없앤 뒤에도 계속 돌아서,
            //    못 가는 주머니 하나 때문에 "방이 여럿"이 되고 카메라가 퍽 튀었다.
            //    (08-30 사장님: "여기는 왜 갑자기 퍽 하고 커짐?" — pv2 에 2칸짜리 주머니가 있었다)
            if (!_L.HasGates) return;      // 방 하나 = 판 전체를 잡는다

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
            _segs.Clear(); _foodViews.Clear(); _foodExtra.Clear(); _links.Clear();

            for (int y = 0; y < _L.H; y++)
                for (int x = 0; x < _L.W; x++)
                {
                    int c = y * _L.W + x;
                    bool wall = _L.IsWall(c);
                    var sr = NewSprite(wall ? "Wall" : "Floor", -3, wall ? "wall" : "floor");
                    sr.transform.position = CellPos(c);
                    //  빈 칸은 높이에 따라 밝기가 다르다 — 위가 밝고 아래가 가라앉는다
                    sr.color = Art.Tint(wall ? "wall" : "floor",
                             wall ? Rock
                             : Color.Lerp(FloorLit, FloorDim, _L.H < 2 ? 0f : (float)y / (_L.H - 1)));

                    // 🔴 돌 **바로 밑**의 빈 칸에 그늘. 굴이 파인 것처럼 보이는 건
                    //    거의 이것 하나가 한다 — 처마 밑은 어둡다.
                    if (!wall && y > 0 && _L.IsWall(c - _L.W))
                    {
                        var ao = NewSprite("Overhang", -2);   // 바닥(-3)보다 확실히 위로
                        ao.transform.position = CellPos(c) + new Vector2(0f, 0.33f);
                        ao.transform.localScale = new Vector3(1f, 0.34f, 1);
                        ao.color = Overhang;
                    }

                    if (wall)
                    {
                        // 🔴 돌을 **덩어리**로 보이게 — 공기와 닿는 겉면은 밝고 속은 어둡다.
                        //    평평한 한 색이면 아무리 칠해도 판때기로 보인다.
                        bool up    = y > 0        && !_L.IsWall(c - _L.W);
                        bool down  = y < _L.H - 1 && !_L.IsWall(c + _L.W);
                        bool left  = x > 0        && !_L.IsWall(c - 1);
                        bool right = x < _L.W - 1 && !_L.IsWall(c + 1);
                        if (!up && !down && !left && !right) sr.color = RockDeep;   // 속

                        // 윗면 — "여기 딛고 설 수 있다". 두고 온 몸의 윗면과 같은 신호다.
                        if (up)
                        {
                            var e = NewSprite("WallTop", -2, "wall_top");
                            e.transform.position = CellPos(c) + new Vector2(0f, 0.42f);
                            e.transform.localScale = new Vector3(1f, 0.16f, 1);
                            e.color = Art.Tint("wall_top", RockTop);
                        }
                        // 🔴 옆·아랫면 테는 **뺐다.** 돌마다 테가 둘리니 다시 타일로 보였고,
                        //    밝은 면이 너무 많아 빈 곳보다 돌이 밝아졌다 (08-31 화면).
                        //    밝은 건 윗면 하나뿐 — 그래야 "설 수 있다"가 신호로 남는다.
                        _ = left; _ = right; _ = down;
                    }
                }

            // 🔴 목표 홈 — 바닥보다 어둡게 파인 것처럼. 몸이 덮으면 한 칸씩 빛이 찬다
            _holes.Clear();
            _holeEdges.Clear();
            _holeLips.Clear();
            _frame.Clear();
            _seated.Clear(); _seatAt.Clear();
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
                int dr = kvDoor.Value;
                //  틀 안 바닥. 칸 가득 깔아 이웃과 붙게 한다 — 한 덩어리로 보여야 한다.
                var edge = NewSprite("HoleEdge", -1, "slot");
                edge.transform.position = CellPos(c);
                edge.transform.localScale = new Vector3(1f, 1f, 1);
                edge.color = SlotLine;

                //  🔴 **덩어리 바깥선에만** 레일을 둔다. 칸마다 두르면
                //     이어진 모양이 "구멍 여럿 개"로 보인다 (08-29에 사람이 61초 동안 못 읽음).
                bool nU = _doorOf.TryGetValue(c - _L.W, out int du) && du == dr;
                bool nD = _doorOf.TryGetValue(c + _L.W, out int dd2) && dd2 == dr;
                bool nL = _doorOf.TryGetValue(c - 1, out int dl) && dl == dr;
                bool nR = _doorOf.TryGetValue(c + 1, out int drr) && drr == dr;

                if (!nU) Rail(c, dr, 0);
                if (!nD) Rail(c, dr, 1);
                if (!nL) Rail(c, dr, 2);
                if (!nR) Rail(c, dr, 3);

                //  꾺이는 모서리에 장식을 박는다 — 참고 그림에서 틀을 틀로 만드는 것이 이것이다.
                float g = 0.5f - GemW * 0.55f;
                if (!nU && !nL) Gem(c, dr, new Vector2(-g,  g));
                if (!nU && !nR) Gem(c, dr, new Vector2( g,  g));
                if (!nD && !nL) Gem(c, dr, new Vector2(-g, -g));
                if (!nD && !nR) Gem(c, dr, new Vector2( g, -g));

                //  틀 안을 칸으로 나눈다 — "몇 칸을 채우는가"가 저절로 세어진다
                if (nR) Seam(c, new Vector2(0.5f, 0f), new Vector3(0.035f, 0.86f, 1));
                if (nD) Seam(c, new Vector2(0f, -0.5f), new Vector3(0.86f, 0.035f, 1));

                // ⚠️ 한번 슬라임 모양의 **거푸집**으로 파봤다가 되돌렸다 (09-02).
                //    바깥은 네모 틀인데 안은 둥근 덩어리가 되어 **모양 언어가 둘로 싸웠다** —
                //    까만 얼룩처럼 보였다. 바꿀 거면 틀과 속을 **같은 모양으로** 바꿔야 한다.
                var inner = NewSprite("HoleInner", 0);
                inner.transform.position = CellPos(c);
                inner.transform.localScale = new Vector3(SlotInner, SlotInner, 1);
                inner.color = HoleCol;

                // 🔴 홈이 **칠해놓은 네모**로 보였다. 파인 구멍처럼 보이게 한다.
                //    빛은 위에서 온다 — 파인 구멍은 **아래 안쪽 벽이 빛을 받는다.**
                //    (위쪽에 그림자를 넣는 게 정석이지만 속이 이미 거의 검어서 안 보인다.)
                //    돌 윗면만 밝게 두는 것과 같은 신호 체계다 — 밝은 면이 곧 방향이다.
                var lip = NewSprite("HoleLip", 1);
                lip.transform.position = CellPos(c) + new Vector2(0f, -0.37f);
                lip.transform.localScale = new Vector3(SlotInner, 0.15f, 1);
                lip.color = Color.Lerp(HoleCol, EdgeOf(kvDoor.Value), 0.44f);
                //  아래에 홈이 또 있으면 거긴 벽이 아니라 속이다 — 안 그린다
                lip.enabled = !_doorOf.ContainsKey(c + _L.W);
                _holeLips[c] = lip;

                _holes[c] = inner;
                _holeEdges[c] = edge;

                // 🔴 굳은 몸의 윗면 — 평소엔 숨어 있다가 문이 열리면 나타난다.
                //    벽의 윗면과 같은 신호라, 여기 설 수 있다는 게 바로 읽힌다.
                var top = NewSprite("SpentTop", 1, "wall_top");
                top.transform.position = CellPos(c) + new Vector2(0f, 0.42f);
                top.transform.localScale = new Vector3(1f, 0.16f, 1);
                top.color = SpentTop;
                top.enabled = false;
                _spentTop[c] = top;
            }

            // 🔴 화살표 표지판 — 맵에 떠 있는 안내. 테두리 · 속 · 화살표 세 겹.
            //    지형·조각·홈 어느 것과도 안 닮게 푸른빛 네모로 둔다.
            foreach (var s in _L.Signs)
            {
                var at = CellPos(s.cell) + new Vector2(0f, 0.06f);
                var frame = NewSprite("Sign", 4);
                frame.transform.position = at;
                frame.transform.localScale = new Vector3(0.74f, 0.74f, 1);
                frame.color = SignFrame;

                var fill = NewSprite("SignFill", 5);
                fill.transform.position = at;
                fill.transform.localScale = new Vector3(0.62f, 0.62f, 1);
                fill.color = SignFill;

                // 화살표 = 자루 하나 + 45도 돌린 네모(머리) 하나
                float ax = s.dir == 2 ? -1 : s.dir == 3 ? 1 : 0;
                float ay = s.dir == 0 ? 1 : s.dir == 1 ? -1 : 0;
                bool horiz = ax != 0;

                var shaft = NewSprite("SignShaft", 6);
                shaft.transform.position = at - new Vector2(ax, ay) * 0.06f;
                shaft.transform.localScale = horiz ? new Vector3(0.34f, 0.11f, 1)
                                                   : new Vector3(0.11f, 0.34f, 1);
                shaft.color = SignArrow;

                var head = NewSprite("SignHead", 6);
                head.transform.position = at + new Vector2(ax, ay) * 0.16f;
                head.transform.localScale = new Vector3(0.20f, 0.20f, 1);
                head.transform.rotation = Quaternion.Euler(0, 0, 45f);
                head.color = SignArrow;
            }

            // 🔴 받침대 — 몸을 얹어두는 선반. 홈과 달리 채워도 판이 안 끝난다.
            _padViews.Clear(); _padTops.Clear();
            foreach (int c in _L.Pads)
            {
                var pad = NewSprite("Pad", -1, "pad");
                pad.transform.position = CellPos(c) + new Vector2(0f, -0.22f);
                pad.transform.localScale = new Vector3(0.96f, 0.5f, 1);
                pad.color = PadCol;
                _padViews[c] = pad;

                var lip = NewSprite("PadTop", 1, "pad_top");
                lip.transform.position = CellPos(c) + new Vector2(0f, 0.02f);
                lip.transform.localScale = new Vector3(0.96f, 0.1f, 1);
                lip.color = PadEdge;
                _padTops[c] = lip;
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

            // 심 — 머리가 마지막에 있어야 할 자리.
            // 🔴 채워진 홈에서는 **지워야 한다.** 안 지우면 노란 표시가 그대로 남아서
            //    "어느 게 내 슬라임이지?" 가 된다 (08-31 사장님 화면).
            _coreViews.Clear();
            for (int di = 0; di < _L.Doors.Count; di++)
            {
                var dd = _L.Doors[di];
                if (dd.Core < 0) continue;
                var ring = NewSprite("CoreRing", 1);
                ring.transform.position = CellPos(dd.Core);
                //  🔴 **구멍처럼** 그린다 — 밝은 테 + 어두운 속.
                //     전에는 밝은 알맹이라 "구멍"이 아니라 놓여 있는 **물건**으로 보였다.
                //     머리에 박힌 열쇠가 꽉 찬 것이니, 구멍은 비어 있어야 짝이 맞는다.
                ring.transform.localScale = Vector3.one * 0.52f;
                ring.color = CoreCol;
                //  🔴 그림에는 테가 이미 들어 있다. 코드 테를 같이 그리면
                //     노란 네모가 샐져나와 홈 한 칸이 크림색 상자로 보였다 (09-02 화면).
                ring.enabled = !Art.Has("core");

                var core = NewSprite("Core", 2, "core");
                core.transform.position = CellPos(dd.Core);
                core.transform.localScale = Vector3.one * 0.30f;
                core.color = HoleCol;
                _coreViews.Add((di, ring, core));
            }

            // 🔴 격자선을 판 전체에 그으면 굴이 아니라 **모눈종이**로 보인다 (08-31 화면).
            //    빈 칸에만, 그것도 아주 옅게 — 칸을 세는 데만 쓰이면 된다.
            for (int y = 1; y < _L.H - 1; y++)
                for (int x = 1; x < _L.W - 1; x++)
                {
                    int c = y * _L.W + x;
                    if (_L.IsWall(c)) continue;
                    if (x + 1 < _L.W && !_L.IsWall(c + 1))
                    {
                        var v = NewSprite("GridV", -2);
                        v.transform.position = CellPos(c) + new Vector2(0.5f, 0f);
                        v.transform.localScale = new Vector3(0.03f, 1f, 1);
                        v.color = Grid;
                    }
                    if (y + 1 < _L.H && !_L.IsWall(c + _L.W))
                    {
                        var hh = NewSprite("GridH", -2);
                        hh.transform.position = CellPos(c) + new Vector2(0f, -0.5f);
                        hh.transform.localScale = new Vector3(1f, 0.03f, 1);
                        hh.color = Grid;
                    }
                }

            foreach (int c in _L.Foods)
            {
                // 🔴 주황 네모는 "블록"이다. 둥글게 하고 위에 반짝임을 얹으면 **먹는 것**이 된다.
                var halo = NewSprite("FoodHalo", 0);
                halo.transform.position = CellPos(c);
                halo.transform.localScale = Vector3.one * 0.68f;
                halo.sprite = PixelSprites.Disc();
                halo.color = new Color(FoodCol.r, FoodCol.g, FoodCol.b, 0.16f);

                var sr = NewSprite("Food", 1, "food");
                sr.transform.position = CellPos(c);
                sr.transform.localScale = Vector3.one * 0.42f;
                sr.sprite = PixelSprites.Disc();
                sr.color = FoodCol;
                _foodViews.Add(sr);

                var shine = NewSprite("FoodShine", 2);
                shine.transform.position = CellPos(c) + new Vector2(-0.07f, 0.08f);
                shine.transform.localScale = Vector3.one * 0.13f;
                shine.sprite = PixelSprites.Disc();
                shine.color = new Color(1f, 1f, 1f, 0.55f);
                _foodExtra.Add(halo); _foodExtra.Add(shine);
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

            // 🔴 별 — 먹어도 몸이 안 자란다. 그래서 조각(주황)과 **다른 색·다른 크기**로 둔다.
            //    헷갈리면 사람이 문 계산을 틀린다.
            _starView = _starGlow = null;
            if (_L.Star >= 0)
            {
                // 🔴 뒤의 빛이 네모라 갈색 상자처럼 보였다 (08-31 화면). 둥글게 바꾼다.
                _starGlow = NewSprite("StarGlow", -1, "star_glow");
                _starGlow.transform.position = CellPos(_L.Star);
                _starGlow.transform.localScale = new Vector3(1.05f, 1.05f, 1);
                _starGlow.sprite = PixelSprites.Disc();

                _starView = NewSprite("Star", 1, "star");
                _starView.transform.position = CellPos(_L.Star);
                _starView.color = StarLit;
            }

            SplitRooms();
        }

        /// 돌 가장자리의 옅은 테. 윗면(설 수 있다)과 헷갈리지 않게 훨씬 어둡다.
        const float RailW = 0.10f;   // 놋쇠 레일 굵기
        const float GemW  = 0.22f;   // 모서리 장식 크기

        /// <summary>
        /// 덩어리 바깥선을 따라 두르는 놋쇠 레일 한 조각.
        /// <paramref name="side"/> 0윗 1아래 2왼 3오른.
        ///
        /// 🔴 그림은 **윗쪽 변 한 장**만 있고 여기서 돌려 쓴다.
        ///    네 변을 따로 그리면 네 장이 되고, 조금씩 어긋나면 틀이 샐뚜려 보인다.
        ///    그림이 없을 땐 예전처럼 얇은 막대를 놓는다.
        /// </summary>
        void Rail(int cell, int door, int side)
        {
            var sr = NewSprite("Rail", 1, "rail");
            bool art = Art.Has("rail");
            float d = 0.5f - RailW * 0.5f;
            Vector2 off = side == 0 ? new Vector2(0f, art ? 0f : d)
                        : side == 1 ? new Vector2(0f, art ? 0f : -d)
                        : side == 2 ? new Vector2(art ? 0f : -d, 0f)
                                    : new Vector2(art ? 0f : d, 0f);
            sr.transform.position = CellPos(cell) + off;
            if (art)
            {
                sr.transform.localScale = Vector3.one;
                sr.transform.rotation = Quaternion.Euler(
                    0, 0, side == 0 ? 0f : side == 1 ? 180f : side == 2 ? 270f : 90f);
            }
            else
            {
                sr.transform.localScale = side <= 1 ? new Vector3(1f, RailW, 1)
                                                    : new Vector3(RailW, 1f, 1);
            }
            sr.color = Art.Tint("rail", RailOf(door));
            _frame.Add((door, sr, false));
        }

        /// 꾺이는 모서리의 장식. 이게 있어야 선이 아니라 **틀**로 보인다.
        void Gem(int cell, int door, Vector2 off)
        {
            var sr = NewSprite("Gem", 2, "gem");
            //  🔴 그림이 있으면 **크기를 그림에 맡긴다.** 코드가 또 줄이면
            //     두 번 줄어들어 보이지도 않는 점이 된다.
            if (Art.Has("gem"))
            {
                sr.transform.position = CellPos(cell) + off;
                sr.transform.localScale = Vector3.one;
            }
            else
            {
                sr.sprite = PixelSprites.Diamond();
                sr.transform.position = CellPos(cell) + off;
                sr.transform.localScale = new Vector3(GemW, GemW, 1);
            }
            sr.color = Art.Tint("gem", RailOf(door));
            _frame.Add((door, sr, true));
        }

        /// 틀 안에서 칸을 나누는 얇은 선. 레일보다 훨씬 약하다.
        void Seam(int cell, Vector2 off, Vector3 scale)
        {
            var sr = NewSprite("Seam", 0);
            sr.transform.position = CellPos(cell) + off;
            sr.transform.localScale = scale;
            sr.color = Color.Lerp(SlotLine, HoleCol, 0.55f);
        }

        void AddRim(int cell, Vector2 off, Vector3 scale)
        {
            var sr = NewSprite("Rim", -2);
            sr.transform.position = CellPos(cell) + off;
            sr.transform.localScale = scale;
            sr.color = RockRim;
        }

        void GridLine(int i, bool vertical)
        {
            var sr = NewSprite("Grid", -2);
            if (vertical) { sr.transform.position = new Vector3(i, -_L.H * 0.5f, 0); sr.transform.localScale = new Vector3(0.035f, _L.H, 1); }
            else { sr.transform.position = new Vector3(_L.W * 0.5f, -i, 0); sr.transform.localScale = new Vector3(_L.W, 0.035f, 1); }
            sr.color = Grid;
        }

        /// <param name="art">
        /// 🔴 그림 자리 이름. `Resources/Art/&lt;이름&gt;.png` 가 있으면 그걸 쓴다.
        /// 없으면 코드가 그린다 — **한 장씩 갈아끼울 수 있다.**
        /// </param>
        SpriteRenderer NewSprite(string name, int order, string art = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Art.Get(art) ?? PixelSprites.Solid();
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>
        /// 🔴 그림이 여러 장인 자리는 매 프레임 다음 장으로 넘긴다.
        /// 그림이 없으면 아무 일도 안 한다 — 코드 그림이 그대로 돈다.
        ///
        /// 빠르기를 자리마다 다르게 둔다. 다 같은 박자로 뛰면 화면이
        /// **한 덩어리로 깜빡여서** 판을 읽는 눈이 흩어진다.
        /// </summary>
        void TickArt()
        {
            SetAll(_foodViews, "food", 7f);
            SetAll(_foodExtra, "food", 7f);
            if (_starView != null) Set(_starView, "star", 9f);
            if (_starGlow != null) Set(_starGlow, "star_glow", 6f);
            if (_keyGlow != null) Set(_keyGlow, "key_glow", 6f);
            foreach (var cv in _coreViews)
                if (cv.core != null) Set(cv.core, "core", 5f);
            foreach (var fb in _frame)
                if (fb.gem && fb.sr != null) Set(fb.sr, "gem", 4f);
        }

        static void Set(SpriteRenderer sr, string art, float fps)
        {
            var s = Art.Frame(art, fps);
            if (s != null && sr.sprite != s) sr.sprite = s;
        }

        static void SetAll(List<SpriteRenderer> list, string art, float fps)
        {
            var s = Art.Frame(art, fps);
            if (s == null) return;
            foreach (var sr in list) if (sr != null && sr.sprite != s) sr.sprite = s;
        }

        /// 빨려드는 진행도 0~1. 이 값으로 홈이 서서히 돌이 된다.
        float SuckT => _drainDone <= 0f ? 1f
                     : Mathf.Clamp01((Time.time - _drainAt) / _drainDone);

        void ClearSuck()
        {
            foreach (var b in _blobs) if (b.sr != null) Destroy(b.sr.gameObject);
            _blobs.Clear();
            if (_gulp != null) { Destroy(_gulp.gameObject); _gulp = null; }
            _drainCore = -1; _drainAt = 0f; _drainDone = 0f; _lastGulp = -1f;
        }

        /// <summary>
        /// 🔴 홈이 채워진 순간. 몸은 이미 홈 칸을 정확히 덮고 있으므로,
        /// **홈을 따라** 심까지 가는 길을 칸마다 하나씩 깔고 그 위로 흘려보낸다.
        /// 심에서 먼 칸이 먼저 출발해야 바깥부터 딸려 들어가는 것처럼 보인다.
        /// </summary>
        void StartSuck(int door)
        {
            ClearSuck();
            if (door < 0 || door >= _L.Doors.Count) return;
            var d = _L.Doors[door];
            _drainCore = d.Core >= 0 ? d.Core : d.Cells[0];

            // 심에서 홈을 따라 너비우선 — 칸마다 "심 쪽으로 한 걸음"을 적어둔다
            var toward = new Dictionary<int, int>();
            var hop = new Dictionary<int, int> { [_drainCore] = 0 };
            var q = new Queue<int>(); q.Enqueue(_drainCore);
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                foreach (int n in new[] { c - 1, c + 1, c - _L.W, c + _L.W })
                {
                    if (!d.Set.Contains(n) || hop.ContainsKey(n)) continue;
                    if (Mathf.Abs(_L.X(n) - _L.X(c)) + Mathf.Abs(_L.Y(n) - _L.Y(c)) != 1) continue;
                    hop[n] = hop[c] + 1; toward[n] = c; q.Enqueue(n);
                }
            }

            float far = 0f;
            foreach (var kv in hop) far = Mathf.Max(far, kv.Value);

            foreach (int c in d.Cells)
            {
                if (c == _drainCore || !hop.ContainsKey(c)) continue;
                var path = new List<Vector2> { CellPos(c) };
                int cur = c;
                while (cur != _drainCore && toward.ContainsKey(cur))
                { cur = toward[cur]; path.Add(CellPos(cur)); }

                var sr = NewSprite("Drain", 6);
                sr.transform.position = path[0];
                sr.color = BodyCol;
                // 먼 칸부터 출발 — 바깥이 먼저 딸려간다
                _blobs.Add(new Blob
                {
                    sr = sr, path = path, len = path.Count - 1,
                    when = (far - hop[c]) * DrainWave,
                });
            }

            _gulp = NewSprite("Gulp", 7);
            _gulp.transform.position = CellPos(_drainCore);
            _gulp.enabled = false;

            float last = 0f;
            foreach (var b in _blobs) last = Mathf.Max(last, b.when + b.len / DrainSpeed);
            _drainDone = last + DrainGulp;
            _drainAt = Time.time;
        }

        /// 길을 따라 t칸만큼 간 자리와, 그 순간 향하는 쪽
        static Vector2 Along(List<Vector2> path, float t, out Vector2 dir)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt(t), 0, path.Count - 2);
            float f = Mathf.Clamp01(t - i);
            dir = (path[i + 1] - path[i]).normalized;
            return Vector2.Lerp(path[i], path[i + 1], f);
        }

        void TickSuck()
        {
            if (_blobs.Count == 0) return;
            float el = Time.time - _drainAt;
            bool any = false;

            foreach (var b in _blobs)
            {
                if (b.sr == null) continue;
                float u = (el - b.when) * DrainSpeed;      // 지금까지 간 칸 수
                if (u < 0f) { b.sr.enabled = true; any = true; continue; }
                if (u >= b.len)
                {
                    if (b.sr.enabled) { b.sr.enabled = false; _lastGulp = el; }
                    continue;
                }
                any = true;

                var pos = Along(b.path, u, out var dir);
                b.sr.transform.position = pos;

                // 🔴 여기가 "쭈아아악"이다 — **가는 쪽으로 늘어나고 옆으로 홀쭉해진다.**
                float speed = Mathf.Clamp01(u / Mathf.Max(0.5f, b.len));
                float stretch = 1f + 0.85f * speed;
                float thin = 1f / (1f + 0.75f * speed);
                bool horiz = Mathf.Abs(dir.x) > Mathf.Abs(dir.y);
                b.sr.transform.localScale = horiz
                    ? new Vector3(0.94f * stretch, 0.94f * thin, 1)
                    : new Vector3(0.94f * thin, 0.94f * stretch, 1);

                var c = Color.Lerp(BodyCol, Color.white, speed * 0.55f);
                c.a = 1f - speed * 0.25f;
                b.sr.color = c;
            }

            // 심이 울컥한다 — 하나 삼킬 때마다
            if (_gulp != null)
            {
                float since = _lastGulp < 0f ? 99f : el - _lastGulp;
                bool on = since < DrainGulp;
                _gulp.enabled = on;
                if (on)
                {
                    float v = 1f - since / DrainGulp;
                    float s = 0.85f + 0.7f * v;
                    _gulp.transform.localScale = new Vector3(s, s, 1);
                    var gc = Color.Lerp(CoreCol, Color.white, v);
                    gc.a = 0.30f + 0.55f * v;
                    _gulp.color = gc;
                    any = true;
                }
            }

            if (!any) ClearSuck();
        }

        void Restart()
        {
            _st = SnakeEngine.StartState(_L);
            ClearSuck();
            _steps = 0;          // 🔴 여기를 빼먹으면 걸음이 판을 넘어 쌓여 커트라인이 무의미해진다
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
                // 🔴 **머리가 맨 위**에 와야 한다. 예전엔 차례를 번갈아 매겨서
                //    뒤쪽 몸이 머리를 덮었다 — 얼굴이 뒤로 밀려 보였다 (09-02 사장님).
                //    머리 4 · 몸 3 · 이음매 2 로 못박는다.
                bool isHead = _segs.Count == 0;
                var sr = NewSprite("Seg", isHead ? 4 : 3, isHead ? "head" : "body");
                _segs.Add(sr);
            }
            for (int i = 0; i < _segs.Count; i++)
            {
                _segs[i].enabled = i < _st.Length;
                // 🔴 각진 네모는 블록으로 보인다. 모서리만 깎아도 말랑해진다.
                // 🔴 그림이 꽂혀 있으면 건드리지 않는다.
                //    예전엔 여기서 매 프레임 코드 그림으로 되돌려버렸다.
                var want = Art.Get(i == 0 ? "head" : "body") ?? PixelSprites.Round();
                if (_segs[i].sprite != want) _segs[i].sprite = want;
            }
            FaceUp();
            for (int i = 0; i < _foodExtra.Count; i++)
            {
                int fi = i / 2;                                   // 조각마다 빛+반짝임 둘
                if (fi < _foodViews.Count) _foodExtra[i].enabled = !SnakeEngine.IsEaten(_st, fi);
            }
            for (int i = 0; i < _foodViews.Count; i++)
            {
                _foodViews[i].enabled = !SnakeEngine.IsEaten(_st, i);
                // 🔴 힌트 2 — 먹으면 안 되는 조각은 잿빛으로 죽인다.
                //    이 게임에서 제일 안 보이는 실패가 "너무 먹은 것"이다.
                bool skip = _hint >= 2 && i < _L.Foods.Count && _skipFood.Contains(_L.Foods[i]);
                _foodViews[i].color = skip ? new Color(0.42f, 0.40f, 0.36f, 0.55f) : FoodCol;
            }

            // 목표 홈에 몸이 들어간 칸은 밝아진다 — "몇 칸 남았는지"가 눈에 보이게
            // 🔴 별 — 안 주웠으면 천천히 돈다. 주우면 사라진다.
            if (_starView != null)
            {
                bool got = _st.Sc != 0;
                _starView.enabled = !got;
                _starGlow.enabled = !got;
                if (!got)
                {
                    float b = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.2f);
                    float s = 0.40f + 0.05f * b;
                    _starView.transform.localScale = new Vector3(s, s, 1);
                    _starView.transform.rotation = Quaternion.Euler(0, 0, Time.time * 40f);
                    var gc = StarLit; gc.a = 0.14f + 0.10f * b;
                    _starGlow.color = gc;
                }
            }

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

            TickSuck();
            TickArt();

            // 🔴 안 채운 홈만 눈에 띄게 둔다 — 남은 칸이 저절로 세어진다
            var body = new HashSet<int>(_st.Body);

            //  🔴 놋쇠 틀. 칸이 아니라 **문 단위**로 움직인다 — 틀은 하나니까.
            //     안 끝난 틀만 천천히 숨을 쉬고, 끝난 틀은 멈춰 돌이 된다.
            foreach (var fb in _frame)
            {
                if (fb.sr == null) continue;
                bool fSpent = (_st.Dm & (1 << fb.door)) != 0;
                var rc = fb.gem ? RailOf(fb.door) : RailDimOf(fb.door);
                if (!fb.gem) rc = Color.Lerp(rc, RailOf(fb.door), 0.55f);
                if (!fSpent)
                {
                    rc = Color.Lerp(rc, Color.white,
                                    0.16f * (0.5f + 0.5f * Mathf.Sin(Time.time * 1.6f)));
                    // 🔴 힌트 1 — 먼저 채울 틀만 깜빡인다.
                    //    글로 "왼쪽 아래요" 하는 것보다 빠르다.
                    if (_hint >= 1 && fb.door == _hintDoor)
                        rc = Color.Lerp(rc, Color.white, 0.35f + 0.35f * Mathf.Sin(Time.time * 4f));
                }
                fb.sr.color = fSpent ? Color.Lerp(rc, SpentCol, SuckT) : rc;
            }
            foreach (var kv in _holes)
            {
                bool covered = body.Contains(kv.Key);
                int door = _doorOf.TryGetValue(kv.Key, out var dn) ? dn : 0;
                // 🔴 이미 연 문은 몸을 두고 온 자리다 — 굳은 색으로 남긴다
                bool spent = (_st.Dm & (1 << door)) != 0;

                // 🔴 칸 하나가 **앉는 순간** 짧게 밝아진다. 판이 클수록 한 칸씩
                //    맞춰 넣게 되는데, 그때마다 "이 칸 됐다"가 손에 잡혀야 한다.
                //    되돌리면 기록도 지운다 — 다시 넣으면 또 반응한다.
                if (covered) { if (_seated.Add(kv.Key)) _seatAt[kv.Key] = Time.time; }
                else if (_seated.Remove(kv.Key)) _seatAt.Remove(kv.Key);

                var fill = FillOf(door);
                if (!spent && _seatAt.TryGetValue(kv.Key, out var sat))
                {
                    float k = (Time.time - sat) / SeatFlash;
                    if (k < 1f) fill = Color.Lerp(EdgeOf(door), fill, k);
                }

                // 🔴 굳는 것도 한 번에 안 한다 — 빨려드는 동안 서서히 돌이 된다
                kv.Value.color = spent
                    ? Color.Lerp(fill, SpentCol, SuckT)
                    : covered ? fill : HoleCol;

                // 아래 안쪽의 빛 — 굳으면 지형이 되므로 끈다
                if (_holeLips.TryGetValue(kv.Key, out var lipSr))
                    lipSr.color = spent
                        ? Color.Lerp(Color.Lerp(HoleCol, EdgeOf(door), 0.44f), SpentCol, SuckT)
                        : Color.Lerp(HoleCol, EdgeOf(door), covered ? 0.20f : 0.44f);

                // 윗면은 **덩어리의 맨 위 칸**에만 켠다. 속에까지 그리면 줄무늬가 된다.
                if (_spentTop.TryGetValue(kv.Key, out var topSr))
                {
                    bool above = _doorOf.TryGetValue(kv.Key - _L.W, out var upDoor)
                                 && (_st.Dm & (1 << upDoor)) != 0;
                    topSr.enabled = spent && !above;
                }
                if (_holeEdges.TryGetValue(kv.Key, out var e))
                {
                    //  틀 안 바닥. 덤으면 가라앉고, 굳으면 돌이 된다.
                    e.color = spent
                        ? Color.Lerp(SlotLine, SpentCol, SuckT)
                        : covered ? Color.Lerp(SlotLine, fill, 0.55f) : SlotLine;
                }
            }
            // 🔴 채워진 홈의 심 표시는 사라진다 — 몸이 빨려드는 동안 같이 흐려진다
            foreach (var cv in _coreViews)
            {
                // 🔴 판이 바뀌면 스프라이트가 먼저 사라진다. 건드리면 예외가 나고
                //    **그 뒤 코드가 통째로 안 돈다** — 그래서 판이 안 깨졌다 (08-31).
                if (cv.ring == null || cv.core == null) continue;
                bool done = (_st.Dm & (1 << cv.door)) != 0;
                float a = done ? 1f - SuckT : 1f;
                cv.ring.enabled = a > 0.02f;
                cv.core.enabled = a > 0.02f;
                if (a > 0.02f)
                {
                    //  머리가 들어와 있으면 구멍 속이 찬다 = 열쇠가 꿂혔다
                    bool inKey = _st.Head >= 0 && _L.Doors[cv.door].Core == _st.Head;
                    cv.ring.enabled = !Art.Has("core") && a > 0.02f;
                    var rc = CoreCol; rc.a = a; cv.ring.color = rc;
                    var cc = inKey ? CoreCol : HoleCol; cc.a = a; cv.core.color = cc;
                }
            }

            // 🔴 받침대: 몸을 놓으면 칸이 꽉 차고 윗면이 밝아진다 = 여기 설 수 있다
            foreach (var kv in _padViews)
            {
                int i = _L.PadIdx[kv.Key];
                bool put = (_st.Pm & (1 << i)) != 0;
                kv.Value.transform.localScale = new Vector3(0.96f, put ? 0.96f : 0.5f, 1);
                kv.Value.transform.position = CellPos(kv.Key) + new Vector2(0f, put ? 0f : -0.22f);
                kv.Value.color = put ? SpentCol : PadCol;
                if (_padTops.TryGetValue(kv.Key, out var lip))
                {
                    lip.transform.position = CellPos(kv.Key) + new Vector2(0f, put ? 0.44f : 0.02f);
                    lip.color = put ? SpentTop : PadEdge;
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
            if (_run != null && !_won && _lostSet != null && _lostSet.Ready && _lostSet.IsLost(_st))
                _run.lostSeconds += Time.deltaTime;

            if (_intro)
            {
                // 🔴 anyKeyDown 은 **마우스 버튼도 포함한다.**
                //    그대로 두면 버튼을 클릭하는 순간 Update가 먼저 안내 화면을 닫아버려서
                //    OnGUI가 화면을 안 그리고, 버튼이 클릭을 받을 기회가 없다.
                //    (08-30: "초기화 안 되는데" — 원인이 이것이었다)
                //    그래서 **키보드만** 여기서 닫는다. 마우스/터치는 OnGUI가 처리한다.
                bool mouse = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)
                          || Input.GetMouseButtonDown(2) || Input.touchCount > 0;
                if (Input.anyKeyDown && !mouse) CloseIntro();
                return;
            }

            // 🔴 결과 화면 — 별이 다 나온 뒤에 눌러야 넘어간다. 안 그러면 별을 못 본다.
            if (_result)
            {
                // 별이 나오는 박자에 맞춰 소리를 낸다
                float el = Time.time - _resultAt;
                while (_shown < 3 && el >= StarLead + _shown * StarBeat)
                { if (_shown < _resultStars) Ding(_shown); _shown++; }

                if (el > StarLead + 3 * StarBeat &&
                    (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0))
                { AdvanceNow(); return; }
                Animate(Time.deltaTime);
                return;
            }

            // 목록 화면에서도 뒤의 판은 계속 숨쉰다. 입력만 OnGUI가 받는다.
            if (_menu)
            {
                //  🔴 목록에서도 개발자 키(F1 · N · P)는 먹게 둔다.
                //     마우스 없이 판에 들어갈 수 있어야 자동 확인이 돌아간다 (09-02).
                //  🔴 유니티 에디터가 F1 을 먹어서 게임까지 안 온다 (09-02).
                //     T 를 같은 뜻으로 둔다 — 혼자 확인할 때 필요하다.
                if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.T))
                { _dev = !_dev; if (!_dev) _showPanel = false; }
                if (_dev && Input.GetKeyDown(KeyCode.N)) { _menu = false; _allDone = false; Load(_index + 1); return; }
                if (_dev && Input.GetKeyDown(KeyCode.P)) { _menu = false; _allDone = false; Load(_index - 1); return; }
                Animate(Time.deltaTime);
                return;
            }

            // 🔴 H — 한 걸음 밀기. 오래 막혔을 때만 열린다.
            if (Input.GetKeyDown(KeyCode.H) && Stuck) { OpenHint(); return; }

            // Esc 로 목록으로 — 언제든 나갈 데가 있어야 갇힌 느낌이 안 든다
            if (Input.GetKeyDown(KeyCode.Escape)) { _menu = true; return; }

            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.T))
            { _dev = !_dev; if (!_dev) _showPanel = false; }

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
                _recs.Clear(); SaveProgress(); Load(0); return;
            }
            if (_dev && Input.GetKeyDown(KeyCode.G))
            {
                _gravity = !_gravity;
                Load(_index);
                return;
            }
            // 열렸으면 잠깐 두고 다음 방으로 — 손이 멈추지 않게
            // 🔴 출구가 있는 맵에서는 문을 채워도 **판이 안 끝난다.**
            //    문벽이 열릴 뿐이고, 넘어가려면 출구까지 걸어가야 한다.
            // 🔴 홈을 채우면 끝. 문이 스르륵 열리는 걸 보여주고 결과 화면으로 넘어간다.
            //    (출구까지 걸어가게 했더니 지루하기만 했다 — 08-30 사장님)
            // 🔴 다 빨려든 다음에 결과 화면. 연출이 잘리면 "해냈다"가 안 남는다.
            // 🔴 연출이 멈춰도 판은 **반드시** 넘어간다. 연출 하나 때문에 못 깨면 안 된다.
            if (_won && Time.time > _wonAt + WinDelay
                && (_blobs.Count == 0 || Time.time > _wonAt + WinDelay + 2f))
            { ClearAndAdvance(); return; }

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

            // 🔴 받침대에 몸을 놓는다 — 스페이스. 받침대가 있는 판에서만 뜻이 있다.
            if (_L.Pads.Count > 0 &&
                (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.X)))
            { Step(SnakeEngine.Dir.Drop); return; }

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
            _steps++;
            var dd = SnakeEngine.Delta[(int)dir];
            if (dd.dx != 0 || dd.dy != 0) _lookDir = new Vector2(dd.dx, -dd.dy);
            _undo.Push(_st);
            int hadDm = _st.Dm;
            _st = ns;

            // 🔴 **어느 문이 방금 열렸나**를 Dm 이 바뀐 걸로 본다.
            //    WonDoor 로 물으면 안 된다 — 문이 채워지는 순간 엔진이 몸을 핵 하나로 줄여버려서
            //    "몸이 문에 맞나"는 이미 거짓이 되어 있다. (08-30: 판이 아예 안 깨지고 있었다)
            int opened = -1;
            for (int i = 0; i < _L.Doors.Count; i++)
                if ((hadDm & (1 << i)) == 0 && (_st.Dm & (1 << i)) != 0) { opened = i; break; }
            if (opened >= 0) StartSuck(opened);

            if (!_won && SnakeEngine.IsWin(_L, _st))
            {
                _wonBy = opened;
                _won = true; _wonAt = Time.time;
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
            _steps = Mathf.Max(0, _steps - 1);   // 되돌리면 걸음도 되돌린다
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
            // 🔴 **여기서 매 프레임 부른다.** SyncViews 는 걸음을 옮길 때만 불렸는데,
            //    문 열리기·별 돌기·빨려들기를 죄다 그 안에 넣어놔서 한 번 그리고 멈춰 있었다.
            //    (08-30: "아무 일도 안 일어나고" · 그 전엔 "이게 다 열린겨?")
            SyncViews();

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
                float s = Art.Scale(i == 0 ? "head" : "body", K.segmentSize);
                if (i == _st.Length - 1) s *= 1f + _pop;               // 새로 붙은 꼬리가 한 번 부푼다

                // 🔴 모양 실험(치마·그림자·돔)은 걷어냈다 — 비석·유령처럼 보였다.
                //    사장님이 나중에 직접 그리실 자리라, 여기서는 **둥근 몸 + 눈 + 이음매**까지만 둔다.
                _segs[i].transform.position = _segPos[i];
                // 착지 눌림은 세로로만 — 넓히면 한 칸을 넘는다
                _segs[i].transform.localScale = new Vector3(s, s * (1f - _land * 0.35f), 1);
                // 🔴 떨어지며 먹으면 다음 걸음에 길어진다 — 그 사이에 아무 표시가 없으면
                //    "먹었는데 사라졌다"로 보인다. 사람이 실제로 그렇게 헤맸다 (2026-08-29).
                //    꼬리를 조각 색으로 물들여 "여기서 길어진다"를 미리 보여준다.
                // 🔴 머리도 몸과 **같은 색**. 얼굴은 색이 아니라 눈이 알려준다 (09-02 사장님).
                //    머리만 밝으면 이어진 관이 아니라 마디가 나뉜 것으로 보인다.
                var col = BodyCol;
                if (_st.Pg > 0 && i == _st.Length - 1)
                {
                    float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
                    col = Color.Lerp(col, FoodCol, 0.45f + 0.35f * beat);
                }
                _segs[i].color = col;
            }

            // 🔴 마디 사이를 **잇는다.** 안 이으면 몸이 길어질 때 따로 떨어진 네모 여럿으로
            //    보인다 — 한 마리가 아니라 세 마리로 보였다 (08-31 사장님).
            //    이음매는 마디보다 얇게 해서 슬라임처럼 잘록하게 만든다.
            while (_links.Count < Mathf.Max(0, _st.Length - 1))
            {
                // 🔴 이음매를 몸(3·4)보다 **위**에 그린다.
                //    마디마다 옆면에 어두운 윤곽이 있어서 붙는 자리에 두 겹으로 곹쳐
                //    세로줄이 생겼다 — 이어진 관이 아니라 블록 여럿으로 보였다 (09-02).
                //    이음매가 그 자리를 가리면 윤곽은 관의 **바깥선에만** 남는다.
                var lk = NewSprite("Link", 5, "link");
                if (!Art.Has("link")) lk.sprite = PixelSprites.Round();
                _links.Add(lk);
            }
            for (int i = 0; i < _links.Count; i++)
            {
                bool on = i + 1 < _st.Length && i + 1 < _segPos.Count;
                _links[i].enabled = on;
                if (!on) continue;
                Vector2 a2 = _segPos[i], b2 = _segPos[i + 1];
                Vector2 mid = (a2 + b2) * 0.5f;
                Vector2 d2 = b2 - a2;
                float len = d2.magnitude;
                // 🔴 이음매는 몸통과 **같은 두께**여야 이어진 한 덩어리로 보인다.
                //    얇게 하면 구슬 목걸이가 된다 (08-31 사장님 그림: 굵기가 일정한 관).
                float thick = K.segmentSize * (1f - _land * 0.35f);
                bool horiz = Mathf.Abs(d2.x) > Mathf.Abs(d2.y);
                _links[i].transform.position = mid;
                if (Art.Has("link"))
                {
                    //  그림은 **가로 막대 한 장**만 있고 여기서 돌려 쓴다.
                    //  세로 폭은 그림이 이미 몸통과 맞추어 놓았다 — 눌러 찌그러뜨지 않는다.
                    _links[i].transform.rotation = Quaternion.Euler(0, 0, horiz ? 0f : 90f);
                    _links[i].transform.localScale =
                        new Vector3(len + 0.06f, 1f - _land * 0.30f, 1);
                }
                else
                {
                    _links[i].transform.rotation = Quaternion.identity;
                    _links[i].transform.localScale = horiz
                        ? new Vector3(len + 0.02f, thick, 1)
                        : new Vector3(thick, len + 0.02f, 1);
                }
                _links[i].color = Art.Tint("link", BodyCol);
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
            var big = new GUIStyle(GUI.skin.button) { fontSize = Px(20 * _uiScale) };
            var small = new GUIStyle(GUI.skin.button) { fontSize = Px(13 * _uiScale) };

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

        /// <summary>
        /// 🔴 게임을 아예 처음으로. 깬 판·모은 획·안내 화면 본 기록을 다 지운다.
        /// 손맛 값(K)은 **안 지운다** — 그건 게임 진행이 아니라 사장님이 맞춰둔 설정이다.
        /// </summary>
        void ResetAll()
        {
            PlayerPrefs.DeleteKey(ProgressKey);
            PlayerPrefs.DeleteKey(IntroKey);
            PlayerPrefs.Save();
            _recs.Clear();
            _trail.Clear();
            _allDone = false;
            _askReset = false;
            _steps = 0;
            Load(0);
            _menu = false;
            _intro = true;      // 안내 화면부터 다시
        }

        /// <summary>
        /// 🔴 깬 직후. 별이 하나씩 **크게 나타났다 줄어들며** 박힌다 — 땅! 땅! 땅!
        /// 못 받은 별도 같은 박자에 (빈 별로) 나와야 "하나 놓쳤다"가 보인다.
        /// </summary>
        void Result(float w, float h)
        {
            float el = Time.time - _resultAt;

            var big = new GUIStyle(_sBig) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, h * 0.5f - 128f * _uiScale, w, 36f * _uiScale), "CLEAR", big);

            // ---- 별 셋 ----
            float sz = Mathf.Clamp(64f * _uiScale, 48f, 96f), gap = sz * 0.34f;
            float total = 3 * sz + 2 * gap;
            float sy = h * 0.5f - sz * 0.5f - 18f * _uiScale;
            var star = new GUIStyle(_sBig)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Px(sz * 0.86f),
            };

            for (int k = 0; k < 3; k++)
            {
                float at = StarLead + k * StarBeat;
                if (el < at) continue;
                float u = Mathf.Clamp01((el - at) / StarPop);
                float pop = 1f + (1f - u) * (1f - u) * 1.7f;      // 크게 나왔다가 제자리로
                var r = new Rect(w * 0.5f - total * 0.5f + k * (sz + gap), sy, sz, sz);

                bool got = k < _resultStars;
                var col = got ? StarLit : new Color(1f, 1f, 1f, 0.20f);
                if (got) col = Color.Lerp(Color.white, StarLit, u);   // 박히는 순간 하얗게 번쩍
                star.normal.textColor = col;

                var m = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), r.center);
                GUI.Label(r, got ? "★" : "☆", star);
                GUI.matrix = m;
            }

            // ---- 걸음 수 ----
            if (el > StarLead + 2 * StarBeat)
            {
                string line = _resultSteps + "걸음";
                if (Def.cut > 0) line += "   커트 " + Def.cut;
                if (!_resultStar) line += "   ·  별을 놓쳤다";
                GUI.Label(new Rect(0, sy + sz + 14f * _uiScale, w, 24f * _uiScale), line, _sMid);

                if (_resultBest)
                    GUI.Label(new Rect(0, sy + sz + 40f * _uiScale, w, 22f * _uiScale),
                              "새 기록", _sSmall);
            }

            if (el > StarLead + 3 * StarBeat)
                GUI.Label(new Rect(0, h - 56f * _uiScale, w, 22f * _uiScale),
                          Touchy ? "화면을 누르면 다음" : "아무 키나 누르면 다음", _sSmall);
        }

        Texture2D _solid;
        Color _solidCol;
        /// 화면을 덮을 단색 한 장. 파일을 두지 않고 만들어 쓴다.
        /// OnGUI 는 한 프레임에 두 번 도니 색이 바뀔 때만 새로 굽는다.
        Texture2D Solid(Color c)
        {
            if (_solid == null)
            {
                _solid = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _solidCol = new Color(-1, -1, -1, -1);
            }
            if (_solidCol != c) { _solid.SetPixel(0, 0, c); _solid.Apply(); _solidCol = c; }
            return _solid;
        }

        /// <summary>
        /// 🔴 IMGUI(판 고르기 · 안내판)에도 도트를 쓴다.
        /// 그림이 없으면 예전처럼 색을 칠한다 — 한 장씩 갈아끼울 수 있어야 한다.
        /// 돌아오는 값은 "그림으로 그렸는가" — 부르는 쪽이 덤어질 것을 정한다.
        /// </summary>
        bool Tex(Rect r, string art, Color fallback)   // Solid() 가 인스턴스 것이라 static 못 씀
        {
            var s = Art.Get(art);
            if (s == null) { GUI.DrawTexture(r, Solid(fallback)); return false; }
            GUI.DrawTexture(r, s.texture);
            return true;
        }

        Vector2 _menuScroll;

        /// 마디에 찍을 이름. 판 이름 "1-6" 에서 뒤쪽만 뗀다 — 자리 번호가 아니다.
        /// 가르치는 판은 수가 아니라 표로 둔다. 세는 판이 아니니까.
        static string NodeLabel(SnakeLevelJson d, int i)
        {
            if (d == null) return (i + 1).ToString();
            if (d.tutorial) return "※";                       // ※ - 가르치는 판
            if (string.IsNullOrEmpty(d.id)) return (i + 1).ToString();
            int dash = d.id.LastIndexOf('-');
            return dash >= 0 && dash + 1 < d.id.Length ? d.id.Substring(dash + 1) : d.id;
        }

        /// <summary>
        /// 🔴 판 고르기. 별이 있을 자리이고, "내가 어디까지 왔나"가 보이는 유일한 화면이다.
        /// 진행은 여전히 한 줄 — 앞 판을 깨야 다음 판이 열린다. 깬 판은 다시 들어갈 수 있다.
        /// </summary>
        void Menu(float w, float h)
        {
            // 🔴 격자 버튼이 아니라 **길로 이어진 지도**다. 판이 늘어선 게 아니라
            //    한 굴을 따라 내려가는 것처럼 보여야 "다음 하나만 더"가 생긴다.
            GUI.DrawTexture(new Rect(0, 0, w, h), Solid(BgCol));

            int n = _set.levels.Length;
            float pad = 46f * _uiScale;
            float top = 78f * _uiScale, bot = 52f * _uiScale;
            int cols = Mathf.Clamp(Mathf.FloorToInt((w - pad * 2f) / (86f * _uiScale)), 3, 6);
            int rows = Mathf.CeilToInt(n / (float)cols);

            float node = Mathf.Clamp(Mathf.Min((w - pad * 2f) / cols, (h - top - bot) / rows) * 0.62f,
                                     34f, 76f);
            float gx = (w - pad * 2f) / cols, gy = (h - top - bot) / Mathf.Max(1, rows);

            // ---- 마디 자리: 뱀처럼 왼쪽→오른쪽→왼쪽으로 내려간다 ----
            var pos = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                int row = i / cols, col = i % cols;
                if (row % 2 == 1) col = cols - 1 - col;          // 지그재그
                // 손으로 놓은 것처럼 조금씩 흔든다 (판마다 늘 같은 값)
                float j1 = (Hash(i * 7 + 3) - 0.5f) * gx * 0.20f;
                float j2 = (Hash(i * 13 + 5) - 0.5f) * gy * 0.28f;
                pos[i] = new Vector2(pad + gx * (col + 0.5f) + j1,
                                     top + gy * (row + 0.5f) + j2);
            }

            // ---- 길 — 점을 뿌려 굽은 오솔길처럼 ----
            for (int i = 0; i + 1 < n; i++)
            {
                Vector2 a = pos[i], b = pos[i + 1];
                Vector2 mid = (a + b) * 0.5f;
                Vector2 perp = new Vector2(-(b - a).y, (b - a).x).normalized;
                float bow = (Hash(i * 31 + 11) - 0.5f) * Mathf.Min(70f, (b - a).magnitude * 0.45f);
                Vector2 ctrl = mid + perp * bow;

                bool walked = Unlocked(i + 1);
                var col = walked ? new Color(0.62f, 0.72f, 0.66f, 0.55f)
                                 : new Color(0.62f, 0.72f, 0.66f, 0.16f);
                int dots = Mathf.Clamp(Mathf.RoundToInt((b - a).magnitude / (7f * _uiScale)), 4, 40);
                float ds = Mathf.Max(2.5f, 3.4f * _uiScale);
                for (int k = 1; k < dots; k++)
                {
                    float u = k / (float)dots;
                    Vector2 p = (1 - u) * (1 - u) * a + 2 * (1 - u) * u * ctrl + u * u * b;
                    GUI.color = col;
                    GUI.DrawTexture(new Rect(p.x - ds * 0.5f, p.y - ds * 0.5f, ds, ds), Solid(Color.white));
                }
                GUI.color = Color.white;
            }

            // ---- 마디 ----
            var numS = new GUIStyle(_sMid)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Px(Mathf.Clamp(node * 0.36f, 11f, 26f)),
            };
            var starS = new GUIStyle(_sSmall)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Px(Mathf.Clamp(node * 0.26f, 11f, 18f)),
            };

            int hover = -1;
            for (int i = 0; i < n; i++)
            {
                var d = _set.levels[i];
                bool open = Unlocked(i);
                int s = Stars(d);
                var r = new Rect(pos[i].x - node * 0.5f, pos[i].y - node * 0.5f, node, node);

                // 굴 입구처럼 — 어두운 돌에 위쪽만 밝다
                GUI.color = open ? Color.white : new Color(1, 1, 1, 0.55f);
                //  그림에는 윗면이 이미 들어 있다 — 따로 덮지 않는다
                if (!Tex(r, open ? "node" : "node_lock", open ? NodeStone : NodeLock))
                    GUI.DrawTexture(new Rect(r.x, r.y, r.width, Mathf.Max(2f, node * 0.13f)),
                                    Solid(open ? NodeTop : NodeLock));
                GUI.color = Color.white;

                // 🔴 마디에 **판 이름**을 찍는다. 자리 번호를 찍고 있어서
                //    1-6 판이 "7"로, 튜토리얼이 "1"로 불리고 있었다 — 판 전부가 한 칸씩 어긋났다.
                //    힌트나 공략을 주고받을 때 서로 다른 판을 가리키게 된다.
                numS.normal.textColor = open ? new Color(1, 1, 1, 0.92f) : new Color(1, 1, 1, 0.4f);
                GUI.Label(new Rect(r.x, r.y + node * 0.10f, r.width, node * 0.5f), NodeLabel(d, i), numS);

                if (open)
                {
                    string ss = "";
                    for (int k = 0; k < 3; k++) ss += k < s ? "★" : "☆";
                    starS.normal.textColor = s > 0 ? StarLit : new Color(1, 1, 1, 0.24f);
                    GUI.Label(new Rect(r.x, r.yMax - node * 0.42f, r.width, node * 0.34f), ss, starS);
                }

                if (r.Contains(Event.current.mousePosition)) hover = i;
                if (GUI.Button(r, GUIContent.none, GUIStyle.none) && open)
                { _menu = false; _allDone = false; Load(i); return; }
            }

            // ---- 지금 서 있는 자리 — 다음에 할 판 위에 슬라임 ----
            int at = 0;
            while (at + 1 < n && Cleared(_set.levels[at].id)) at++;
            float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            float mk = node * 0.30f;
            GUI.color = new Color(1, 1, 1, 0.75f + 0.25f * beat);
            var here = new Rect(pos[at].x - mk * 0.5f,
                                pos[at].y - node * 0.78f - beat * 3f, mk, mk);
            if (Art.Get("head") == null)
                here.height = mk * 0.7f;
            Tex(here, "head", HeadCol);
            GUI.color = Color.white;

            // ---- 머리글 ----
            GUI.Label(new Rect(0, 18f * _uiScale, w, 34f * _uiScale),
                      "제 " + Mathf.Max(1, _set.chapter) + " 굴", _sBig);
            int got = 0;
            foreach (var l in _set.levels) got += Stars(l);
            GUI.Label(new Rect(0, 52f * _uiScale, w, 22f * _uiScale),
                      "★ " + got + " / " + (n * 3), _sMid);

            // ---- 가리키는 판 설명 ----
            if (hover >= 0 && Unlocked(hover))
            {
                var d = _set.levels[hover];
                string line = d.id;
                if (_recs.TryGetValue(d.id, out int rec))
                {
                    bool gs = rec < StarBit;
                    line += (line.Length > 0 ? "   ·   " : "") + (gs ? rec - 0 : rec - StarBit) + "걸음";
                }
                else if (d.cut > 0) line += (line.Length > 0 ? "   ·   " : "") + "커트 " + d.cut + "걸음";
                GUI.Label(new Rect(0, h - bot + 4f, w, 22f * _uiScale), line, _sMid);
            }
            else
            {
                GUI.Label(new Rect(0, h - bot + 4f, w, 22f * _uiScale),
                          "판을 눌러 시작 · 게임 중 Esc 로 여기로", _sSmall);
            }

            var bs = new GUIStyle(GUI.skin.button) { fontSize = Px(13 * _uiScale) };
            if (GUI.Button(new Rect(12f, h - 34f * _uiScale, 84f * _uiScale, 26f * _uiScale), "설명", bs))
            { _intro = true; _askReset = false; }
        }

        /// <summary>
        /// 🔴 가르치는 판에서만 — **홈에 어떻게 들어가는지**를 되풀이해 보여준다.
        /// 글로 "몸을 홈에 정확히 포개세요"라고 써봐야 안 읽힌다. 움직이는 걸 봐야 안다.
        /// (08-31 사장님: "홈에 어떤식으로 들어가야 되는지 움짤 같은 걸로")
        ///
        /// 네 마디로 되풀이한다 — 다가감 → 딱 맞음 → 빨려듦 → 핵만 남음.
        /// </summary>
        // ---- 가르치는 판의 오른쪽 안내판 ----
        // 🔴 시안 A (08-31 사장님). 판을 왼쪽으로 몰고 오른쪽에 안내판을 **세운다**.
        //    카메라 자체를 왼쪽 72%만 쓰게 해서 **겹칠 수가 없게** 만든다 —
        //    지난번엔 판 위에 겹쳐 그려서 안내가 판 밑동에 잘려 나갔다.
        //    여섯 장이 저절로 넘어가며 키 넷과 홈 넣는 법을 전부 보여준다.
        const float GuideW = 0.28f;      // 안내판이 차지하는 가로 비율
        const float SlideSecs = 3.6f;    // 한 장이 머무는 시간
        const int Slides = 6;
        int _slide;               // 지금 보고 있는 장
        float _slideAt = -99f;    // 그 장이 뜬 시각

        /// 안내판 한 칸을 그린다 (작은 격자용)
        void Cell(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, Solid(Color.white)); GUI.color = Color.white; }

        /// <summary>
        /// 🔴 여섯 장을 돌려 보여준다 — → ← ↓ ↑ · 조각 · 홈.
        /// 글로만 쓰면 안 읽힌다. 작더라도 **움직이는 걸** 봐야 안다.
        /// </summary>
        void Guide(float w, float h)
        {
            float pw = w * GuideW;
            var box = new Rect(w - pw, 0, pw, h);
            GUI.DrawTexture(box, Solid(PanelBg));
            Cell(new Rect(box.x, 0, 1.5f, h), new Color(1, 1, 1, 0.10f));   // 왼쪽 경계선

            float pad = pw * 0.09f;

            // 🔴 저절로 넘어가되, 손으로도 넘길 수 있다. 손으로 넘기면 그 장부터 다시 잰다.
            if (_slideAt < 0f) _slideAt = Time.time;
            if (Time.time - _slideAt > SlideSecs) { _slide = (_slide + 1) % Slides; _slideAt = Time.time; }
            int slide = _slide;
            float u = Mathf.Clamp01((Time.time - _slideAt) / SlideSecs);

            // ---- 제목 ----
            var ts = new GUIStyle(_sMid) { alignment = TextAnchor.UpperCenter, wordWrap = true,
                                           fontSize = Px(Mathf.Clamp(pw * 0.075f, 11f, 22f)) };
            ts.normal.textColor = new Color(1, 1, 1, 0.92f);
            GUI.Label(new Rect(box.x + pad, h * 0.06f, pw - pad * 2, h * 0.10f), "튜토리얼", ts);

            // ---- 무대 ----
            // 🔴 무대는 **격자에 딱 맞춰야** 한다. 상자만 크고 칸이 작으면
            //    격자로 안 읽히고 그냥 빈 검은 상자로 보인다 (08-31 화면).
            float c = Mathf.Floor(Mathf.Min((pw - pad * 2f) / 5f, h * 0.30f / 3f));
            var stage = new Rect(box.x + (pw - c * 5f) * 0.5f, h * 0.20f, c * 5f, c * 3f);
            GUI.DrawTexture(new Rect(stage.x - 4, stage.y - 4, stage.width + 8, stage.height + 8),
                            Solid(new Color(1, 1, 1, 0.07f)));
            GUI.DrawTexture(stage, Solid(StageBg));

            float gx = stage.x, gy = stage.y;
            Rect at(int cx, int cy) => new Rect(gx + cx * c + 1f, gy + cy * c + 1f, c - 2f, c - 2f);

            // 바닥 한 줄은 늘 돌
            for (int k = 0; k < 5; k++) Cell(at(k, 2), Rock);

            float e = Mathf.Clamp01((u - 0.15f) / 0.55f);      // 움직이는 구간
            switch (slide)
            {
                case 0: {                                       // →
                    float x = Mathf.Lerp(0.6f, 3.4f, e);
                    Cell(new Rect(gx + x * c + 1f, gy + c + 1f, c - 2f, c - 2f), BodyCol);
                    break;
                }
                case 1: {                                       // ←
                    float x = Mathf.Lerp(3.4f, 0.6f, e);
                    Cell(new Rect(gx + x * c + 1f, gy + c + 1f, c - 2f, c - 2f), BodyCol);
                    break;
                }
                case 2: {                                       // ↓ 떨어진다
                    Cell(at(0, 1), Rock); Cell(at(1, 1), Rock);
                    float y = Mathf.Lerp(0f, 1f, e * e);
                    Cell(new Rect(gx + 2.6f * c + 1f, gy + y * c + 1f, c - 2f, c - 2f), BodyCol);
                    break;
                }
                case 3: {                                       // ↑ 몸이 길어야 오른다
                    Cell(at(3, 1), Rock); Cell(at(4, 1), Rock);
                    float x = Mathf.Lerp(0.6f, 2.4f, Mathf.Clamp01(e * 1.6f));
                    float up = Mathf.Clamp01((e - 0.62f) / 0.38f);
                    Cell(new Rect(gx + (x + up) * c + 1f, gy + (1f - up) * c + 1f, c - 2f, c - 2f), BodyCol);
                    Cell(new Rect(gx + x * c + 1f, gy + c + 1f, c - 2f, c - 2f), BodyCol);
                    break;
                }
                case 4: {                                       // 조각을 먹으면 길어진다
                    bool ate = e > 0.55f;
                    if (!ate) Cell(at(3, 1), FoodCol);
                    float x = Mathf.Lerp(0.6f, 3f, Mathf.Clamp01(e / 0.55f));
                    Cell(new Rect(gx + x * c + 1f, gy + c + 1f, c - 2f, c - 2f), BodyCol);
                    if (ate) Cell(new Rect(gx + (x - 1f) * c + 1f, gy + c + 1f, c - 2f, c - 2f), BodyCol);
                    break;
                }
                default: {                                      // 홈에 몸을 포갠다
                    for (int k = 0; k < 3; k++)
                    {
                        Cell(at(2 + k, 1), HoleEdge);
                        var q = at(2 + k, 1);
                        Cell(new Rect(q.x + 2, q.y + 2, q.width - 4, q.height - 4), HoleCol);
                    }
                    var core = at(4, 1);
                    Cell(new Rect(core.x + c * 0.3f, core.y + c * 0.3f, c * 0.4f, c * 0.4f), CoreCol);

                    float slid = Mathf.Clamp01(u / 0.45f);
                    float suck = Mathf.Clamp01((u - 0.62f) / 0.22f);
                    float hx = Mathf.Lerp(-0.6f, 4f, 1f - (1f - slid) * (1f - slid));
                    for (int k = 0; k < 3; k++)
                    {
                        if (suck >= 1f && k > 0) continue;
                        float bx = hx - k;
                        float s = 1f;
                        if (suck > 0f && k > 0) { float v = Mathf.Clamp01(suck * 1.4f - k * 0.2f); bx = Mathf.Lerp(bx, 4f, v); s = 1f - v * 0.8f; }
                        float p = c * (1f - s) * 0.5f;
                        Cell(new Rect(gx + bx * c + p + 1f, gy + c + p + 1f, (c - 2f) * s, (c - 2f) * s),
                             Color.Lerp(BodyCol, SpentTop, suck));
                    }
                    break;
                }
            }

            // ---- 한 줄 설명 ----
            string[] LINE = {
                "→  오른쪽 화살표 키",
                "←  왼쪽 화살표 키",
                "↓  아래로 — 떨어지면 못 돌아옵니다",
                "↑  위로 — 몸이 길어야 오릅니다",
                "주황 조각을 먹으면 몸이 한 칸 길어집니다",
                "몸을 홈에 정확히 포개세요\n머리가 노란 칸에서 끝나야 합니다",
            };
            var ls = new GUIStyle(_sSmall) { alignment = TextAnchor.UpperCenter, wordWrap = true,
                                             fontSize = Px(Mathf.Clamp(pw * 0.062f, 11f, 17f)) };
            ls.normal.textColor = new Color(1, 1, 1, 0.80f);
            GUI.Label(new Rect(box.x + pad, stage.yMax + h * 0.035f, pw - pad * 2, h * 0.22f),
                      LINE[slide], ls);

            // ---- 몇 번째 장인지 ----
            float dot = Mathf.Clamp(pw * 0.022f, 4f, 9f), gap = dot * 1.9f;
            float row = h * 0.60f;
            float span = Slides * gap - (gap - dot);
            float dx = box.x + pw * 0.5f - span * 0.5f;
            for (int k = 0; k < Slides; k++)
            {
                var dr = new Rect(dx + k * gap, row, dot, dot);
                Cell(dr, k == slide ? StarLit : new Color(1, 1, 1, 0.18f));
                // 점을 눌러 그 장으로 바로
                if (GUI.Button(new Rect(dr.x - 4, dr.y - 8, dr.width + 8, dr.height + 16),
                               GUIContent.none, GUIStyle.none))
                { _slide = k; _slideAt = Time.time; }
            }

            // 🔴 손으로 넘기는 단추 — 기다리기 싫은 사람이 있다
            var ns = new GUIStyle(_sMid)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Px(Mathf.Clamp(pw * 0.09f, 16f, 28f)),
            };
            ns.normal.textColor = new Color(1, 1, 1, 0.55f);
            ns.hover.textColor = StarLit;
            float bw = Mathf.Clamp(pw * 0.14f, 26f, 48f);
            var prev = new Rect(dx - span * 0.16f - bw, row - bw * 0.42f, bw, bw * 0.9f);
            var next = new Rect(dx + span + span * 0.16f, row - bw * 0.42f, bw, bw * 0.9f);
            if (GUI.Button(prev, "‹", ns)) { _slide = (_slide + Slides - 1) % Slides; _slideAt = Time.time; }
            if (GUI.Button(next, "›", ns)) { _slide = (_slide + 1) % Slides; _slideAt = Time.time; }

            // ---- 지금 해야 할 것 (상태를 보고) ----
            string coach = Coach();
            if (coach != null)
            {
                var cs = new GUIStyle(_sMid) { alignment = TextAnchor.LowerCenter, wordWrap = true,
                                               fontSize = Px(Mathf.Clamp(pw * 0.068f, 11f, 19f)) };
                cs.normal.textColor = StarLit;
                GUI.Label(new Rect(box.x + pad, h * 0.66f, pw - pad * 2, h * 0.24f), coach, cs);
            }

            var ks = new GUIStyle(_sSmall) { alignment = TextAnchor.LowerCenter, wordWrap = true };
            ks.normal.textColor = new Color(1, 1, 1, 0.40f);
            GUI.Label(new Rect(box.x + pad, h * 0.90f, pw - pad * 2, h * 0.08f),
                      Touchy ? "화면을 밀어서 움직입니다" : "← ↑ ↓ →     Z 되돌리기", ks);
        }

        /// 판 번호로 늘 같은 흔들림을 만든다 — 손으로 놓은 듯하되 켤 때마다 안 바뀌게
        static float Hash(int i)
        {
            unchecked
            {
                int x = i * 374761393 + 668265263;
                x = (x ^ (x >> 13)) * 1274126177;
                return ((x ^ (x >> 16)) & 0xffff) / 65535f;
            }
        }

        /// <summary>
        /// 🔴 처음 하는 사람에게 **다음 한 가지만** 말해준다. 답은 안 알려준다 —
        /// "무엇을 해야 하는가"만 말하고 "어디로 가야 하는가"는 말하지 않는다.
        /// 세 판을 깨고 나면 사라진다. 계속 떠 있으면 잔소리가 된다.
        /// </summary>
        string Coach()
        {
            if (_won) return null;

            // 🔴 가르치는 판에서는 **키부터** 하나씩. 여기선 절대 안 숨긴다.
            //    (08-30 동생 둘 다 "처음에 뭘 하라는 건지 모르겠다")
            if (Def.tutorial)
            {
                // 🔴 맵에 박아둔 화살표를 따라가게 한다. 글은 거들 뿐이다.
                if (_steps == 0)
                    return Touchy ? "화면을 밀어 움직여 보세요"
                                  : "화살표 키로 움직여 보세요";
                int ate = 0;
                for (int i = 0; i < _L.Foods.Count; i++) if (SnakeEngine.IsEaten(_st, i)) ate++;
                if (ate == 0) return "주황 조각을 먹으면 몸이 길어집니다";
                if (_st.Length < _L.Doors[0].Cells.Count)
                    return "몸이 " + _st.Length + "칸 · 홈은 " + _L.Doors[0].Cells.Count
                           + "칸입니다 — 조각을 더 먹으세요";
                bool onDoor = true;
                foreach (int c in _st.Body) if (!_L.Doors[0].Set.Contains(c)) { onDoor = false; break; }
                if (!onDoor) return "밝은 홈에 몸을 정확히 포개세요";
                if (_L.Doors[0].Core >= 0 && _st.Head != _L.Doors[0].Core)
                    return "거의 다 됐습니다 — 머리가 노란 칸에서 끝나야 합니다";
                return null;
            }

            if (_recs.Count >= 3 && Cleared(Def.id)) return null;   // 익숙해지면 그만
            if (_recs.Count >= 3 && _index >= 3) return null;

            // 지금 채워야 할 홈
            int di = -1;
            for (int i = 0; i < _L.Doors.Count; i++)
                if ((_st.Dm & (1 << i)) == 0) { di = i; break; }
            if (di < 0) return null;
            var door = _L.Doors[di];

            int have = _st.Length, want = door.Cells.Count;
            int inside = 0;
            foreach (int c in _st.Body) if (door.Set.Contains(c)) inside++;

            if (have < want)
                return "주황 조각을 먹어 몸을 " + want + "칸으로 — 지금 " + have + "칸";
            if (have > want)
                return "몸이 " + have + "칸인데 홈은 " + want + "칸 · 너무 먹었다 (Z 로 되돌리기)";
            if (inside < want)
                return "밝은 홈 위에 몸을 정확히 포개세요 (" + inside + "/" + want + "칸)";
            if (door.Core >= 0 && _st.Head != door.Core)
                return "다 맞았다 — 머리가 노란 칸에서 끝나야 한다";
            return null;
        }

        // ---- 얼굴 ----
        // 🔴 눈은 미러 뻐다 (09-02 사장님). 얼굴에 있는 건 **열쇠 하나**다.
        //    눈이 있으면 눈길이 눈으로 가는데, 사람이 실제로 봐야 하는 건
        //    "내 열쇠가 어디 있나"이다. 캐릭터다움보다 **읽히는 것**을 골랐다.
        SpriteRenderer _keyGlow, _keyRing, _keyCore;   // 🔴 머리에 박힌 열쇠

        void FaceUp()
        {
            if (_segs.Count == 0) return;
            var head = _segs[0];
            if (!head.enabled) { SetEyes(false); return; }
            SetEyes(true);

            Vector2 look = _lookDir;                  // 마지막으로 간 쪽 — 열쇠가 그쪽으로 조금 쓸린다
            Vector3 hp = head.transform.position;
            float s = Mathf.Min(head.transform.localScale.x, head.transform.localScale.y);
            KeyUp(hp, look, s);
        }

        void SetEyes(bool on)
        {
            if (_keyRing == null) return;
            _keyGlow.enabled = _keyRing.enabled = _keyCore.enabled = on;
        }

        /// <summary>
        /// 🔴 머리에 박힌 **열쇠**. (09-02 사장님)
        ///
        /// 이게 규칙 하나를 설명 없이 만든다 — "머리가 심에서 끝나야 한다"는
        /// 지금까지 **외워야 하는 조건**이었다. 머리에 열쇠가 보이면
        /// 그냥 *열쇠를 열쇠 구멍에 맞추는 일*이 된다. 튜토리얼 한 장이 줄어든다.
        /// 세계관 문장도 원래 이거였다 — **"몸이 곧 열쇠"** (brief §86).
        ///
        /// 열쇠 구멍 위에 서면 열쇠가 밝아지며 뛴다. 맞았다는 걸 손에 쥐여준다.
        /// </summary>
        void KeyUp(Vector3 hp, Vector2 look, float s)
        {
            if (_keyRing == null)
            {
                _keyGlow = NewSprite("KeyGlow", 6, "key_glow");   // 뒤에 번지는 빛
                _keyRing = NewSprite("KeyRing", 7);      // 마름모 테
                _keyCore = NewSprite("KeyCore", 8, "key");      // 마름모 속 — 제일 밝다
                _keyGlow.sprite = PixelSprites.Disc();
                _keyRing.sprite = PixelSprites.Diamond();
                _keyCore.sprite = PixelSprites.Diamond();
            }
            _keyGlow.enabled = _keyRing.enabled = _keyCore.enabled = true;

            //  머리가 열쇠 구멍 위인가
            bool fit = false;
            if (_L != null && _L.Doors != null)
                for (int i = 0; i < _L.Doors.Count && !fit; i++)
                    if (_L.Doors[i].Core >= 0 && _st.Head == _L.Doors[i].Core) fit = true;
            //  🔴 **번짝번짝** (09-02 사장님). 두 박자를 겹친다 —
            //     느린 숨(늘 살아 있다) + 짧게 터지는 섬광(눈길을 끔다).
            //     섬광은 exp 로 떨어뜨린다. sin 으로 하면 번쎍이 아니라 출렁임이 된다.
            float breathe = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.3f);
            float spark   = Mathf.Exp(-Mathf.Repeat(Time.time, KeyBlink) * 7f);
            //  열쇠 구멍 위에 서면 쉬지 않고 뛴다 = 맞았다
            float lit = fit ? 0.55f + 0.45f * Mathf.Sin(Time.time * 9f)
                            : Mathf.Max(spark, 0.22f * breathe);

            //  이마에, 가는 쪽으로 조금 — 얼굴에 붙어 있어야 "이게 내 열쇠"로 읽힌다
            var at = hp + new Vector3(look.x * 0.07f * s, 0.26f * s + look.y * 0.03f * s, 0f);
            //  🔴 위아래로 긴 마름모. 가로세로를 따로 준다
            float kw = 0.30f * s * (1f + 0.06f * lit);
            float kh = 0.52f * s * (1f + 0.10f * lit);

            _keyGlow.transform.position = at;
            _keyGlow.transform.localScale = new Vector3(kh * 1.9f, kh * 1.9f, 1f);
            var gc = CoreCol; gc.a = 0.10f + 0.26f * lit;
            _keyGlow.color = gc;

            _keyRing.transform.position = at;
            _keyRing.transform.localScale = new Vector3(kw, kh, 1f);
            _keyRing.color = Color.Lerp(CoreRing, CoreCol, 0.35f + 0.65f * lit);

            _keyCore.transform.position = at;
            _keyCore.transform.localScale = new Vector3(kw * 0.52f, kh * 0.52f, 1f);
            _keyCore.color = Color.Lerp(CoreCol, Color.white, 0.75f * lit);
        }

        void CloseIntro()
        {
            _intro = false;
            _menu = true;              // 🔴 안내 다음은 판 고르기다
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
            // 🔴 초기화 버튼은 **안내 화면에만** 둔다.
            //    게임 중에 두면 실수로 눌러 다 날린다. 여기선 ? 버튼으로만 들어온다.
            var rs = new GUIStyle(GUI.skin.button) { fontSize = Px(13 * _uiScale) };
            float rw = 150 * _uiScale, rh = 30 * _uiScale;
            var rr = new Rect(w * 0.5f - rw * 0.5f, h - s * 3.6f, rw, rh);
            if (_askReset)
            {
                if (GUI.Button(rr, "정말 처음부터? (누르면 지워짐)", rs)) { ResetAll(); return; }
            }
            else if (GUI.Button(rr, "처음부터 다시", rs)) { _askReset = true; return; }

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
                // 버튼을 눌렀으면 GUI.Button이 이미 먹었다. 그 밖을 눌렀을 때만 닫는다
                if (Event.current.type == EventType.MouseDown && !_askReset) CloseIntro();
                return;
            }

            if (_result) { Result(w, h); return; }
            if (_menu) { Menu(w, h); return; }

            // ---- 다 깼으면 결과만 보여준다 ----
            //    🔴 WebGL은 파일을 못 쓴다. 이 화면이 기록을 돌려받는 유일한 통로다.
            if (_allDone)
            {
                GUI.Label(new Rect(0, 40, w, 30), "다 깼습니다. 고맙습니다!", _sBig);
                GUI.Label(new Rect(0, 74, w, 24),
                    "이 화면을 찍어서 보내주세요", _sMid);
                // 🔴 표는 줄이 밀리면 안 된다. 화면 폭에 맞춰 상자를 잡고 글씨를 줄인다.
                //    (08-30: 좁은 화면에서 머리글이 세 줄로 접혀 판을 덮었다)
                float bw = Mathf.Min(w - 32f, 620f);
                float bh = Mathf.Min(h - 190f, 74f + 20f * (SnakeLog.Runs.Count + 2));
                var box = new Rect(w * 0.5f - bw * 0.5f, 104f, bw, bh);
                GUI.Box(box, GUIContent.none);
                var tbl = new GUIStyle(_sSmall)
                {
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = false,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(bw / 36f), 9, 15),
                };
                tbl.normal.textColor = new Color(1f, 1f, 1f, 0.78f);
                GUI.Label(new Rect(box.x + 12, box.y + 8, box.width - 24, box.height - 44),
                          SnakeLog.Table(), tbl);
                GUI.Label(new Rect(box.x + 12, box.yMax - 32, box.width - 24, 24),
                          SnakeLog.Summary(), tbl);
                GUI.Label(new Rect(0, h - 56, w, 22),
                    "재미있었는지 · 어디서 막혔는지 · 그만두고 싶었는지 한 줄만 적어주시면 큰 도움이 됩니다", _sMid);
                GUI.Label(new Rect(0, h - 32, w, 20), "R  처음부터 다시", _sSmall);
                return;
            }

            // 🔴 가르치는 판은 **오른쪽 안내판이 전부**다. 판 위에는 아무것도 안 얹는다.
            //    제목·남은 홈·걸음·커트가 네 줄로 쌓여 어디를 볼지 몰랐다 (08-31).
            if (def.tutorial) { Guide(w, h); return; }

            // ---- 플레이어가 보는 것 : 번호 · 남은 홈 · 조작. 그게 전부다 ----
            // 🔴 판 이름은 없앴다. 생성기가 붙인 이름은 열네 판이 전부 "홈 하나"라
            //    되풀이되는 이름은 없느니만 못했다 (08-31 사장님: "너무 다 짜쳐서").
            GUI.Label(new Rect(0, 12, w, 28), def.id, _sBig);

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

            // 🔴 걸음 수와 커트라인은 늘 보여야 한다. 다 끝나고 알려주면 늦다.
            if (_L.Star >= 0 || def.cut > 0)
            {
                string s = (_st.Sc != 0 ? "★ " : "☆ ") + _steps + "걸음";
                if (def.cut > 0) s += "  ·  커트 " + def.cut;
                GUI.Label(new Rect(0, 66, w, 20), s, _sSmall);
            }

            // 🔴 지도 대신 **지나온 길**을 보여준다. 어디서 갈렸는지가 남는다.
            if (_dev && _trail.Count > 1)
                GUI.Label(new Rect(0, 88, w, 20),
                    string.Join(" › ", _trail.GetRange(Mathf.Max(0, _trail.Count - 6), Mathf.Min(6, _trail.Count))),
                    _sSmall);

            if (_won)
            {
                GUI.Label(new Rect(0, h - 40, w, 24), "문이 열렸다", _sMid);
            }
            else
            {
                // 🔴 오래 막혔을 때만 도움을 내민다. 처음부터 떠 있으면 아무도 안 푼다.
                if (Stuck)
                {
                    bool dead = _lostSet.IsLost(_st);
                    var hs = new GUIStyle(GUI.skin.button) { fontSize = Px(13 * _uiScale) };
                    float hw = 210f * _uiScale, hh = 30f * _uiScale;
                    var hr = new Rect(w - hw - 12f, h - hh - 12f, hw, hh);
                    // 🔴 단계마다 다른 도움. 필요한 만큼만 열어보게 한다.
                    int nx = NextHint();
                    string label = dead ? "되돌리세요 (Z)"
                        : nx == 1 ? "어느 홈부터? (H)"
                        : nx == 2 ? "먹으면 안 되는 조각은? (H)"
                        : nx == 3 ? "다음 한 걸음 (H) — 별 셋 포기"
                        : null;
                    if (label != null && GUI.Button(hr, label, hs) && !dead) OpenHint();

                    // 민 방향을 잠깐 크게 보여준다 — 답이 아니라 **다음 한 수**다
                    if (Time.time - _nudgeShow < 2.2f)
                    {
                        string face = _lostSet.IsLost(_st) ? "여긴 이미 진 자리다 — 되돌리세요"
                            : _nudgeDir == SnakeEngine.Dir.Drop ? "여기서 몸을 놓으세요"
                            : "이쪽으로   " + "↑↓←→"[(int)_nudgeDir];
                        var ns2 = new GUIStyle(_sBig) { fontSize = Mathf.RoundToInt(26 * _uiScale) };
                        ns2.normal.textColor = StarLit;
                        GUI.Label(new Rect(0, h * 0.5f - 24f, w, 44f), face, ns2);
                    }
                    string note = _tooBig ? "이 판은 너무 커서 다음 걸음은 못 알려드립니다"
                                : _hint == 1 ? "먼저 채울 홈이 깜빡입니다"
                                : _hint == 2 ? "잿빛 조각은 먹으면 안 됩니다"
                                : _nudges > 0 ? "다음 걸음을 " + _nudges + "번 봤습니다 — 이 판은 별 셋을 못 받습니다"
                                : null;
                    if (note != null) GUI.Label(new Rect(0, h - 62, w, 20), note, _sSmall);
                }

                if (!Touchy)
                    GUI.Label(new Rect(0, h - 34, w, 22),
                        _L.Pads.Count > 0
                          ? "← ↑ ↓ →      Space  몸 놓기      Z  되돌리기      R  처음부터"
                          : "← ↑ ↓ →      Z  되돌리기      R  처음부터", _sSmall);

                // 🔴 다시 볼 길을 남긴다. 한 번 보고 잊으면 물어볼 데가 없다.
                var qs = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(15 * _uiScale) };
                float qb = Mathf.Clamp(28f * _uiScale, 28f, 56f);
                if (GUI.Button(new Rect(w - qb - 10, 10, qb, qb), "?", qs)) { _intro = true; _askReset = false; }

                // 🔴 안내는 필요한 순간에만 뜬다. 늘 떠 있으면 아무도 안 읽는다.
                //    (08-30 동생 둘 다: "처음에 뭘 하라는 건지 모르겠다" — 가만히 있는
                //     설명은 안 읽힌다. **지금 상태를 보고** 다음 한 가지만 말해준다)
                string coach = Coach();
                if (coach != null)
                {
                    // 가르치는 판에서는 크게 — 구석의 작은 글씨는 안 읽힌다
                    var cs = Def.tutorial ? _sBig : _sMid;
                    GUI.Label(new Rect(0, Def.tutorial ? 96f * _uiScale : h - 62, w, 30), coach, cs);
                }
                // 🔴 가르치는 판에서는 "홈에 어떻게 들어가는가"를 계속 보여준다

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
                            (Cleared(def.id) ? "  (깬 판)" : "") +
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
