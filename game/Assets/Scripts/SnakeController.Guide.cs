using System.Collections.Generic;
using UnityEngine;

namespace SlimeEscape
{
    //  🔴 SnakeController 의 한 조각 (partial). 필드 · Awake · 판 진행은 SnakeController.cs.
    //     가르치는 판의 안내판 · 시범 게임 · Coach
    //     2026-09-12 에 4,400줄짜리 한 파일을 구획대로 나눴다 — 내용은 한 글자도 안 바꿨다.
    public partial class SnakeController
    {
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
        //  🔴 세로 폰(h > w)에서는 안내판을 **아래 띠**로 내린다 (09-12, todo 모바일 손질).
        //     오른쪽 28% 기둥이면 390px 폭에서 글이 깨알같이 부서지고, 판은 작게 떠서
        //     위아래가 텅 비었다 — 390×844 로 실제로 찍어서 봤다.
        const float GuideH = 0.40f;      // 세로일 때 안내판이 차지하는 세로 비율
        static bool Tall(float w, float h) => h > w * 1.05f;

        // ================================================================ 시범 (안내판)
        /// <summary>
        /// 🔴 **안내판은 흉내가 아니라 진짜 게임이다** (09-03 사장님:
        /// "진짜 내 캐릭터랑 같은 거가 저기서 방향키 누르는 방식 + 진짜 우리가 쓰는 벽").
        ///
        /// 같은 판을 IMGUI 로 다시 그리다가 오늘만 두 번 어긋났다 —
        /// 게임을 둥글게 바꿔놓고 안내판만 네모로, 심을 희게 바꿔놓고 글은 "노란 칸"으로.
        /// 그래서 <see cref="SnakeController"/> 를 **하나 더** 띄워 시범을 돌린다.
        /// 그리는 코드가 같으니 게임이 바뀌면 안내판도 저절로 따라온다.
        ///
        /// 판 밖 세상(진행 저장·소리·목록·입력)은 전부 막는다. 그리는 것만 쓴다.
        /// </summary>
        bool _demo;                       // 나는 시범용인가
        SnakeController _guide;           // 안내판에서 도는 시범 (본판만 가진다)
        bool _guideBlew;                  // 안내판이 터진 적이 있나 (한 번만 남긴다)
        Camera _guideCam;
        int _guideSlide = -1;             // 시범이 지금 보여주는 장
        string _demoKeys = "";            // 눌러줄 방향들
        int _demoAt;                      // 지금 몇 번째
        float _demoNext;                  // 다음 걸음 시각
        /// <summary>
        /// 🔴 **몰아붙이기.** 타이틀에서 게임으로 넘어갈 때, 남은 걸음을
        /// 빠르게 밟아 홈까지 들어가게 한다 — 그 빨려듦이 곧 "시작" 연출이다.
        /// 다 하면 **되돌아가지 않는다.** 빨려드는 걸 끝까지 보여줘야 하니까.
        /// </summary>
        bool _demoRush;
        const float DemoRushStep = 0.11f;
        public void DemoRush() { _demoRush = true; _demoNext = 0f; }
        /// 시범이 처음부터 끝까지 몇 번 돌았나. 본판이 이걸 보고 장을 넘긴다.
        public int DemoLoops { get; private set; }
        Vector2 _org;                     // 판을 놓을 자리 (시범은 멀리 떨어뜨린다)

        const float DemoStep = 0.70f;     // 한 걸음 사이
        const float DemoHold = 1.30f;     // 다 하고 처음으로 돌아가기 전 쉼

