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
    public partial class SnakeController : MonoBehaviour
    {
        // ⚠ 판 크기는 이제 고정이 아니다. 판마다 grid에서 읽는다(_L.W/_L.H).
        //    이 상수는 판이 없을 때의 기본값으로만 쓴다.
        public const int BoardW = 20, BoardH = 12;
        //  이 묶음에서 제일 큰 판 (판 자료를 읽을 때 한 번 잰다). 0 이면 아직 안 쟀다.
        int _setW, _setH;

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
        static Color BgCol   = new Color32(0x0b, 0x0d, 0x10, 0xff);
        // 🔴 빈 칸은 **어둡게**, 벽은 **밝은 덩어리**로. 예전엔 둘이 #18221e / #25322c 라
        //    거의 같아서 넓은 판에서 굴 모양이 아예 안 읽혔다 (08-30 사장님 전체화면).
        // 🔴 빈 칸이 한 색이라 굴 안이 **평평한 판**으로 보였다.
        //    빛이 위에서 들어온다고 치고 위아래로 기울인다. 규칙과 맞는 거짓말이다 —
        //    이 게임은 중력이 있고, 위가 곧 "올라가야 할 쪽"이다.
        //  🔴 아래쪽을 너무 어둡게 깎았더니 **빈 칸이 홈 속과 같은 색**이 됐다(09-02 화면).
        //     기울기는 위를 밝히는 쪽으로 준다 — 바닥은 홈보다 확실히 밝게 남긴다.
        static Color Floor    = new Color32(0x12, 0x16, 0x1b, 0xff);  // 빈 칸 = 공기 (중간)
        static Color FloorLit = new Color32(0x15, 0x1a, 0x20, 0xff);  // 굴 위쪽 — 빛이 닿는다
        static Color FloorDim = new Color32(0x0f, 0x12, 0x17, 0xff);  // 굴 바닥 — 가라앉는다
        static Color Rock    = new Color32(0x2b, 0x32, 0x3c, 0xff);   // 벽 = 차가운 회청색 돌
        static Color RockTop = new Color32(0x4a, 0x55, 0x63, 0xff);   // 딛고 설 수 있는 윗면

        //  🔴 **굴마다 지형만 다르게** (09-12, todo "굴별 겉모습"). 위 여섯 값을 판을 열 때 갈아 끼운다.
        //     뜻을 나르는 색(슬라임 초록 · 조각 주황 · 홈 테두리 · 포탈 보라 · 심 흰빛)은 **안 건드린다.**
        //     전부 무채색을 살짝 기울인 것뿐이다 — 색이 있으면 정보로 읽힌다 (색 규칙 ③).
        //  ⚠️ 빈 칸 어두운 쪽을 홈 속(HoleCol 0x080a0d)보다 어둡게 내리지 않는다 — 홈이 파여 보여야 한다.
        //  ⚠️ 굴 3 은 **차갑게** — 금 간 바닥(따뜻한 회색)이 떠야 한다. 굴 2 는 **중성** — 상자(푸른 회색)가 떠야 한다.
        //     굴 5 의 보라 기운은 아주 옅게 — 꺼진 포탈(PortOff)이 벽에 묻히면 안 된다.
        struct CaveLook { public Color bg, lit, mid, dim, rock, top; }
        static Color C32(uint v) => new Color32((byte)(v >> 16), (byte)(v >> 8), (byte)v, 0xff);
        static CaveLook Cave(uint bg, uint lit, uint mid, uint dim, uint rock, uint top) => new CaveLook
            { bg = C32(bg), lit = C32(lit), mid = C32(mid), dim = C32(dim), rock = C32(rock), top = C32(top) };
        /// <summary>
        /// 🔴 **굴은 아래에서 위로 올라간다** (09-20 사장님이 받아오신 조언).
        /// 게임 이름이 *탈출*이고 배경에 종유석이 있다 — **1굴이 가장 깊고 어둡고,
        /// 출구로 갈수록 밝아진다.** 그러면 "얼마나 왔나"가 **진행도 자체**에 담기고,
        /// 마지막에 처음 빛을 보는 순간이 세진다.
        ///
        /// ⛔ 전에는 반대였다 — 굴 6(피날레)이 *"제일 어둡고 윗면만 또렷"* 이었다.
        ///    깊이로 뒤집으면서 손으로 적던 여섯 벌을 **계산식 한 벌**로 바꿨다:
        ///    굴이 13층으로 늘었는데 열세 벌을 손으로 적으면 사다리가 고르지 않고,
        ///    층수가 또 바뀌면 전부 다시 적어야 한다.
        ///
        /// ⚠️ 색 규칙은 지킨다 — 초록은 슬라임만. 출구 쪽의 따뜻함은 **아주 옅게**만 섞는다
        ///    (빛이 스며드는 것이지 색을 칠하는 게 아니다).
        /// </summary>
        public const int CaveDepth = 13;          // 굴이 몇 층인가 (맨 아래 1 ~ 출구 13)

        /// 배경에서 살짝 들어올린 색 — 빈 칸을 그린다
        static Color Lift(Color c, float up) => new Color(c.r + up, c.g + up * 1.02f, c.b + up * 1.06f, 1f);

        static CaveLook CaveOf(int ch)
        {
            //  ⚠️ **제곱근을 썼다가 물렀다** (09-20). 아래쪽만 확 벌어지고 위쪽 여섯 굴이 뭉쳤다 —
            //     밝기가 6 14 18 21 23 25 27 28 30 31 33 34 35 로, 칸 차이가 처음엔 8 이고 나중엔 1 이었다.
            //     **직선**이 고르다 (칸 차이 3~4). 대신 출구를 더 밝게 잡아 폭을 넓혔다 (6 → 48).
            float k = Mathf.Clamp01((Mathf.Clamp(ch, 1, CaveDepth) - 1) / (float)(CaveDepth - 1));

            //  깊은 바닥 → 출구. 위로 갈수록 붉은 기가 아주 조금 는다 (빛이 든다)
            Color bg   = Color.Lerp(C32(0x05060a), C32(0x26313f), k);
            Color rock = Color.Lerp(C32(0x22262e), C32(0x59657a), k);
            Color top  = Color.Lerp(C32(0x39414f), C32(0x95a3b8), k);

            return new CaveLook {
                bg = bg,
                lit = Lift(bg, 0.055f),           // 빈칸 위
                mid = Lift(bg, 0.032f),           // 빈칸
                dim = Lift(bg, 0.014f),           // 빈칸 아래
                rock = rock, top = top };
        }

        //  🔴 **굴이 바뀐 것을 알린다** (09-16 사장님: *"굴 바뀌는 게 약하다"* ·
        //     *"이펙트가 좀 약했어. 개인적으로 뒷 배경이 좀 바뀌는 것도 방법이라 생각함"*).
        //     굴별 색은 09-12 부터 있었지만 **지형만** 아주 옅게 바뀌어서, 넘어간 줄을 모르고 지나쳤다.
        //     배경은 판 고르기·타이틀에 깔았고(SnakeController.Cave.cs), 여기서는 **판에 들어설 때** 알린다.
        int _caveShown = -1;
        float _caveAt = -9f;

        /// 판을 짓기 **전에** 부른다 — BuildBoard 가 이 값들을 한 번 읽어 칠한다.
        void ApplyCaveLook(int ch)
        {
            var k = CaveOf(ch);
            BgCol = k.bg; FloorLit = k.lit; Floor = k.mid; FloorDim = k.dim; Rock = k.rock; RockTop = k.top;
            if (_cam != null) _cam.backgroundColor = BgCol;

            //  ⚠️ 이 함수는 **판을 열 때마다** 불린다 — 같은 굴 안에서 다음 판으로 가거나
            //     R 로 다시 시작할 때도 온다. 그래서 **굴이 바뀔 때만** 시각을 적는다.
            if (ch != _caveShown) { _caveShown = ch; _caveAt = Time.time; }
        }
        static Color HeadCol = new Color32(0x9b, 0xe8, 0xad, 0xff);   // 지도 위 "여기 있다" 표시
        static Color BodyCol = new Color32(0x6f, 0xd0, 0x8c, 0xff);   // 나
        static Color FoodCol = new Color32(0xf0, 0x87, 0x3a, 0xff);   // 조각
        // 🔴 목표 홈 — 바닥(0x18221e)과 색이 거의 같아 안 보였다.
        //    속은 훨씬 어둡게 파고, 민트 테두리를 둘러 확실히 띄운다.
        //  🔴 홈 속은 **어느 빈 칸보다도 어두워야** 한다. 그래야 파인 것으로 읽힌다.
        static Color HoleCol   = new Color32(0x08, 0x0a, 0x0d, 0xff);  // 파인 속
        static Color HoleEdge  = new Color32(0xe8, 0xed, 0xf2, 0xff);  // 테두리(민트)
        // 🔴 홈을 두르는 **놋쇠 틀** — 참고 그림의 그 틀이다.
        //    돌도 초록 · 슬라임도 초록인데 홈만 놋쇠라서 **목표가 색으로 뛰어나온다.**
        //  🔴 홈 테두리 = **출구 표시** (09-03 사장님: "여기가 출구야! 하는 초록 형광").
        //     슬라임과 같은 초록이지만 쓰임이 정반대다 —
        //     슬라임은 **꽉 찬 무광**, 홈은 **속이 빈 형광 테두리**. 열쇠와 열쇠구멍이다.
        //     그래서 "초록은 슬라임만" 규칙이 안 깨진다: 채워진 초록은 여전히 나 하나뿐이다.
        static Color FrameCol  = new Color32(0x4d, 0xff, 0xa0, 0xff);  // 홈 테두리 — 형광 초록
        static Color FrameDim  = new Color32(0x1c, 0x6b, 0x45, 0xff);
        static Color SlotLine  = new Color32(0x1a, 0x1f, 0x25, 0xff);  // 틀 안 칸을 나누는 선
        static readonly Color Frame2Col = new Color32(0x5c, 0xe0, 0xff, 0xff);  // 둘째 틀 — 형광 하늘빛
        static readonly Color Frame2Dim = new Color32(0x4a, 0x5a, 0x70, 0xff);
        Color RailOf(int door) => door == 0 ? FrameCol : Frame2Col;
        Color RailDimOf(int door) => door == 0 ? FrameDim : Frame2Dim;
        static Color HoleFill  = new Color32(0x2f, 0x4a, 0x39, 0xff);  // 몸이 덮인 칸 — 자물쇠에 불이 든다
        static Color CoreCol   = new Color32(0xff, 0xff, 0xff, 0xff);  // 심 = 열쇠 구멍
        static Color CoreRing  = new Color32(0xc8, 0xd4, 0xdc, 0xff);   // 🔴 회색이면 머리 마름모가 흐려진다
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
        //  ⚠️ 갈색이었는데, 색이 있으면 "정보" 로 읽혀 조각(주황)과 다툰다.
        //     열릴 수 있는 벽이라는 건 **벽보다 밝다**는 것으로 말한다.
        // 🔴 무중력 구역 — "여기선 안 떨어진다". 벽이 아니라 **뒷벽**이라
        //    바닥보다 밝되 홈(목표)보다는 어둡다. 색 규칙 ③ 대로 무채색이다.
        //    ⚠️ 홈처럼 테를 두르면 "여기 채워라"로 잘못 읽힌다 — 테는 두르지 않는다.
        // 🔴 미는 상자 — **딛고 설 수 있는 것**이라 벽(Rock)과 같은 계열로 그린다.
        //    두고 온 몸도 같은 이유로 벽 계열이다. 디딜 수 있는 건 디딜 수 있게 보여야 한다.
        //    벽과 가르는 건 색이 아니라 **윗면이 밝고 테가 있다**는 것 — "이건 낱개다".
        static readonly Color BoxCol  = new Color32(0x44, 0x4d, 0x5a, 0xff);
        static readonly Color BoxTop  = new Color32(0x6e, 0x7a, 0x8a, 0xff);
        // 🔴 포탈 — **보라**다 (09-05 사장님: "포털색 + 이게 포털이다! 라는 느낌").
        //    ⚠️ 색 규칙 ③(나머지는 무채색)의 **예외**다. 사장님이 정하셨다.
        //       무채색으로 뒀더니 바닥에 묻혀 "이게 뭐지" 가 됐다.
        //    🔴 왜 보라인가 — 안 겹치는 자리가 여기뿐이다:
        //       초록은 슬라임 · 주황은 조각 · 민트/푸른빛은 홈 1·2 · 흰빛은 심.
        //    켜지기 전엔 **색이 빠진다** (회보라). 켜지면 보라가 돌아오고 빛이 돈다.
        static readonly Color PortOff  = new Color32(0x4a, 0x42, 0x56, 0xff);
        static readonly Color PortOn   = new Color32(0xa8, 0x75, 0xe8, 0xff);
        static readonly Color PortGlow = new Color32(0x7a, 0x4f, 0xc0, 0xff);
        static readonly Color PortHole = new Color32(0x17, 0x10, 0x28, 0xff);
        // 🔴 얼음 (rev.12) — 색 규칙 ③ 대로 **무채색**이다. 파란 물감을 쓰면 홈 2(하늘빛)와 다툰다.
        //    차가움은 중성회색을 **푸른 쪽으로 아주 살짝** 기울여서 낸다.
        //    ⚠️ "여기서 미끄러진다"를 말하는 건 색이 아니라 **가로 결**이다 —
        //       결이 좌우로 누워 있으니 좌우로 미끄러진다는 게 그림만 봐도 읽힌다.
        static readonly Color IceCol   = new Color32(0x2c, 0x38, 0x46, 0xff);  // 언 바닥 (벽보다 푸르고 밝다)
        static readonly Color IceTop   = new Color32(0x8f, 0xb8, 0xd4, 0xff);  // 🔴 **밟는 면.** 미끄럽다는 말은 여기서 한다
        static readonly Color IceLine  = new Color32(0x8f, 0xa6, 0xbe, 0xff);  // 결
        static readonly Color IceShine = new Color32(0xdf, 0xec, 0xf8, 0xff);  // 미끄러져 지나가는 빛
        // 🔴 금 간 바닥 (rev.12) — **딛고 설 수 있는 것**이라 벽(Rock)과 같은 계열이다.
        //    벽과 가르는 건 **따뜻한 쪽으로 기운 어두움 + 금**. "돌인데 삭았다".
        //    ⚠️ 색을 주면 정보로 읽혀 조각(주황)과 다툰다 — 갈색이 아니라 **따뜻한 회색**이다.
        static readonly Color CrackCol  = new Color32(0x38, 0x33, 0x2e, 0xff);
        static readonly Color CrackTop  = new Color32(0x5e, 0x55, 0x4c, 0xff);
        static readonly Color CrackLine = new Color32(0x14, 0x12, 0x10, 0xff);
        static readonly Color ZoneCol = new Color32(0x3a, 0x40, 0x48, 0xff);
        static readonly Color ZoneDot = new Color32(0x9a, 0xa6, 0xb4, 0xff);
        static readonly Color GateCol  = new Color32(0x3c, 0x44, 0x50, 0xff);
        static readonly Color GateEdge = new Color32(0x6e, 0x7a, 0x8a, 0xff);
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

        static Color SpentCol = new Color32(0x39, 0x40, 0x4a, 0xff);   // 굳은 몸 — 돌이 됐다
        static Color SpentTop = new Color32(0x5c, 0x68, 0x75, 0xff);   // 윗면 — 여기 설 수 있다

        // 🔴 출구 — 맵을 넘는 자리. 동작만 넣고 **그리는 걸 빼먹어서** 아무것도 안 보였다(08-30).
        //    문(홈)과 헷갈리면 안 되니 색을 아예 다르게 — 따뜻한 빛으로 둔다.
        static Color StarLit   = new Color32(0xf2, 0xc9, 0x4c, 0xff);  // 땡 별  ⚠️ 심(CoreCol #f0c05a)과 거의 같은 금빛이다
        Vector2 _lookDir = Vector2.right;   // 마지막으로 간 쪽 — 눈이 그쪽을 본다
        //  🔴 눈은 뚝 돌지 않는다. _lookDir 을 쫓아가는 **부드러운** 값이다 (09-03).
        //     이게 없으면 걸음마다 마름모가 순간이동해서 눈이 아니라 표시로 보인다.
        Vector2 _lookNow = Vector2.right;
        const float KeyLead = 0.17f;        // 가는 쪽으로 쏠리는 몫 (머리 크기 대비)
        //  🔴 판 밖 화면도 같은 규칙이다 (rev.8): 초록은 슬라임만 · 나머지는 무채색.
        //     여기가 초록빛이면 "초록 = 나" 라는 약속이 판 밖에서 깨진다.
        static readonly Color PanelBg   = new Color32(0x14, 0x18, 0x1e, 0xff);  // 안내판 바탕
        static readonly Color StageBg   = new Color32(0x0b, 0x0d, 0x10, 0xff);  // 안내판 속 무대 (배경과 같게)
        // 표지판 — 맵에 떠 있는 화살표. 지형과 안 섞이게 밝은 무채색으로 둔다.
        //  ⚠️ 푸른빛이었는데, 색이 있으면 "이건 정보다" 라는 뜻이 되어 홈·조각과 다툰다.
        static readonly Color SignFrame = new Color32(0xe8, 0xed, 0xf2, 0xff);
        static readonly Color SignFill  = new Color32(0x23, 0x28, 0x30, 0xff);
        static readonly Color SignArrow = new Color32(0xc2, 0xcb, 0xd6, 0xff);
        // 판 고르기 지도 — 굴 입구처럼 보이게. 벽(Rock)과 **같은 계열**이라야
        // 판 목록이 판과 같은 세계로 읽힌다.
        //  열린 마디의 돌 · 윗면은 굴마다 CaveOf(ch).rock / .top 을 쓴다 (09-12)
        static readonly Color NodeLock  = new Color32(0x1a, 0x1e, 0x25, 0xff);   // 아직 못 여는 것

        // 🔴 받침대 — **홈이 아니다.** 여기 놓은 몸은 점수가 안 되고 계단만 된다.
        //    홈(민트)과 색을 확실히 갈라놓아야 사람이 헷갈리지 않는다.
        static readonly Color PadCol  = new Color32(0x4a, 0x52, 0x5e, 0xff);   // 빈 받침대 — 돌보다 밝게
        static readonly Color PadEdge = new Color32(0x8e, 0x97, 0xa3, 0xff);
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

        /// <summary>
        /// 🔴 **태어나기.** 판이 열리면 슬라임이 시작 칸에서 스르륵 올라온다 (09-03 사장님).
        /// 끝이 "심으로 빨려듦"이니 시작은 그 반대여야 짝이 맞는다 —
        /// 툭 나타나면 놓아둔 말이 되고, 기어 나오면 **살아 있는 것**이 된다.
        /// </summary>
        const float BornTime = 0.42f;

        /// <summary>
        /// 🔴 **출렁임** (09-03 사장님: "슬라임이니까 조금 출렁거리는 느낌").
        /// 늘 잔잔히 흔들리고, 걸음 뒤엔 크게 흔들렸다 잦아든다.
        /// 마디마다 위상을 어긋나게 줘서 출렁임이 **몸을 타고 흐르게** 한다 —
        /// 다 같이 뛰면 젤리가 아니라 화면 전체가 깜빡이는 것으로 보인다.
        /// </summary>
        /// 🔴 **밑이 무겁다** (09-03 사장님). 네 가지가 같이 그 느낌을 만든다:
        ///   ① 밑면이 붙박이 — 눌리면 가운데가 아니라 **아래로** 주저앉는다
        ///   ② 쉴 때도 제 무게에 조금 퍼져 있다 (Sag)
        ///   ③ 세로가 가로보다 **늦게** 따라온다 — 무게가 뒤늦게 내려앉는다 (Drag)
        ///   ④ 느리게 흔들린다 — 가벼운 것일수록 빠르게 떤다
        const float WobbleHz    = 2.6f;    // 흔들리는 빠르기
        const float WobbleLag   = 0.85f;   // 마디 사이 위상차 (몸을 타고 흐른다)
        const float WobbleIdle  = 0.095f;  // 가만있을 때 (09-03: 터질 것처럼)
        const float WobbleKick  = 0.100f;  // 걸음 직후에 더해지는 몫

        /// <summary>
        /// 🔴 **부푼다** — 가로세로가 **같이** 는다 (09-03 사장님: "막 터질 것같이").
        /// 서로 반대로 가는 건 출렁임이지 **압력**이 아니다. 안에서 밀어내는 느낌은
        /// 부피가 실제로 늘었다 줄어야 나온다.
        ///
        /// 🔴 밖으로는 조금, 안으로는 크게. 몸(차례 3)은 벽(-3)보다 **위**라
        /// 칸 밖으로 크게 부풀면 돌을 뚫고 나간 것처럼 보인다.
        /// </summary>
        const float SwellHz     = 1.35f;   // 부풀었다 꺼지는 빠르기
        const float SwellLag    = 1.10f;   // 마디 사이 위상차
        const float SwellAmt    = 0.055f;  // 부푸는 몫
        const float BulgeOut    = 0.55f;   // 밖으로 나가는 쪽은 이만큼만
        const float BulgeIn     = 1.55f;   // 안으로 들어오는 쪽은 이만큼 (공짜다)
        const float MaxBulge    = 1.10f;   // 🔴 이보다 크게는 절대 안 부푼다
        const float WobbleSag   = 0.040f;  // 제 무게로 퍼진 몫 (가로 +, 세로 −)
        const float WobbleDrag  = 0.60f;   // 세로가 늦는 만큼 (라디안)

        /// <summary>
        /// 🔴 **방향 출렁임** (09-03 사장님: "앞뒤 점프 이럴 때 출렁이는 느낌").
        /// 잔잔한 사인파(Wobble…)와는 다른 것이다 — 저건 늘 도는 숨이고,
        /// 이건 **일이 벌어졌을 때** 한 번 치고 두어 번 흔들리다 잦아드는 것이다.
        ///
        /// 가는 쪽으로 늘고 그 직각으로 줄어든다. 부피가 도는 것처럼 보여야 젤리가 된다.
        /// 마디마다 늦게 와야 출렁임이 **몸을 타고 흐른다.**
        /// </summary>
        const float JigHz     = 2.6f;    // 흔들리는 횟수 (초당)
        const float JigDecay  = 5.5f;    // 잦아드는 빠르기 — 클수록 짧게 끝난다
        const float JigStep   = 0.15f;   // 한 칸 갈 때
        const float JigLand   = 0.26f;   // 떨어져 닿을 때 (제일 세다)
        const float JigFall   = 0.16f;   // 떨어지는 **동안** 세로로 늘어나는 몫
        float _jigX, _jigY;              // 가로·세로 세기 (서로 반대다)
        float _jigT = 99f;               // 친 뒤 지난 시간

        /// 지금 이 순간 출렁이는 몫. 친 뒤 시간이 지나면 0 으로 잦아든다.
        void Jig(float ax, float ay)
        {
            _jigX = ax; _jigY = ay; _jigT = 0f;
        }

        /// <summary>
        /// 🔴 **밑동이 촥 퍼진다** (09-03 사장님). 눌린 만큼 옆으로 밀려 나온다 —
        /// 부피는 그대로인데 위에서 누르면 아래가 퍼지는, 물방울이 앉는 그 모양이다.
        ///
        /// ⚠️ 09-02 에 치마·돔을 붙였다가 **비석·유령처럼 보여서** 걷어낸 적이 있다.
        ///    그때와 다른 점 셋: ① 아주 낮게 ② 몸보다 조금만 넓게
        ///    ③ **고정이 아니라 눌릴 때만** 퍼진다. 가만있을 때 실루엣이 바뀌면 그때처럼 된다.
        /// </summary>
        const float SplayIdle  = 0.06f;   // 쉴 때 (거의 대칭)
        const float SplayBy    = 6.0f;    // 눌린 몫에 곱한다
        const float SplayKick  = 0.55f;   // 착지 순간 촥
        const float SplayMax   = 1.00f;   // 이 값이면 제일 퍼진 단

        /// <summary>
        /// 🔴 **증발하는 김** (09-03 사장님). 몸에서 조금씩 떠올라 사라진다.
        /// 떨어져 나온 뒤엔 몸을 안 따라간다 — 붙어 다니면 김이 아니라 장식이 된다.
        /// </summary>
        class Puff
        {
            public SpriteRenderer sr;
            public Vector2 p;              // 떨어져 나온 자리 (그 자리에 남는다)
            public float t, sp, dx, size;
        }
        readonly List<Puff> _puffs = new List<Puff>();

        /// <summary>
        /// 🔴 **몸 안에서 부글부글** (09-03 사장님).
        /// 김(<see cref="Puff"/>)과 헷갈리지 말 것 — 김은 몸에서 **떨어져 나와**
        /// 제자리에 남지만, 거품은 **마디에 붙어서** 같이 출렁인다.
        /// 그래서 자리를 월드로 안 박고, 마디 안에서의 **비율**로 들고 있는다.
        /// </summary>
        class Bubble
        {
            public SpriteRenderer sr;
            public int seg;                // 어느 마디 안에 있나
            public float x;                // 마디 폭의 -0.5 ~ 0.5
            public float t, sp, size;      // 0 = 바닥 · 1 = 꼭대기
        }
        readonly List<Bubble> _bubbles = new List<Bubble>();

        /// <summary>
        /// 🔴 **삼킨 조각.** 먹었지만 아직 몸이 안 된 것 — 다음 걸음에 마디 하나가 된다.
        /// 꼬리 **속**에서 부푼다. 마디를 통째로 물들이면 몸이 두 색이 되어
        /// 한 마리로 안 보인다 (09-03 사장님 화면).
        /// </summary>
        SpriteRenderer _swallow;
        //  🔴 끓는 것처럼 보이려면 **수**가 있어야 한다. 몇 개로는 방울이지 끓음이 아니다.
        const int BubbleCount = 16;
        //  마디가 제 무게로 내려앉은 몫. 이음매가 같이 안 내려가면 관이 어긋난다.
        readonly List<float> _segDy = new List<float>();
        //  🔴 마디의 **지금** 가로·세로 (출렁임까지 들어간 실제 크기).
        //     이음매가 이걸 안 보면 몸만 눌리고 관은 원래 굵기로 남는다 (09-03 사장님).
        readonly List<float> _segW = new List<float>();
        readonly List<float> _segH = new List<float>();
        const int   PuffCount = 7;
        const float PuffRise  = 0.55f;     // 얼마나 떠오르나 (칸)
        float _bornAt = -99f;
        SpriteRenderer _bornFlash;         // 시작 칸에서 번지는 빛
        //  🔴 시작 칸의 **심**. 홈의 심과 **똑같은 마름모**다 —
        //     같은 것을 두 군데 두어야 "여기서 나와 저기로 들어간다"가 말 없이 읽힌다.
        SpriteRenderer _bornRing, _bornCore;
        /// 0 = 막 나오는 중 · 1 = 다 나왔다
        float BornT => Mathf.Clamp01((Time.time - _bornAt) / BornTime);
        /// 다 빨아들이고 결과 화면까지 두는 사이
        const float WinDelay = 0.5f;

        //  미는 상자 — 자리가 매 걸음 바뀐다 (몸처럼 따라 그린다)
        readonly List<SpriteRenderer> _boxViews = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _boxTops = new List<SpriteRenderer>();
        // 🔴 **상자는 무겁게 움직인다** (09-05 사장님: "너무 슥 하고 밀리거든?").
        //    자리를 바로 찍으면 순간이동으로 보인다. 슬라임과 반대 곡선을 쓴다 —
        //    슬라임은 빨리 떠서 사뿐히 앉고(Ease), 상자는 **버티다 밀려서 쿵 선다**.
        readonly List<Vector2> _boxDrawn = new List<Vector2>();   // 지금 그려지는 자리
        readonly List<Vector2> _boxFrom = new List<Vector2>();    // 이번 걸음의 출발선
        readonly List<float> _boxSq = new List<float>();          // 미는 동안 퍼지는 정도
        const float BoxLag = 0.20f;      // 슬라임보다 늦게 떠난다 — 버티는 만큼
        const float BoxSpan = 1.25f;     // 그리고 더 오래 간다
        const float BoxShake = 0.05f;    // 멈출 때 한 번 떤다
        const float BoxSquash = 0.09f;   // 밀리는 동안 옆으로 퍼지고 납작해진다
        //  포탈 — (칸, 몇 번 홈이 켜는가, 테, 속, 도는 점 둘)
        class Port
        {
            public int cell, door;
            public SpriteRenderer glow, ring, hole;
            public SpriteRenderer[] dots;
        }
        readonly List<Port> _ports = new List<Port>();
        //  🔴 방금 건너뛴 포탈 두 짝 (들어간 칸 · 나온 칸) — 고리가 한 번 크게 돈다.
        //     안 그러면 몸이 그냥 사라졌다 나타나서 "버그인가?" 로 읽힌다.
        int _warpIn = -1, _warpOut = -1;
        float _warpT = 9f;
        //  무중력 구역의 떠 있는 점 — (칸, 몇 번째, 그림)
        //  🔴 얼음 — 결 위를 지나가는 빛. 가만있는 그림이면 "그냥 다른 바닥"으로 읽힌다.
        readonly List<(int cell, SpriteRenderer sr)> _iceShines
            = new List<(int, SpriteRenderer)>();
        //  🔴 금 간 바닥 — 부서지면 사라진다. 부서지는 **동안**을 보여줘야
        //     "내가 밟아서 무너졌다"가 읽힌다 (그냥 사라지면 버그로 보인다).
        struct Crack { public int cell; public SpriteRenderer slab, top, l1, l2; public float t; }
        readonly List<Crack> _cracks = new List<Crack>();
        readonly List<(int cell, int k, SpriteRenderer sr)> _zoneDots
            = new List<(int, int, SpriteRenderer)>();
        readonly Dictionary<int, SpriteRenderer> _gateViews = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, int> _gateOf = new Dictionary<int, int>();   // 칸 -> 문 번호
        readonly Dictionary<int, SpriteRenderer> _gateInner = new Dictionary<int, SpriteRenderer>();
        int _shownDm = -1;    // 지금 화면에 그려진 "열린 문" 상태
        //  🔴 문벽마다 따로 사라진다. 값 하나로 묶어두면 홈 하나 채울 때마다
        //     이미 사라진 문벽까지 다시 사라지는 연출이 돈다 (09-05).
        readonly float[] _gateFadeOf = new float[3];

        // ---- 방 ----
        // 🔴 지도 전체를 한 번에 보여주면 "방"이 안 생긴다. 넓은 판 하나로 보일 뿐이다.
        //    문벽을 **다 닫아놓고** 이어진 덩어리를 찾으면 그게 곧 방이다 — 따로 적을 필요가 없다.
        //  🔴 방 나누기는 rev.10 에서 걷어냈다. 문벽에 딸려 있던 **카메라 미끄러짐**이
        //     사장님이 원치 않으신 것이었고 (09-04: "카메라 이동 없이 한 화면에서만"),
        //     그것 때문에 47판이 전부 문벽을 안 쓰고 있었다.
        //     tools/fit.js 로 재보니 폰 세로에서도 가로 22칸까지는 한 칸 30픽셀이 나온다.
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
        /// 놋쇠 틀 조각들. (문번호, 그림, 장식인가)
        readonly HashSet<int> _seated = new HashSet<int>();          // 지금 몸이 덮고 있는 홈 칸
        readonly Dictionary<int, float> _seatAt = new Dictionary<int, float>();  // 그 칸이 덮인 시각
        /// 이웃한 목표 칸의 속을 이어 붙이는 조각. (칸 두 개, 그림 하나)
        readonly List<(int a, int b, SpriteRenderer sr)> _bridges = new List<(int, int, SpriteRenderer)>();
        /// <summary>
        /// 홈 뒤에 번지는 빛. 🔴 **여러 겹**이다 (09-03 사장님: "끝으로 갈수록 흐려지게").
        /// 한 겹으로는 가장자리가 뚝 끊겨 **네모 덩어리**로 보인다 —
        /// 코드가 그리는 도형에는 번짐이 없으니 겹을 쌓아서 흉내낸다.
        /// float 은 그 겹의 진하기 (바깥일수록 작다).
        /// </summary>
        readonly List<(int door, SpriteRenderer sr, float a)> _holeGlow
            = new List<(int, SpriteRenderer, float)>();

        /// 🔴 빛이 칸 **밖으로** 번지는 거리 (09-03 사장님이 주신 0.7).
        ///    1.0 보다 작게 두면 테두리 뒤에 통째로 가려서 안 보인다.
        const float GlowSpread = 0.7f;
        //  겹마다 (얼마나 나갔나 0~1, 진하기). 안쪽이 진하고 바깥이 옅다.
        static readonly (float k, float a)[] GlowRing =
            { (0.06f, 0.34f), (0.28f, 0.20f), (0.55f, 0.11f), (1f, 0.05f) };
        //  테두리 이음매 — 테두리와 같은 색으로 같이 칠한다
        readonly List<(int door, SpriteRenderer sr)> _holeEdgeExtra = new List<(int, SpriteRenderer)>();

        /// <summary>
        /// 🔴 **출구에서 위로 스르르 올라가는 불빛** (09-03 사장님).
        /// 홈이 "채워야 할 칸"이 아니라 **나가는 곳**으로 읽히게 하는 것이 목적이다.
        ///
        /// 홈 **안에서만** 오르내린다 — 밖으로 내보내면 벽을 뚫고 지나간다.
        /// 같은 세로줄에서 홈이 이어진 만큼만 올라가고, 위아래 끝에서 사라진다.
        /// </summary>
        class Mote
        {
            public SpriteRenderer sr;
            public int door;
            public float x, y0, y1;   // 오르는 구간 (월드)
            public float t, sp;       // 0~1 진행 · 빠르기
        }
        readonly List<Mote> _motes = new List<Mote>();
        readonly Dictionary<int, int> _doorOf = new Dictionary<int, int>();   // 칸 -> 문 번호
        /// 굳은 몸의 윗면. 문이 열린 뒤에만 보인다.
        readonly Dictionary<int, SpriteRenderer> _spentTop = new Dictionary<int, SpriteRenderer>();
        const float SlotInner = 0.88f;   // 홈 속의 크기. 나머지가 테두리로 보인다
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
        //    09-05: 덤 조각 공정으로 47판을 통째 갈았다 → rev3.
        //    09-05 밤: rev.11 기믹 공정으로 47판을 또 통째 갈았다 → rev4.
        //    09-05 낮: 길게 다시 뽑은 47판 → rev5.
        //  🔴 rev.12 에서 판이 통째로 바뀌었다 — 옛 기록을 그대로 두면
        //     안 깬 판에 별이 붙어 있다 (09-05 화면에서 0~6번에 별이 남아 있었다).
        const string ProgressKey = "snakeRecords_rev8";
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
        //  🔴 판 고르기에서 **보고 있는 굴.** 0 이면 지금 판의 굴을 연다.
        //     Load() 가 0 으로 되돌린다 — 판을 하다 목록을 열면 늘 그 판의 굴부터 보인다.
        int _menuCh;
        //  🔴 잠긴 판을 누르면 **그 마디가 도리질한다** (09-13). 전엔 아무 반응이 없어서 눌린 건지도 몰랐다.
        int _lockShake = -1;
        float _lockShakeAt = -9f;

        // ---- 깬 뒤 결과 화면 ----
        // 🔴 깨자마자 다음 판으로 넘기면 "해냈다"가 없다. 별을 하나씩 찍어 보여주고 넘어간다.
        bool _result;
        float _resultAt;         // 화면이 뜬 시각 — 별이 나오는 박자를 여기서 잰다
        int _resultStars, _resultSteps;
        bool _resultStar;        // 이번에 별을 먹었나
        bool _resultBest;        // 최고 기록을 갈아치웠나
        int _shown;              // 지금까지 소리를 낸 별 개수
        const float StarBeat = 0.42f, StarPop = 0.30f, StarLead = 0.35f;

        // 마디마다 화면 위치를 따로 굴린다 — 뒷마디가 조금 늦게 출발한다
        readonly List<Vector2> _segPos = new List<Vector2>();   // 지금 그려지는 자리
        readonly List<Vector2> _from = new List<Vector2>();     // 이번 걸음을 시작한 자리
        float _pop, _stepT;

        // ---- 낙하 ----
        // 🔴 걸음과 낙하는 다른 동작이다. Step()은 둘을 한 번에 끝내 버리므로,
        //    화면에서는 걸음 -> 낙하 두 구간으로 나눠 그린다.
        int _fall;                                              // 이번 걸음에 떨어진 칸 수
        //  🔴 이번 걸음에 **미끄러진 칸 수** (얼음이 없으면 1).
        //     한 칸이면 그냥 걸은 것이고, 두 칸 이상이면 그만큼 오래 흘러야 한다.
        int _slid = 1;
        readonly List<Vector2> _mid = new List<Vector2>();      // 떨어지기 직전 자리
        float _land;                                            // 착지 눌림 (1 -> 0)
        bool _landed = true;                                    // 이번 낙하의 착지를 이미 쳤나
        bool _showPanel;

        // 🔴 검증할 때 테스터에게 개발자 정보가 보이면 안 된다.
        //    "최단 14걸음"을 보면 행동이 바뀌고, N을 누르면 판을 건너뛴다.
        //    F1로만 열린다. 기본은 꺼짐.
        bool _dev;
        //  🔴 개발자 모드는 **에디터와 개발 빌드에서만** 켜진다 (09-13, todo 「없어야 할 것」).
        //     출시 빌드에서 F1·T 를 누르면 누구나 판을 건너뛰고(N/P) 정답을 재생(F3)할 수 있었고,
        //     판 오른쪽 아래에 「F1」 글씨도 늘 보였다.
        //  ⚠️ 웹에 올려 사장님이 직접 확인할 때도 꺼진다 — 그때는 Build Settings 의 Development Build 를 켠다.
        static bool DevAllowed => Application.isEditor || Debug.isDebugBuild;
        /// 화면을 찍는 동안(Editor/ShotTour) 판 구석의 「F1」 표시를 숨긴다 — 가게 스크린샷에 개발자 표시가 찍혔다 (09-16)
        //  public — 에디터 도구는 다른 어셈블리(Assembly-CSharp-Editor)라 internal 은 안 보인다
        public static bool HideDevHint;

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

        /// <summary>
        /// 🔴 **타이틀 화면** (09-04 사장님). 게임을 켜면 제일 먼저 나온다.
        ///
        /// 전에는 켜자마자 설명 화면(처음)이나 판 고르기(두 번째부터)로 떨어졌다 —
        /// **"게임을 켰다"는 순간이 없었다.** 앱으로 낼 거면 이 자리가 있어야 한다.
        /// </summary>
        bool _title = true;
        bool _settings;                    // 설정 화면
        /// 설정에 **어디서** 들어왔나 — 0 타이틀 · 1 판 · 2 판 고르기.
        /// 이게 없으면 판을 하다 설정에 들어갔을 때 타이틀로 튕겨 나간다.
        int _settingsFrom;
        bool _askWipe;                     // "새로시작" 이 진행을 지워도 되는지 묻는 중
        const string MuteKey = "snakeMute";
        const string VolKey  = "snakeVol";
        bool _mute;
        /// 소리 크기 0~1. 🔴 음소거와 **따로** 둔다 —
        /// 음소거를 풀었을 때 원래 크기로 돌아와야 하기 때문이다.
        float _volume = 1f;
        /// 소리 조절이 펼쳐져 있나 (판 화면 위쪽)
        bool _volPanel;
        bool _volDrag;

        /// <summary>
        /// 🔴 **시작 연출** (09-04 사장님: "슬라임이 쏙 들어가는 연출").
        /// 누르면 곧바로 안 넘어간다 — 타이틀의 슬라임이 홈으로 들어가고,
        /// 그 빨려듦이 끝나면서 게임으로 넘어간다. **탈출이 곧 시작**이다.
        /// </summary>
        float _startAt = -1f;
        int _startWhat;              // 0 새로 하기 · 1 이어하기
        const float StartShow = 1.25f;

        // ---- 손가락 조작 ----
        // 🔴 휴대폰엔 키보드가 없다. 그리고 한 판에 100걸음을 넘기기도 하므로
        //    스와이프만 두면 손이 아프다 — 방향 패드를 같이 둔다.
        bool Touchy => Application.isMobilePlatform || Input.touchSupported;
        Vector2 _swipeFrom;
        bool _swiping;
        float _uiScale = 1f;

        /// <summary>
        /// 글자 크기를 정수 픽셀로. 🔴 **11의 배수로 떨어뜨리던 것은 뺐다** (09-12) —
        /// 그건 도트 글꼴(갈무리11)이 11픽셀에 맞춰 그려져서였다. Pretendard 는 아무 크기나 된다.
        /// ⚠️ 그래서 같은 값을 줘도 글자가 전보다 조금 작거나 클 수 있다 (예: 16 → 전엔 11, 이제 16).
        /// 한글 받침이 뭉개지지 않게 10 아래로는 안 내린다.
        /// </summary>
        static int Px(float want) => Mathf.Max(10, Mathf.RoundToInt(want));
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
        //  🔴 미끄러진 칸마다 걸음 구간을 이만큼 늘린다.
        //     ⚠️ 낙하(√n)와 달리 **칸에 그대로 비례**한다 — 미끄러짐은 등속이라
        //        열 칸을 세 칸보다 확실히 오래 가야 "쭉 밀렸다"로 읽힌다.
        const float SlideCell = 0.55f;
        float SlideSpan => _slid <= 1 ? 0f : (_slid - 1) * SlideCell;

        void Awake()
        {
            //  🔴 시범은 여기서 아무것도 안 한다. SetupDemo 가 세운다 —
            //     본판의 카메라를 뺏거나 진행을 덮어쓰면 안 된다.
            if (_demo) return;
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
            //  🔴 카메라 기준은 이제 **굴마다** 잰다 — MeasureChapter() 가 Load() 안에서 한다.
            //     전에는 여기서 파일 전체를 훑었다. 그래서 19x11 피날레 하나 때문에
            //     굴 1의 10x8 판이 쪼그라들었다 (09-08 사장님이 짚으신 것).
            _setW = _setH = 0;
            _gravity = _set.gravity;
            LoadProgress();
            //  🔴 켜면 **타이틀부터**. 설명은 "새로시작" 을 고른 사람에게만 보여준다 —
            //     두 번째부터 켠 사람에게 설명을 다시 들이밀 이유가 없다.
            _mute = PlayerPrefs.GetInt(MuteKey, 0) == 1;
            _volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolKey, 1f));
            _title = true;
            _intro = false;
            _menu  = false;

            //  🔴 안내판에서 돌 **진짜 게임**을 하나 세운다 (09-03 사장님).
            //     본판과 겹치지 않게 멀리 놓고, 제 카메라로만 비춘다.
            //
            //  🔴 **꺼둔 채로 만든다.** AddComponent 는 그 자리에서 Awake 를 부르는데,
            //     _demo 를 나중에 켜면 시범이 본판 Awake 를 통째로 돌면서
            //     **또 시범을 만든다 — 무한 재귀다.**
            var gg = new GameObject("GuideDemo");
            gg.SetActive(false);
            gg.transform.SetParent(transform, false);
            _guide = gg.AddComponent<SnakeController>();
            _guide._demo = true;

            _guideCam = new GameObject("GuideCam").AddComponent<Camera>();
            _guideCam.transform.SetParent(gg.transform, false);
            _guideCam.orthographic = true;
            _guideCam.clearFlags = CameraClearFlags.SolidColor;
            _guideCam.backgroundColor = StageBg;
            _guideCam.depth = _cam.depth + 1;
            gg.SetActive(true);

            Load(NextUncleared());
        }

        /// 테스트 맵 하나. 🔴 판 설계가 아니다 — 움직임만 보려고 둔 것이다.
        /// 넓은 빈 방 + 기둥 몇 개 + 먹이 여럿.

        SnakeLevelJson Def => _set.levels[_index];

        /// 이어할 게 있나. 한 판이라도 깼으면 이어하기가 열린다.
        bool HasSave => _recs != null && _recs.Count > 0;

        /// <summary>
        /// 🔴 **아직 안 깬 첫 판.** 이어하기가 여기로 바로 간다 (09-04 사장님).
        /// 목록을 거치지 않는다 — 이어하려는 사람은 이미 어디까지 왔는지 안다.
        /// 다 깼으면 마지막 판으로.
        /// </summary>
        int NextUncleared()
        {
            int i = 0;
            while (i < _set.levels.Length && Cleared(_set.levels[i].id)) i++;
            return Mathf.Clamp(i, 0, _set.levels.Length - 1);
        }

        int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < _set.levels.Length; i++) if (_set.levels[i].id == id) return i;
            Debug.LogWarning("[길] 모르는 방: " + id);
            return -1;
        }

        /// 🔴 그 판이 몇 번째 굴인가. 자료에 없으면(튜토리얼 1-0) 1로 본다.
        static int ChapterOf(SnakeLevelJson j) => Mathf.Max(1, j == null ? 0 : j.chapter);

        /// 판 자료에 있는 마지막 굴 번호.
        int MaxChapter()
        {
            int m = 1;
            foreach (var lv in _set.levels) m = Mathf.Max(m, ChapterOf(lv));
            return m;
        }

        /// 판 고르기에서 굴을 넘긴다. 끝에서 멈춘다 (돌아 나오지 않는다).
        void MenuChapter(int d)
        {
            if (_menuCh <= 0) _menuCh = ChapterOf(Def);
            _menuCh = Mathf.Clamp(_menuCh + d, 1, MaxChapter());
        }

        /// <summary>
        /// 🔴 **카메라 배율의 기준을 그 굴 안에서 잰다.**
        /// 파일 전체에서 제일 큰 판에 맞추면 19x11 피날레 하나가
        /// 굴 1의 10x8 판을 쪼그라뜨린다. 굴마다 판 크기가 다르니 기준도 굴마다 달라야 한다.
        /// 구경용 판도 센다 — 그것만 크면 같은 굴의 다른 판이 다 작아진다.
        /// </summary>
        void MeasureChapter(int ch)
        {
            _setW = _setH = 0;
            if (_set == null || _set.levels == null) return;
            foreach (var lv in _set.levels)
            {
                if (lv == null || lv.grid == null || lv.grid.Length == 0) continue;
                if (ChapterOf(lv) != ch) continue;
                if (lv.grid[0].Length > _setW) _setW = lv.grid[0].Length;
                if (lv.grid.Length > _setH) _setH = lv.grid.Length;
            }
        }

        void Load(int i)
        {
            _volPanel = false;
            _index = Mathf.Clamp(i, 0, _set.levels.Length - 1);
            //  🔴 이 판이 속한 굴로 카메라 기준을 다시 잡는다. 판마다 굴이 다르므로
            //     Awake 에서 한 번 재고 마는 것으로는 안 된다.
            MeasureChapter(ChapterOf(_set.levels[_index]));
            _menuCh = 0;
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
            //  세로 폰이면 안내판이 **아래 띠**라 판은 위쪽을 쓴다 (09-12). Guide() 가 매 프레임 다시 맞춘다.
            if (_cam != null)
                _cam.rect = !Def.tutorial ? new Rect(0f, 0f, 1f, 1f)
                          : Tall(Screen.width, Screen.height) ? new Rect(0f, GuideH, 1f, 1f - GuideH)
                                                              : new Rect(0f, 0f, 1f - GuideW, 1f);

            _L = SnakeLevels.ToLevel(Def, _gravity);
            ApplyCaveLook(ChapterOf(Def));   // 🔴 BuildBoard 보다 먼저 — 칠할 색을 굴에 맞춘다
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
            if (_demo || _mute || _volume <= 0.001f) return;
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                // 세 음이 올라간다 — A5 · C#6 · E6
                _dings = new[] { MakeDing(880f, 0.5f), MakeDing(1109f, 0.5f), MakeDing(1319f, 0.7f) };
            }
            _audio.PlayOneShot(_dings[Mathf.Clamp(i, 0, 2)], _volume);
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
                    case '⇄': d = SnakeEngine.Dir.Swap; break;
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
            if (_demo) return;
            EndRun();                                  // 앞 판을 안 깨고 넘어갔어도 남긴다
            _run = new SnakeLog.Run { level = Def.id, best = Def.best };
            _runStart = Time.time;
        }

        void EndRun()
        {
            if (_demo) return;
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
            if (_demo) return;
            var sb = new System.Text.StringBuilder();
            foreach (var kv in _recs)
            {
                if (sb.Length > 0) sb.Append(',');
                sb.Append(kv.Key).Append(':').Append(kv.Value);
            }
            PlayerPrefs.SetString(ProgressKey, sb.ToString());
            PlayerPrefs.Save();
        }

        /// 지금 핵이 있는 방에 카메라를 맞춘다
        void AimCamera(bool snap)
        {
            // 🔴 **늘 판 전체를 잡는다.** 카메라는 판이 바뀔 때 말고는 안 움직인다 (rev.10)
            float asp = Mathf.Max(0.1f, _cam.aspect);
            float cx = _L.W * 0.5f, cy = -_L.H * 0.5f;
            float halfW = _L.W * 0.5f, halfH = _L.H * 0.5f;
            //  시범은 본판에서 멀리 떨어뜨려 놓았다 — 카메라도 그만큼 옮긴다
            cx += _org.x; cy += _org.y;
            //  시범은 판이 작으니 여백도 작게. 안 그러면 무대 한가운데 점처럼 보인다.
            float mg = _demo ? 0.45f : 0.9f;
            float fit = Mathf.Max(halfH + mg, (halfW + mg) / asp);
            // 작은 화면에선 읽기를 택한다 (2026-08-29)
            //  🔴 **한 묶음 안에서는 칸 크기가 안 바뀌어야 한다** (09-05 사장님:
            //     "27,28,31,41은 갑자기 카메라가 작아진다 — 맵이 작아보여").
            //     기준이 20x12 로 못박혀 있어서 그보다 높은 판만 축소됐다 —
            //     세로 15칸짜리는 15x7 짜리보다 칸이 40% 작게 보인다. 판마다 튄다.
            //     기준을 **이 묶음에서 제일 큰 판**으로 잡으면 모두 같은 배율이 된다.
            //     작은 판은 여백이 넓어질 뿐, 칸 크기는 그대로다.
            float refW = _setW > 0 ? _setW : BoardW, refH = _setH > 0 ? _setH : BoardH;
            float refFit = Mathf.Max(refH * 0.5f + 0.9f, (refW * 0.5f + 0.9f) / asp);
            //  🔴 시범은 **기준 판 크기(20×12)를 안 본다.** 그걸 최소 배율로 삼으면
            //     5×3 짜리 시범이 큰 판 기준으로 축소돼 점처럼 작아진다 (09-03 사장님 화면).
            float size = Mathf.Max(_demo ? 0f : refFit, fit);
            if (Screen.height / (2f * size) < 30f) size = fit;

            //  ⛔ 배율·자리를 **도트 격자(한 칸 32도트)에 맞추던 것**(SnapSize/SnapPos)을 뺐다 (09-12).
            //     도트 그림은 rev.8 에 버렸는데 이것만 남아, 배율을 정수 배로 **내림**해서
            //     판이 필요보다 작게 나오고 있었다. 지금 도형은 전부 부드러운 가장자리라 번질 것이 없다.
            _camWant = new Vector3(cx, cy, -10);
            _camSizeWant = size;
            if (snap) { _cam.transform.position = _camWant; _cam.orthographicSize = _camSizeWant; }
        }

        /// 🔴 둥근 테두리 둘이 맞닿으면 이음매에 홈이 남는다 — 그 자리를 메운다.
        ///    테두리와 **같은 차례(-1)** 에 둬야 위에 겹쳐 뜨지 않는다.
        void RimBridge(int cell, int door, Vector3 scale, Vector2 offset)
        {
            var sr = NewSprite("RimBridge", -1);
            sr.transform.position = CellPos(cell) + offset;
            sr.transform.localScale = scale;
            _holeEdgeExtra.Add((door, sr));
        }

        /// <summary>
        /// 홈 테두리 색. 🔴 **여기 한 곳에서만** 만든다 —
        /// 테두리·이음매가 따로 칠해지면 이음매만 색이 어긋나 끊겨 보인다.
        ///
        /// 형광이라 더 밝힐 수가 없다. 어둡게 깔았다 밝히는 쪽으로 숨을 쉰다.
        /// </summary>
        Color RimColor(int door, bool spent)
        {
            var rim = RailOf(door);
            if (spent) return Color.Lerp(Color.Lerp(RailDimOf(door), rim, 0.5f), SpentCol, SuckT);
            //  🔴 홈마다 **위상을 어긋내고 진폭을 절반**으로 (09-16 사장님: "홈 들어갈때마다 다른 홈들이 번쩍거림").
            //     전에는 안 채운 홈이 **전부 같은 박자**로 24% 씩 오르내려, 한 홈에 몸을 넣는 순간
            //     나머지 홈이 한 덩어리로 출렁여 번쩍이는 것으로 보였다.
            //     (Wiki_gm/game-feel.md 「애니는 자리마다 빠르기를 다르게」 — 우리가 그걸 어기고 있었다)
            float br = 0.88f + 0.12f * (0.5f + 0.5f * Mathf.Sin(Time.time * 1.15f + door * 1.9f));
            rim = new Color(rim.r * br, rim.g * br, rim.b * br, 1f);
            // 🔴 힌트 1 — 먼저 채울 홈만 깜빡인다. "왼쪽 아래요" 라고 쓰는 것보다 빠르다.
            if (_hint >= 1 && door == _hintDoor)
                rim = Color.Lerp(rim, Color.white, 0.35f + 0.35f * Mathf.Sin(Time.time * 4f));
            return rim;
        }

        /// <summary>
        /// 🔴 **출구에서 스르르 올라가는 불빛** (09-03 사장님).
        /// 홈마다 몇 개를 띄워 홈 **안에서** 아래에서 위로 흐르게 한다.
        ///
        /// 밖으로 내보내면 벽을 뚫는다 — 같은 세로줄에서 홈이 이어진 만큼만 오른다.
        /// 문이 열리면 멈춘다. 다 쓴 출구는 부를 일이 없다.
        /// </summary>
        void MakeMotes()
        {
            _motes.Clear();
            if (_L == null || _L.Doors == null) return;
            for (int di = 0; di < _L.Doors.Count; di++)
            {
                var cells = _L.Doors[di].Cells;
                if (cells == null || cells.Count == 0) continue;
                int n = Mathf.Clamp(cells.Count / 2, 2, 5);
                for (int k = 0; k < n; k++)
                {
                    var sr = NewSprite("Mote", 1);
                    sr.sprite = PixelSprites.Disc();
                    _motes.Add(new Mote { sr = sr, door = di, t = k / (float)n });
                    Respawn(_motes[_motes.Count - 1], cells);
                }
            }
        }

        /// 몸에서 떠오르는 김. 판마다 몇 개를 만들어 돌려 쓴다.
        void MakePuffs()
        {
            _puffs.Clear();
            for (int i = 0; i < PuffCount; i++)
            {
                var sr = NewSprite("Puff", 8);        // 머리(6)·거품(7) 위
                sr.sprite = PixelSprites.Disc();
                sr.enabled = false;
                _puffs.Add(new Puff { sr = sr, t = 1f });   // 첫 틱에 흩어져 나온다
            }
        }

        void MakeBubbles()
        {
            _bubbles.Clear();
            for (int i = 0; i < BubbleCount; i++)
            {
                //  🔴 **머리(6)보다 위**여야 한다. 아래에 두면 길이 1 일 때
                //     거품이 전부 머리 안에 있어서 하나도 안 보인다 (09-03 사장님).
                var sr = NewSprite("Bubble", 7);
                sr.sprite = PixelSprites.Disc();
                sr.enabled = false;
                _bubbles.Add(new Bubble { sr = sr });
                BubbleFrom(_bubbles[i]);
                //  🔴 **흩어서** 시작한다. 다 같이 0 에서 출발하면 한꺼번에 떠올라
                //     부글부글이 아니라 한 번 번쩍이는 것으로 보인다.
                _bubbles[i].t = Random.value;
            }
        }

        void BubbleFrom(Bubble b)
        {
            b.seg  = Random.Range(0, Mathf.Max(1, _st == null ? 1 : _st.Length));
            b.x    = Random.Range(-0.5f, 0.5f);
            b.t    = 0f;
            b.sp   = Random.Range(0.55f, 1.30f);
            //  🔴 크기를 **고르게 두지 않는다.** 다 같으면 규칙적인 무늬로 보인다 —
            //     끓는 것은 작은 게 잔뜩에 가끔 큰 게 하나 올라온다.
            b.size = Random.value < 0.16f ? Random.Range(0.11f, 0.16f)
                                          : Random.Range(0.035f, 0.085f);
        }

        void TickBubbles()
        {
            float dt = Time.deltaTime;
            bool hide = BornT < 1f || _st == null || _segPos.Count == 0;
            foreach (var b in _bubbles)
            {
                if (b.sr == null) continue;
                if (hide) { b.sr.enabled = false; continue; }

                //  🔴 삼킨 조각이 든 마디는 **더 바쁘게** 부글거린다 — 소화 중이다
                bool digest = _st.Pg > 0 && b.seg == _st.Length - 1;
                //  초록 쪽이 이미 빨라졌으니 소화 중일 때는 조금만 더 올린다
                b.t += dt * b.sp * (digest ? 1.25f : 1f);
                if (b.t >= 1f || b.seg >= _st.Length || b.seg >= _segW.Count) BubbleFrom(b);
                if (b.seg >= _segPos.Count || b.seg >= _segW.Count) { b.sr.enabled = false; continue; }

                //  🔴 마디의 **지금** 크기·자리를 매 프레임 다시 읽는다.
                //     한 번 박아두면 몸이 출렁일 때 거품만 제자리에 남아 밖으로 샌다.
                float w = _segW[b.seg], h = _segH[b.seg];
                float cx = _segPos[b.seg].x;
                float cy = _segPos[b.seg].y + (b.seg < _segDy.Count ? _segDy[b.seg] : 0f);

                //  넉넉히 안쪽으로 — 모서리가 둥글어서 끝까지 채우면 밖으로 삐져나온다
                b.sr.enabled = true;
                b.sr.transform.position =
                    new Vector3(cx + b.x * w * 0.52f, cy + (b.t - 0.5f) * h * 0.62f, -0.01f);

                //  🔴 **터진다.** 올라오며 커지다가 끝에서 확 부풀며 사라진다.
                //     sin 으로 부드럽게 여닫으면 방울이 뜨는 게 아니라 숨쉬는 것으로 보인다.
                float fin = Mathf.Clamp01(b.t / 0.12f);              // 나타나고
                float pop = Mathf.Clamp01((b.t - 0.72f) / 0.28f);    // 끝에서 터진다
                float grow = fin * (1f - pop);
                float sc = b.size * (0.55f + 0.45f * Mathf.Clamp01(b.t / 0.72f))
                                  * (1f + 0.75f * pop);
                b.sr.transform.localScale = new Vector3(sc, sc, 1);

                //  🔴 몸보다 **어둡다.** 밝게 하면 표면 위에 얹힌 것으로 보인다 —
                //     안쪽에 있는 것은 몸을 통과해서 보이니 한 단 가라앉아야 한다
                //     (09-03 사장님: "안쪽에서 부글거리는 느낌이니 색이 좀 달라야").
                //     같은 초록을 어둡게만 한다. 색을 바꾸면 다른 물건으로 보인다.
                //  담고 있는 것보다 한 단 어둡다 — 조각이 든 마디에서는 주황이 어두워진다
                var bc = (digest ? FoodCol : BodyCol) * 0.58f;
                bc.a = 0.42f * grow;
                b.sr.color = bc;
            }
        }

        void PuffFrom(Puff f)
        {
            //  🔴 지금 몸 어딘가에서 떨어져 나온다. 자리를 **그때 박아둔다** —
            //     몸을 계속 따라다니면 김이 아니라 몸에 붙은 장식이 된다.
            int n = Mathf.Min(_st.Length, _segPos.Count);
            if (n <= 0) { f.t = 0f; return; }
            var at = _segPos[Random.Range(0, n)];
            f.p    = at + new Vector2(Random.Range(-0.28f, 0.28f), Random.Range(-0.20f, 0.16f));
            f.t    = 0f;
            f.sp   = Random.Range(0.55f, 1.00f);
            f.dx   = Random.Range(-0.16f, 0.16f);
            f.size = Random.Range(0.09f, 0.17f);
        }

        void TickPuffs()
        {
            float dt = Time.deltaTime;
            //  아직 태어나는 중이면 안 낸다 — 몸이 다 나오기도 전에 김부터 나면 이상하다
            bool hide = BornT < 1f || _st == null || _segPos.Count == 0;
            foreach (var f in _puffs)
            {
                if (f.sr == null) continue;
                if (hide) { f.sr.enabled = false; continue; }
                f.t += dt * f.sp;
                if (f.t >= 1f) PuffFrom(f);
                f.sr.enabled = true;
                f.sr.transform.position = f.p + new Vector2(f.dx * f.t, PuffRise * f.t);
                float sc = f.size * (1f - 0.55f * f.t);
                f.sr.transform.localScale = new Vector3(sc, sc, 1);
                //  나타났다 사라진다 — 뚝 끊기면 튀어나온 점으로 보인다
                var pc = Color.Lerp(BodyCol, Color.white, 0.35f);
                pc.a = 0.42f * Mathf.Sin(f.t * Mathf.PI);
                f.sr.color = pc;
            }
        }

        void Respawn(Mote m, List<int> cells)
        {
            int c = cells[Random.Range(0, cells.Count)];
            //  같은 세로줄에서 홈이 이어진 맨 위 칸까지가 오르는 구간이다
            int top = c;
            while (_doorOf.TryGetValue(top - _L.W, out var up) && up == m.door) top -= _L.W;
            m.x  = CellPos(c).x + Random.Range(-0.24f, 0.24f);
            m.y0 = CellPos(c).y - 0.42f;
            m.y1 = CellPos(top).y + 0.42f;
            m.sp = Random.Range(0.30f, 0.55f);
            m.t -= Mathf.Floor(m.t);
        }

        void TickMotes()
        {
            //  🔴 SyncViews 가 매 프레임 부르는 자리다. dt 를 받아 오려면
            //     호출 자리 넷을 다 고쳐야 해서, 여기서 직접 읽는다.
            float dt = Time.deltaTime;
            for (int i = 0; i < _motes.Count; i++)
            {
                var m = _motes[i];
                if (m.sr == null) continue;
                bool spent = (_st.Dm & (1 << m.door)) != 0;
                if (spent) { m.sr.enabled = false; continue; }
                m.sr.enabled = true;

                m.t += dt * m.sp;
                if (m.t >= 1f) Respawn(m, _L.Doors[m.door].Cells);

                float y = Mathf.Lerp(m.y0, m.y1, m.t);
                m.sr.transform.position = new Vector3(m.x, y, 0f);
                //  나타났다 사라진다 — 끝에서 뚝 끊기면 "튀어나온 점"으로 보인다
                float a = Mathf.Sin(m.t * Mathf.PI);
                float s = 0.09f + 0.05f * a;
                m.sr.transform.localScale = new Vector3(s, s, 1f);
                var col = RailOf(m.door);
                col.a = 0.60f * a * a;
                m.sr.color = col;
            }
        }

        /// <summary>
        /// 🔴 둥근 네모 둘이 맞닿으면 이음매에 홈이 남는다 — 그 자리를 메운다.
        /// 벽 덩어리의 **바깥선만** 둥글고 속은 메워져야 한 덩어리로 보인다.
        /// z 를 뒤로 물려 둔다 — 칸과 같은 차례(-3)라 앞뒤를 못박아야 안 떤다.
        /// </summary>
        void WallBridge(int x, int y, Vector3 scale, Vector2 offset)
        {
            var sr = NewSprite("WallBridge", -3);
            var p = CellXY(x, y) + offset;
            sr.transform.position = new Vector3(p.x, p.y, 0.002f);
            sr.transform.localScale = scale;
            sr.color = Rock;
        }

        /// <summary>
        /// 🔴 판 **맨 바깥 칸**을 배경으로 흘려보낸다 (09-03 사장님).
        /// 배경색을 칸 위에 덮되, 바깥쪽이 꽉 차고 안쪽으로 갈수록 투명해진다.
        ///
        /// 벽 윗면 띠(-2)보다 **앞**에 둔다 — 벽만 녹고 띠가 남으면 띠만 떠 보인다.
        /// </summary>
        void EdgeFade(int x, int y, float turn)
        {
            var sr = NewSprite("EdgeFade", -2);
            sr.sprite = PixelSprites.Fade();
            var p = CellXY(x, y);
            sr.transform.position = new Vector3(p.x, p.y, -0.002f);
            sr.transform.rotation = Quaternion.Euler(0, 0, turn);
            sr.transform.localScale = new Vector3(1.02f, 1.02f, 1);   // 이음매가 새지 않게
            sr.color = BgCol;
        }

        void AddBridge(int a, int b, Vector3 scale, Vector2 offset)
        {
            var sr = NewSprite("HoleBridge", 0);
            sr.transform.position = CellPos(a) + offset;   // Vector2 + Vector2
            sr.transform.localScale = scale;
            sr.color = HoleCol;
            _bridges.Add((a, b, sr));
        }

    }
}