        /// <summary>
        /// 시범 판 여섯. 🔴 **진짜 판 글자**를 쓴다 (SnakeEngine.Parse) —
        /// # 벽 · . 빈칸 · S 시작 · + 조각 · = 채울 칸 · * 심.
        /// 중력이 걸려 있으니 떨어지고, 막히면 안 간다. 흉내가 아니다.
        /// </summary>
        static readonly string[][] DemoMap =
        {
            //  🔴 **목표를 맨 앞에** (09-03 사장님: "튜토 사람들 잘 안 보잖음").
            //     앞 한두 장만 보고 넘어가니, 조작보다 **뭘 해야 끝나는지**가 먼저다.
            new[] { ".....",            // ⓪ 이렇게 하면 끝난다 (조각 하나 = 홈 두 칸)
                    ".....",
                    ".....",
                    "S+.=*",
                    "#####" },
            new[] { ".....",            // → 오른쪽
                    ".....",
                    ".....",
                    "S....",
                    "#####" },
            new[] { ".....",            // ← 왼쪽
                    ".....",
                    ".....",
                    "....S",
                    "#####" },
            //  🔴 떨어지는 장. 아래 키로는 못 보여준다 — 중력이 있어서
            //     슬라임은 늘 무언가를 딛고 서 있고, 발밑은 벽이라 아래로 못 간다.
            //     **발판 끝까지 걸어가면 떨어진다** 가 실제로 일어나는 일이다.
            new[] { ".....",
                    "S....",
                    "##...",
                    "##...",
                    "#####" },
            new[] { ".....",            // 조각을 먹으면 길어진다
                    ".....",
                    ".....",
                    "S..+.",
                    "#####" },
            //  🔴 발판 **위에 올라서는 데까지** 보여준다. 한 칸 뜨고 마는 것은
            //     "올랐다"로 안 읽힌다 (09-03 사장님).
            //     ⚠️ **먹는 장 뒤**에 둔다 — 길어지는 법을 모르고서는 이 장이 안 읽힌다.
            new[] { ".....",            // 몸이 길어야 오른다
                    ".....",
                    ".....",
                    "S+.##",
                    "#####" },
            //  🔴 **삼킨 조각(주황)** — 이 상태는 *떨어지면서* 먹어야 나온다.
            //     평지에서 먹으면 그 걸음에 바로 길어져서 Pg 가 안 선다 (SnakeEngine.Settle).
            //     그리고 머리 초록 + 꼬리 주황으로 보이려면 **길이가 2 이상**이어야 한다 —
            //     길이 1 이면 덩어리가 머리에 얹혀 얼굴을 덮는다.
            //     그래서 ① 평지에서 하나 먹어 길이 2 → ② 발판 끝에서 떨어지며 하나 더 먹는다.
            new[] { ".....",
                    "S+...",
                    "##...",
                    "##.+.",
                    "#####" },
            //  ── 기믹 (rev.10~11) ────────────────────
            //  🔴 안 가르치면 사람들이 모른다. 판에서 처음 만나면 그냥 벙으로 읽힌다.
            //     셀 다 **엔진으로 미리 돌려 되는 것만** 넣었다 (09-05).
            new[] { "...:..",           // 무중력 구역 — 몸이 짧아도 오른다
                    "...:..",
                    "...:..",
                    "S..:..",
                    "######" },
            new[] { "......",           // 미는 상자 — 옆으로만 밀린다
                    "......",
                    "S.B...",
                    "######" },
            new[] { "...1..",           // 문벽 — 홈을 채우면 사라진다
                    "...1..",
                    "S+=*..",
                    "######" },
            //  🔴 얼음 (rev.12-a) — **밟으면** 끝까지 간다. 이게 이 기믹의 전부라
            //     시범도 딱 그것만 보여준다: 오른쪽 한 번, 왼쪽 한 번.
            //     ⚠️ 얼음은 **발밑**이다. 지나가는 칸이 아니다.
            new[] { "......",
                    "S.....",
                    "#IIII#",
                    "######" },
            //  🔴 금 간 바닥 (rev.12) — 지나가면 무너지고 **돌아올 수 없다.**
            //     그래서 시범은 돌아오려다 떨어지는 데까지 보여준다. 그게 요점이다.
            new[] { "......",
                    "S.....",
                    "#XXX##",
                    "......",
                    "######" },
        };
        //  마침표는 **한 박자 쉼** — 상태를 보여주려면 멈출 줄도 알아야 한다
        /// <summary>
        /// 🔴 **타이틀에서 도는 판** (09-04 사장님: "실제 플레이어 캐릭터로").
        /// 장식 그림이 아니라 진짜 게임이다 — 출렁이고 거품 내고 홈으로 빨려든다.
        /// 조각 하나를 먹고 홈에 들어가는 데까지, **탈출을 되풀이한다.**
        /// </summary>
        static readonly string[] TitleMap =
        {
            ".....",
            ".....",
            "S+.=*",
            "#####",
        };
        const string TitleKeys = ">>>>";
        /// _guideSlide 에 넣는 표 — 지금 시범이 **타이틀 것**이라는 뜻
        const int GuideTitle = -2;

        static readonly string[] DemoKeys =
        { ">>>>", ">>>", "<<<", ">>>", ">>>", ">>^>", ">>>..>",
          ">>>^^^", ">>>", ">>>", ">...<...", ">>>...<" };

        /// <summary>
        /// 시범을 세운다. 판은 글자 격자로, 누를 방향은 문자열로 준다.
        /// 자리를 멀리 떨어뜨려서 본판과 안 겹치게 한다 — 층(layer)을 안 쓰고
        /// **카메라 둘이 서로 다른 데를 보게** 하는 쪽이 손이 덜 간다.
        /// </summary>
        public void SetupDemo(Camera cam, string[] grid, string keys, bool gravity)
        {
            _demo = true;
            _cam = cam;
            _org = new Vector2(600f, -600f);
            _demoKeys = keys ?? "";
            _demoAt = 0;
            _demoNext = 0f;
            DemoLoops = 0;
            _demoRush = false;
            _gravity = gravity;
            _L = SnakeEngine.Parse(grid, "demo", gravity);
            BuildBoard();
            Restart();
            AimCamera(true);
        }

        /// 방향 글자 하나 → 걸음. > < ^ v · 마침표는 **한 박자 쉼**
        static SnakeEngine.Dir DirOf(char k)
            => k == '<' ? SnakeEngine.Dir.Left
             : k == '^' ? SnakeEngine.Dir.Up
             : k == 'v' ? SnakeEngine.Dir.Down
             : SnakeEngine.Dir.Right;

        /// <summary>
        /// 시범이 스스로 방향키를 누른다. 다 누르면 잠깐 쉬었다 처음부터.
        /// 🔴 **막히면 그냥 넘어간다.** 중력이 걸려 자리가 어긋나도
        /// 시범이 그 자리에서 굳어버리면 안 된다.
        /// </summary>
        void TickDemo()
        {
            if (_demoKeys.Length == 0) return;
            if (Time.time < _demoNext) return;

            if (_demoAt >= _demoKeys.Length)
            {
                //  몰아붙이는 중이면 처음으로 안 돌아간다 — 빨려드는 걸 끝까지 본다
                if (_demoRush) { _demoNext = float.MaxValue; return; }
                _demoNext = Time.time + DemoHold;
                _demoAt = 0;
                DemoLoops++;
                Restart();
                return;
            }
            //  🔴 마침표는 **가만히 있는 박자**다. 상태를 보여주려면 멈출 줄도 알아야 한다 —
            //     먹자마자 다음 걸음을 밟아버리면 "삼킨 조각"이 한 순간에 지나간다.
            if (_demoKeys[_demoAt] != '.') Step(DirOf(_demoKeys[_demoAt]));
            _demoAt++;
            _demoNext = Time.time + (_demoRush ? DemoRushStep : DemoStep);
        }
        /// <summary>
        /// 🔴 한 장에 시범을 **딱 두 번**만 보여준다 (09-03 사장님).
        /// 시간으로 끊으면 판마다 걸음 수가 달라 어떤 장은 도중에 잘린다.
        /// </summary>
        const int DemoRepeat = 2;
        /// 시범이 어쩌다 안 끝나도 장은 넘어가야 한다 — 마지막 안전장치
        const float SlideMax = 30f;
        const int Slides = 12;
        int _slide;               // 지금 보고 있는 장
        float _slideAt = -99f;    // 그 장이 뜬 시각

        /// 안내판에서 쓰는 네모·동그라미 한 장. 색은 GUI.color 로 준다.
        void Shape(Rect r, Color c, Texture2D tex)
        {
            //  🔴 지워진 그림을 그리면 터진다. PixelSprites 가 만든 것은 스크립트가 다시
            //     컴파일될 때 유니티가 지운다 — 이제 HideAndDontSave 를 붙였지만,
            //     여기서도 한 번 막는다. **그리는 일이 게임을 멈추면 안 된다.**
            if (tex == null) return;
            GUI.color = c; GUI.DrawTexture(r, tex); GUI.color = Color.white;
        }
        /// 모양 없는 네모 — 경계선처럼
        void Bar(Rect r, Color c) => Shape(r, c, Solid(Color.white));
        /// 동그라미 — 장 표시 점
        void Dot(Rect r, Color c) => Shape(r, c, PixelSprites.Disc().texture);

        /// <summary>
        /// 🔴 여섯 장을 돌려 보여준다 — → ← ↓ ↑ · 조각 · 홈.
        /// 글로만 쓰면 안 읽힌다. 작더라도 **움직이는 걸** 봐야 안다.
        /// </summary>
        void Guide(float w, float h)
        {
            bool tall = Tall(w, h);
            float pw = tall ? w : w * GuideW;          // 글자 크기는 이 폭으로 잰다
            float ph = tall ? h * GuideH : h;
            var box = tall ? new Rect(0, h - ph, w, ph) : new Rect(w - pw, 0, pw, h);
            //  카메라 자리도 **매번** 맞춘다 — 폰을 돌리면 가로/세로가 바뀐다.
            //  (y 는 아래가 0 이다: 세로일 때 판은 띠 **위쪽**을 쓴다)
            if (_cam != null)
                _cam.rect = tall ? new Rect(0f, ph / h, 1f, 1f - ph / h)
                                 : new Rect(0f, 0f, 1f - GuideW, 1f);
            //  🔴 바탕을 통째로 칠하면 **시범을 덮는다.** IMGUI 는 카메라보다 늘 위다.
            //     그래서 무대 자리만 남기고 네 조각으로 칠한다.
            //     (아래에서 stage 를 잡은 뒤 칠하므로 여기서는 아무것도 안 한다)
            if (tall) Bar(new Rect(0, box.y, w, 1.5f), new Color(1, 1, 1, 0.10f));   // 위쪽 경계선
            else      Bar(new Rect(box.x, 0, 1.5f, h), new Color(1, 1, 1, 0.10f));   // 왼쪽 경계선

            float pad = tall ? w * 0.05f : pw * 0.09f;

            // 🔴 저절로 넘어가되, 손으로도 넘길 수 있다. 손으로 넘기면 그 장부터 다시 잰다.
            if (_slideAt < 0f) _slideAt = Time.time;
            //  🔴 **시범이 두 번 다 돌면** 다음 장으로. 시간으로 끊지 않는다.
            bool done = _guide != null && _guide.DemoLoops >= DemoRepeat;
            if (done || Time.time - _slideAt > SlideMax)
            { _slide = (_slide + 1) % Slides; _slideAt = Time.time; }
            int slide = _slide;

            // ---- 제목 ----
            var ts = new GUIStyle(_sMid) { alignment = TextAnchor.UpperCenter, wordWrap = true,
                                           fontSize = Px(Mathf.Clamp(pw * 0.075f, 11f, 22f)) };
            ts.normal.textColor = new Color(1, 1, 1, 0.92f);
            //  🔴 상단 띠가 화면 전체 폭이라, 안내판도 그 **아래에서** 시작한다.
            //     안 내리면 띠의 설정·? 단추가 "튜토리얼" 글자를 덮는다.
            //  세로 띠에는 제목 자리가 없다 — 위 띠에 판 이름(1-0)이 이미 있다
            if (!tall)
                GUI.Label(new Rect(box.x + pad, h * 0.115f, pw - pad * 2, h * 0.10f), "튜토리얼", ts);

            // ---- 무대 ----
            // 🔴 무대는 **격자에 딱 맞춰야** 한다. 상자만 크고 칸이 작으면
            //    격자로 안 읽히고 그냥 빈 검은 상자로 보인다 (08-31 화면).
            //  세로 띠에서는 무대를 **왼쪽**에 두고 설명 글을 그 오른쪽에 붙인다.
            float c;
            Rect stage;
            if (tall)
            {
                c = Mathf.Floor(Mathf.Min(w * 0.46f / 5f, ph * 0.46f / 3f));
                stage = new Rect(box.x + pad, box.y + ph * 0.07f, c * 5f, c * 3f);
            }
            else
            {
                c = Mathf.Floor(Mathf.Min((pw - pad * 2f) / 5f, h * 0.30f / 3f));
                stage = new Rect(box.x + (pw - c * 5f) * 0.5f, h * 0.245f, c * 5f, c * 3f);
            }
            //  🔴 바탕은 무대에 **딱 붙여** 칠한다. 사이에 틈을 두면
            //     그 띠는 **어느 카메라도 안 지우는 자리**가 되어 쓰레기 화소가 그대로 비친다
            //     (09-03 사장님 화면: 위아래에 흰 줄). 본 카메라는 이 안내판 자리를 안 비춘다.
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, stage.y - box.y), Solid(PanelBg));
            GUI.DrawTexture(new Rect(box.x, stage.yMax, box.width, box.yMax - stage.yMax), Solid(PanelBg));
            GUI.DrawTexture(new Rect(box.x, stage.y, stage.x - box.x, stage.height), Solid(PanelBg));
            GUI.DrawTexture(new Rect(stage.xMax, stage.y, box.xMax - stage.xMax, stage.height), Solid(PanelBg));

            //  🔴 **시범을 무대 자리에 비춘다.** 카메라 자리는 0~1 로 주고 y 는 아래가 0 이라
            //     화면 좌표(y 가 위가 0)를 뒤집어야 한다.
            if (_guide != null && _guideCam != null)
            {
                if (_guideSlide != slide)
                {
                    _guideSlide = slide;
                    _guideCam.backgroundColor = StageBg;   // 타이틀에서 BgCol 로 바꿔 놨다
                    _guide.SetupDemo(_guideCam, DemoMap[slide % DemoMap.Length],
                                     DemoKeys[slide % DemoKeys.Length], _gravity);
                }
                _guideCam.rect = new Rect(stage.x / w, 1f - stage.yMax / h,
                                          stage.width / w, stage.height / h);
                _guideCam.enabled = true;
            }

            //  🔴 여기서 판을 **흉내내 그리던 코드는 걷어냈다** (09-03 사장님).
            //     같은 것을 두 군데서 그리니 게임이 바뀔 때마다 어긋났다 —
            //     오늘만 두 번(둥글게 바꾼 것 · 심을 희게 바꾼 것). 이제 시범이 진짜로 돈다.

            // ---- 한 줄 설명 ----
            string[] LINE = {
                "몸을 홈에 정확히 포개면 판이 끝납니다\n머리는 마름모 칸에서 끝나야 합니다",
                //  폰에는 화살표 키가 없다 — 밀어서 움직인다 (09-15, 웹 빌드를 가로 폰으로 눌러 보다 잡음).
                //  새 글자를 안 쓴다 (밀·으·로 는 이미 잘라 넣은 글꼴에 있다 — 덫 13)
                Touchy ? "→  오른쪽으로 밀기" : "→  오른쪽 화살표 키",
                Touchy ? "←  왼쪽으로 밀기"   : "←  왼쪽 화살표 키",
                "발판 끝에서는 떨어집니다",
                "주황 조각을 먹으면 몸이 한 칸 길어집니다",
                "↑  위로 — 몸이 길어야 오릅니다",
                "떨어지면서 먹으면 꼬리에 담깁니다\n다음 걸음에 몸이 됩니다",
                "옅은 칸에서는 떨어지지 않습니다\n몸이 짧아도 오를 수 있습니다",
                "상자는 옆으로 밀립니다\n밀어 놓고 딛고 설 수 있습니다",
                "홈을 채우면 같은 번호의 벽이 사라집니다",
                "얼음 위에서는 멈추지 못합니다\n막힐 때까지 미끄러집니다",
                "금 간 바닥은 지나가면 무너집니다\n돌아올 수 없습니다",
            };
            var ls = new GUIStyle(_sSmall) { alignment = TextAnchor.UpperCenter, wordWrap = true,
                                             fontSize = Px(Mathf.Clamp(pw * 0.062f, 11f, 17f)) };
            ls.normal.textColor = new Color(1, 1, 1, 0.80f);
            //  🔴 글이 몇 줄이 될지는 글꼴·화면 폭에 따라 달라진다. **재서** 쓴다 —
            //     아래 점 자리를 비율로 못박아 뒀더니 세 줄이 되는 순간 점을 덮었다
            //     (09-04 밤 화면). 글이 길어질 때마다 다시 터질 자리였다.
            //  세로 띠: 무대 **오른쪽**에, 무대 높이 안에서 글을 둔다
            var lineRect = tall
                ? new Rect(stage.xMax + pad, stage.y, box.xMax - stage.xMax - pad * 2, stage.height)
                : new Rect(box.x + pad, stage.yMax + h * 0.035f, pw - pad * 2, h * 0.30f);
            if (tall) ls.alignment = TextAnchor.MiddleLeft;
            float lineH = ls.CalcHeight(new GUIContent(LINE[slide]), lineRect.width);
            if (tall) GUI.Label(lineRect, LINE[slide], ls);
            else GUI.Label(new Rect(lineRect.x, lineRect.y, lineRect.width, lineH), LINE[slide], ls);

            // ---- 몇 번째 장인지 ----
            float dot = Mathf.Clamp(pw * 0.022f, 4f, 9f), gap = dot * 1.9f;
            //  글 아래로 밀어 둔다 (최소 자리는 지키되, 글이 길면 그만큼 내려간다)
            //  세로 띠: 무대 바로 아래 한 줄
            float row = tall ? stage.yMax + ph * 0.07f
                             : Mathf.Max(h * 0.60f, lineRect.y + lineH + h * 0.030f);
            float span = Slides * gap - (gap - dot);
            float dx = box.x + box.width * 0.5f - span * 0.5f;
            for (int k = 0; k < Slides; k++)
            {
                var dr = new Rect(dx + k * gap, row, dot, dot);
                Dot(dr, k == slide ? StarLit : new Color(1, 1, 1, 0.18f));
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
                //  세로 띠: 점 줄 아래부터 조작 안내 위까지
                var cr = tall
                    ? new Rect(box.x + pad, row + dot + ph * 0.04f, box.width - pad * 2,
                               box.yMax - ph * 0.13f - (row + dot + ph * 0.04f))
                    : new Rect(box.x + pad, h * 0.66f, pw - pad * 2, h * 0.24f);
                if (tall) cs.alignment = TextAnchor.MiddleCenter;
                GUI.Label(cr, coach, cs);
            }

            var ks = new GUIStyle(_sSmall) { alignment = TextAnchor.LowerCenter, wordWrap = true };
            ks.normal.textColor = new Color(1, 1, 1, 0.40f);
            //  🔴 **되돌리기와 다시하기를 여기서도 말해준다** (09-08 사장님: "튜토쪽에
            //     되돌리기 버튼이 없어. 새로하기랑"). 가르치는 판은 바닥 띠 앞에서
            //     빠져나가므로, 안내판이 유일하게 말할 수 있는 자리다.
            GUI.Label(tall ? new Rect(box.x + pad, box.yMax - ph * 0.12f, box.width - pad * 2, ph * 0.10f)
                           : new Rect(box.x + pad, h * 0.90f, pw - pad * 2, h * 0.08f),
                      Touchy ? "화면을 밀어서 움직입니다"
                             : "← ↑ ↓ →     Z 되돌리기     R 처음부터", ks);
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
                    return "거의 다 됐습니다 — 머리가 마름모 칸에서 끝나야 합니다";
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
                return "다 맞았다 — 머리가 마름모 칸에서 끝나야 한다";
            return null;
        }
    }
}
