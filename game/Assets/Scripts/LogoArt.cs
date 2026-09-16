using System.Collections.Generic;
using UnityEngine;

namespace SlimeEscape
{
    /// <summary>
    /// 🔴 **로고를 글자가 아니라 모양으로 그린다** (09-16 사장님: "SlimeEscape 를 혹시 텍스트가 아니고 모양으로 만들 수 있어?").
    ///
    /// 그전에는 폰트로 찍은 글자 밑에 방울·띠를 **덧붙였다.** 세 번 고쳐도 계속 어색했던 까닭이 그것이다 —
    /// 글자는 글자대로, 장식은 장식대로라 둘이 따로 놀았다 (압정 → 철봉에 나사 → 빨랫줄에 구슬).
    ///
    /// 여기서는 열한 글자를 **획(선·꺾은선) 목록**으로 정의하고, 그 획을 **끝이 둥근 굵은 획**으로 그린다.
    ///
    /// 🔴 **슬라임 느낌은 모양이 아니라 색으로 낸다** (09-16 사장님).
    ///    - "슬라임 같은 느낌은 빼고 문자 하나가 라운드로 이어지게 / 동글동글한 느낌 빼고" →
    ///      흘러내리는 **방울을 전부 들어내고**, 획이 만나는 자리가 **녹아 뭉치던 것**을 확 줄였다.
    ///      (방울은 글자를 바꿔 버리기까지 했다 — a 가 q 로, E 가 Ę 로 읽혔다)
    ///    - "슬라임 색으로 바꿔줘 슬라임 같은 느낌으로 빛반사 그것도 좀 주고" →
    ///      **색까지 구워서** 낸다. 자세한 것은 <see cref="Build"/> 의 두 번째 훑기.
    ///
    /// 🔴 **획은 곡선으로 흐른다** (09-16 사장님: "좀 더 라운드로").
    ///    S·e·s·c·a 는 점 여덟아홉 개를 **직선으로 이은 다각형**이라, 둥근 글자인데도 모서리마다 각이 졌다.
    ///    같은 점들을 지나되 사이를 **휘어서 잇는다**. 점이 둘뿐인 곧은 획(l·i·E)은 직선 그대로 둔다.
    ///
    /// 폰트에 안 기댄다 — 글꼴을 바꿔도 로고는 그대로다. 글자 하나도 글꼴 서브셋에 안 넣어도 된다.
    /// ⚠️ 그리는 쪽은 **흰색으로 곱해야** 한다 — 색이 그림에 들어 있으므로 딴 색을 곱하면 물든다.
    /// </summary>
    public static class LogoArt
    {
        //  글자 한 칸의 좌표: x 는 0..adv, y 는 밑줄(0) ~ 대문자 높이(1). 소문자 허리는 0.62, 꼬리는 -0.24.
        struct Glyph
        {
            public float adv;              // 다음 글자까지의 폭
            public Vector2[][] strokes;    // 꺾은선 여러 개
            public Vector2[] dots;         // i 의 점 같은 것 (x, y)
        }

        static Vector2 P(float x, float y) => new Vector2(x, y);

        static readonly Dictionary<char, Glyph> Font = new Dictionary<char, Glyph>
        {
            ['S'] = new Glyph { adv = 0.62f, strokes = new[] { new[] {
                P(0.56f,0.82f), P(0.44f,0.97f), P(0.24f,0.97f), P(0.11f,0.82f), P(0.15f,0.66f),
                P(0.40f,0.57f), P(0.55f,0.45f), P(0.53f,0.24f), P(0.34f,0.10f), P(0.13f,0.18f) } } },

            ['l'] = new Glyph { adv = 0.26f, strokes = new[] { new[] { P(0.13f,1.00f), P(0.13f,0.02f) } } },

            //  ⚠️ 점이 획에 닿아 **l 로 보였다** (09-16). 0.60/0.88 도, 0.56/0.94 도 붙었다 —
            //     획 반지름(0.072) + 점 반지름 + 합쳐지는 폭이 다 더해지기 때문이다.
            //     획을 허리 밑으로 낮추고 점은 **대문자 높이 위**로 올린다. 사이가 0.5 는 떠야 떨어져 보인다.
            ['i'] = new Glyph { adv = 0.26f, strokes = new[] { new[] { P(0.13f,0.52f), P(0.13f,0.02f) } },
                                dots = new[] { P(0.13f,1.02f) } },

            //  🔴 **한 붓으로 잇는다** (09-16, 세 번째 시도). 기둥과 아치를 따로 두고 겹치기를 기대했더니
            //     두 번 다 벌어져 **ıη 처럼 갈라져** 보였다 (0.56 에서도, 기둥 꼭대기 0.62 에서도).
            //     왼쪽 기둥 밑 → 위 → 첫 아치 → 가운데 기둥 → 위 → 둘째 아치 → 오른쪽 기둥으로
            //     **끊기지 않는 꺾은선 하나**로 그린다. 끊어질 자리가 아예 없어진다.
            //     (가운데 기둥은 오르내리며 두 번 지나가지만, 같은 선 위라 굵기가 그대로다)
            ['m'] = new Glyph { adv = 0.84f, strokes = new[] { new[] {
                P(0.09f,0.02f), P(0.09f,0.62f),
                P(0.18f,0.66f), P(0.31f,0.63f), P(0.37f,0.50f), P(0.37f,0.02f),
                P(0.37f,0.50f), P(0.37f,0.62f),
                P(0.46f,0.66f), P(0.59f,0.63f), P(0.65f,0.50f), P(0.65f,0.02f) } } },

            //  ⚠️ **e 의 속구멍이 메워져 o 로 보였다** (09-16 화면). 가로획이 원 안쪽을 지나는데
            //     획이 굵어 합치는 과정에서 구멍이 막혔다. 가로획을 **위로 올리고 짧게** 자르고,
            //     원을 키워 구멍을 남긴다.
            ['e'] = new Glyph { adv = 0.62f, strokes = new[] { new[] {
                P(0.13f,0.40f), P(0.49f,0.40f), P(0.52f,0.50f), P(0.40f,0.62f), P(0.24f,0.62f),
                P(0.11f,0.50f), P(0.10f,0.28f), P(0.22f,0.12f), P(0.41f,0.11f), P(0.52f,0.19f) } } },

            //  ⚠️ **모서리마다 작은 뿔이 났다** (09-16 사장님: "E 쪽이 너무 어색함").
            //     기둥은 1.00~0.02 인데 가로획은 0.98 과 0.04 에 있어, 기둥 끝이 가로획 **밖으로 삐져나왔다.**
            //     획 끝이 둥근 마감이라 그 삐져나온 조각이 뿔로 보이고, 이음매를 키울수록 살까지 붙어 도드라진다.
            //     🔴 끝점을 **딱 맞춘다** — 세 모서리에서 두 획이 정확히 만나면 뿔이 없어지고,
            //        이음매가 오히려 모서리를 깔끔한 라운드로 만들어 준다.
            //     가로획 높이도 l 과 같은 1.00 / 0.02 로 맞췄다 (E 만 작아 보이던 것).
            ['E'] = new Glyph { adv = 0.58f, strokes = new[] {
                new[] { P(0.11f,1.00f), P(0.11f,0.02f) },
                new[] { P(0.11f,1.00f), P(0.52f,1.00f) },
                new[] { P(0.11f,0.50f), P(0.50f,0.50f) },   // ⚠️ 짧고 위에 붙어 F 로 보였다 — 가운데로, 길게
                new[] { P(0.11f,0.02f), P(0.52f,0.02f) } } },

            ['s'] = new Glyph { adv = 0.52f, strokes = new[] { new[] {
                P(0.46f,0.52f), P(0.34f,0.62f), P(0.18f,0.61f), P(0.10f,0.50f), P(0.16f,0.39f),
                P(0.36f,0.33f), P(0.46f,0.24f), P(0.42f,0.13f), P(0.24f,0.10f), P(0.10f,0.16f) } } },

            ['c'] = new Glyph { adv = 0.54f, strokes = new[] { new[] {
                P(0.47f,0.50f), P(0.35f,0.61f), P(0.20f,0.59f), P(0.10f,0.45f), P(0.10f,0.28f),
                P(0.20f,0.14f), P(0.35f,0.12f), P(0.47f,0.22f) } } },

            //  a 도 같은 까닭으로 o 가 됐다 — 원을 키우고 기둥을 오른쪽 끝으로 물린다
            ['a'] = new Glyph { adv = 0.62f, strokes = new[] {
                new[] { P(0.50f,0.18f), P(0.38f,0.10f), P(0.20f,0.11f), P(0.10f,0.26f), P(0.11f,0.46f),
                        P(0.25f,0.60f), P(0.42f,0.58f), P(0.50f,0.46f) },
                new[] { P(0.50f,0.62f), P(0.50f,0.02f) } } },

            ['p'] = new Glyph { adv = 0.60f, strokes = new[] {
                new[] { P(0.12f,0.62f), P(0.12f,-0.22f) },
                new[] { P(0.12f,0.50f), P(0.25f,0.61f), P(0.41f,0.57f), P(0.48f,0.40f),
                        P(0.41f,0.20f), P(0.25f,0.12f), P(0.12f,0.20f) } } },
        };

        const string Word = "SlimeEscape";
        //  ⚠️ 글자 사이가 벌어져 **"Slim oEscape" 두 단어**로 읽혔다 (09-16 화면) — 좁힌다
        const float Gap = 0.035f;           // 글자 사이
        //  ⚠️ 획이 굵어 e·a 의 속구멍이 메워져 **o** 로 보였다 — 얇게
        const float Stroke = 0.072f;        // 획 반지름 (대문자 높이 기준)
        //  곡선 한 마디를 몇 조각으로 쪼개 그릴까. 크면 매끄럽고, 그만큼 굽는 데 오래 걸린다.
        const int SubDiv = 12;
        //  🔴 곡선이 **얼마나 휘는가** (09-16 사장님: "좀더 라운드"). 0.5 가 보통이고, 키울수록
        //     점과 점 사이가 크게 부풀어 둥글어진다. 너무 키우면 모양이 원래 점을 벗어나 불룩해진다.
        const float Curve = 0.72f;

        //  🔴 슬라임 색. 게임 안의 몸 색(SnakeController.BodyCol #6fd08c)과 **같은 것을 쓴다** —
        //     타이틀의 슬라임과 로고가 다른 초록이면 둘이 남남으로 보인다.
        public static readonly Color Body = new Color32(0x6f, 0xd0, 0x8c, 0xff);

        //  🔴 **구를 칠하듯** 한쪽을 밝게 한다 (09-17 사장님: "구를 그리면 밝은 부분에는
        //     살짝 밝은 색을 칠하잖아? 그런것 처럼 글자마다 조금씩").
        //  ⚠️ **좁고 센 흰 광이 아니다.** 그건 이미 한 번 물렸다 — 표면 위에 반짝이는 점을 얹었더니
        //     캐릭터에 없는 것이라 안 닮았다 ("이 빛반사가 아니고", 09-17).
        //     여기서는 밝은 쪽과 그늘 쪽 **두 색 사이를 넓고 천천히** 오간다. 흰색은 안 쓴다.
        static readonly Color Lite  = new Color32(0x9a, 0xde, 0xae, 0xff);   // 빛 받는 쪽
        static readonly Color Shade = new Color32(0x57, 0xa2, 0x6d, 0xff);   // 그늘
        //  빛은 **왼쪽 위**에서 온다. 게임 안에서 조각의 반짝임도 왼쪽 위에 얹혀 있다
        //  (SnakeController.Board 의 FoodShine: 가운데에서 -0.07, +0.08 만큼 옮겨 둔다).
        static readonly Vector2 Light = new Vector2(-0.42f, 0.91f).normalized;

        //  🔴 **거품이 슬라임다움의 정체다** (09-17 사장님: "이 빛반사가 아니고, 지금 캐릭터 슬라임 같은 느낌").
        //     처음엔 가장자리에 빛을 두르고 흰 광을 얹었다 — 캐릭터에 **없는 것**이라 안 닮았다.
        //     게임 속 몸은 `_segs[i].color = BodyCol` 로 **끝까지 한 색**이고(SnakeController.Motion),
        //     슬라임처럼 보이는 까닭은 **안에서 부글거리는 거품**이다.
        //     그 거품은 몸보다 **어둡다** — 09-03 사장님: "안쪽에서 부글거리는 느낌이니 색이 좀 달라야".
        //     밝게 하면 표면 **위에 얹힌 것**으로 보인다(SnakeController 의 TickBubbles 주석).
        //     그래서 같은 배합을 그대로 옮긴다 — 몸 색 × 0.58, 덮는 정도 0.42.
        public const float BubbleDim = 0.58f;   // 거품이 몸보다 얼마나 어두운가
        //  ⚠️ 게임과 같은 0.42 로 뒀더니 로고에서는 **거의 안 보였다** (09-17 화면) —
        //     몸은 칸 하나만큼 큰데 로고 획은 그보다 훨씬 가늘어서, 같은 진하기로는 묻힌다.
        public const float BubbleMix = 0.58f;   // 얼마나 진하게 비치는가
        //  ⚠️ 0.42 는 **너무 촘촘해** 글자가 통째로 점박이가 됐고, 0.62 는 **너무 성겨** 거품이 안 보였다.
        //     둘 사이로 잡는다 (09-17 화면 두 판).
        const float BubbleStep = 0.52f;         // 획을 따라 이만큼마다 하나 (글자 높이 기준)

        static Texture2D _tex;
        static Texture2D _mask;
        static float _aspect;

        /// 로고 그림의 가로세로비 (폭 ÷ 높이). 그릴 자리를 잡을 때 쓴다.
        public static float Aspect { get { Build(); return _aspect; } }

        /// 🔴 한 번 굽고 계속 쓴다. **색까지 들어 있다** — 그릴 때는 흰색으로 곱해야 한다.
        public static Texture2D Get() { Build(); return _tex; }

        /// <summary>
        /// 🔴 몸 모양만 담은 **흰색 판**. 윤기처럼 **덧칠해 밝힐 때** 쓴다.
        /// ⚠️ <see cref="Get"/> 에 흰색을 곱해서는 못 밝힌다 — GUI.color 는 **곱하기**라
        ///    색이 든 그림에 흰색을 곱하면 **초록 그대로**다.
        ///    09-17 에 그걸 모르고 윤기를 넣었다가, 여섯 장을 재 보니 가장 밝은 값이
        ///    107~109 로 꼼짝도 안 했다. 아예 안 밝아지고 있었다.
        /// </summary>
        public static Texture2D Mask() { Build(); return _mask; }

        /// 선 조각 하나. 🔴 차지하는 네모 칸을 **미리 적어 둔다** — 곡선으로 쪼개면 조각이 천 개가 넘어서,
        /// 픽셀마다 전부 재면 굽는 데 몇 초씩 걸린다. 멀리 있는 조각은 이 칸만 보고 건너뛴다.
        struct Seg
        {
            public Vector2 a, b;
            public float x0, x1, y0, y1;
        }

        /// <summary>
        /// 🔴 거품 하나가 뜰 **자리**. 그림에 굽지 않고 이 목록만 넘긴다 —
        /// 부글거리려면 움직여야 하는데, 구워 버리면 못 움직인다
        /// (09-17 사장님: "로고에 부글 거리는 에니도 넣어주고").
        ///
        /// 좌표는 로고 그림 안에서의 **비율**이다 — u 는 왼쪽 0 ~ 오른쪽 1,
        /// v 는 **위가 0** ~ 아래 1 (그리는 쪽이 위에서부터 재므로 여기서 맞춰 준다).
        /// r 은 그림 **높이**에 대한 비율이라, 로고를 얼마로 그리든 같은 비율로 커진다.
        /// ⚠️ r 은 그 자리에서 **몸 밖으로 안 새는 한계**다 — 이보다 크게 그리면 획 밖으로 삐져나온다.
        /// seed 는 자리마다 박자를 어긋내려고 둔다 (0~1). 다 같은 박자면 한 번 번쩍이는 것으로 보인다.
        /// </summary>
        public struct Spot { public float u, v, r, seed; }

        static Spot[] _spots;

        /// 거품이 뜰 자리들. 🔴 **굽지 않고 넘긴다** — 그리는 쪽이 매 프레임 띄운다.
        public static Spot[] Spots { get { Build(); return _spots; } }

        static void Build()
        {
            if (_tex != null) return;

            //  글자를 늘어놓으며 획을 **그림 좌표**로 모은다 (아직 픽셀이 아니라 글자 단위)
            var lines = new List<(Vector2 a, Vector2 b)>();
            var dots = new List<Vector2>();
            float pen = 0f;
            foreach (char ch in Word)
            {
                if (!Font.TryGetValue(ch, out var g)) continue;
                foreach (var line in g.strokes)
                {
                    var cur = Smooth(line);
                    for (int i = 0; i + 1 < cur.Count; i++)
                        lines.Add((new Vector2(pen + cur[i].x, cur[i].y),
                                   new Vector2(pen + cur[i + 1].x, cur[i + 1].y)));
                }
                if (g.dots != null) foreach (var d in g.dots) dots.Add(new Vector2(pen + d.x, d.y));
                pen += g.adv + Gap;
            }

            //  여백 — 획 굵기와 p 의 꼬리를 담을 만큼. 방울이 없어져 아래 여백이 확 줄었다.
            float padX = Stroke * 1.6f, top = 1.02f + Stroke * 1.6f;
            float bottom = -0.24f - Stroke * 1.6f;
            float wUnits = pen - Gap + padX * 2f, hUnits = top - bottom;
            _aspect = wUnits / hUnits;

            //  ⚠️ 빛 반사를 **이웃 픽셀을 봐서** 낸다. 그림이 거칠면 그 명암도 계단처럼 끊긴다 —
            //     200 이던 높이를 288 로 올렸다.
            const int H = 288;
            int W = Mathf.RoundToInt(H * _aspect);
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false)
                    { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                      hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[W * H];
            //  🔴 같은 모양의 **흰색 판**도 같이 만든다 — 덧칠해 밝힐 때 쓴다 (<see cref="Mask"/>).
            var mpx = new Color[W * H];

            float sc = H / hUnits;                       // 1 단위가 몇 픽셀인가
            float r = Stroke * sc;
            //  🔴 **뭉침은 여기서 나온다.** 이 값이 클수록 획이 만나는 자리에 살이 붙는다.
            //     0.9 배였던 것을 줄였다 (09-16 사장님: "동글동글한 느낌 빼고").
            //     0 으로 두면 이음매가 각져 보이므로 얇게만 남긴다.
            float k = r * 0.40f;
            float reach = r + k + 1.5f;                  // 이 칸 밖이면 이 조각은 볼 것도 없다

            //  🔴 픽셀 좌표로 **미리 바꿔 둔다.** 전에는 픽셀마다 a * sc 를 다시 곱하고 있었다.
            var segs = new Seg[lines.Count];
            for (int i = 0; i < lines.Count; i++)
            {
                Vector2 a = lines[i].a * sc, b = lines[i].b * sc;
                segs[i] = new Seg {
                    a = a, b = b,
                    x0 = Mathf.Min(a.x, b.x) - reach, x1 = Mathf.Max(a.x, b.x) + reach,
                    y0 = Mathf.Min(a.y, b.y) - reach, y1 = Mathf.Max(a.y, b.y) + reach };
            }
            var dotsPx = new Vector2[dots.Count];
            for (int i = 0; i < dots.Count; i++) dotsPx[i] = dots[i] * sc;


            //  ── 첫 번째 훑기: 픽셀마다 "테두리에서 얼마나 떨어졌나"를 잰다 ──
            //  🔴 색을 칠하려면 **이웃 값**이 있어야 해서(빛 반사), 한 번에 칠하지 않고 먼저 다 재 둔다.
            var dist = new float[W * H];
            //  🔴 그 자리가 **획 중심선에서 얼마나 벗어났는가.** 명암은 이 값으로 낸다.
            //  ⚠️ 거리장의 **기울기**로 내면 안 된다 — 획 한복판에서 방향이 튀어 톱니 줄무늬가 난다.
            //     09-17 에 두 번 물렸다: 처음엔 그냥 썼고, 다음엔 기울기 **크기**로 걸러 보려 했는데
            //     그 크기는 한복판에서도 0 이 아니라(방향만 꺾인다) 아무것도 못 걸렀다.
            var offs = new Vector2[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    //  그림 좌표 → 글자 좌표.
                    //  🔴 Texture2D 는 **y = 0 이 아래**다 (SetPixel 기준). 글자 좌표도 밑줄이 0 이므로
                    //     위에서 빼면 안 되고 **아래(bottom)에서 더해야** 한다.
                    //     처음에 top 에서 빼서 글자가 통째로 **상하로 뒤집혀** 나왔다 (09-16 화면: ƨliW oƷɒɔ...).
                    float ux = (x + 0.5f) / sc - padX;
                    float uy = bottom + (y + 0.5f) / sc;
                    var p = new Vector2(ux, uy) * sc;

                    float d = 1e9f;
                    //  가장 가까운 획의 **중심선 위 점**도 같이 찾는다 — 명암을 거기서 낸다
                    float best = 1e9f;
                    var bestCp = p;
                    for (int i = 0; i < segs.Length; i++)
                    {
                        var s = segs[i];
                        if (p.x < s.x0 || p.x > s.x1 || p.y < s.y0 || p.y > s.y1) continue;
                        var cp = SegClosest(p, s.a, s.b);
                        float dd = (p - cp).magnitude;
                        if (dd < best) { best = dd; bestCp = cp; }
                        d = SMin(d, dd - r, k);
                    }
                    for (int i = 0; i < dotsPx.Length; i++)
                    {
                        float dd = (p - dotsPx[i]).magnitude;
                        if (dd < best) { best = dd; bestCp = dotsPx[i]; }
                        d = SMin(d, dd - r * 1.02f, k);
                    }
                    int at0 = y * W + x;
                    dist[at0] = d;
                    offs[at0] = p - bestCp;
                }

            //  🔴 **거품 자리를 획을 따라 잡는다.** 몸 안에 들어가야 하므로 획의 **중심선 위**에만 둔다 —
            //     가장자리에 두면 몸 밖으로 새어 보인다.
            //     간격은 자리마다 달라야 "부글거리는 것"으로 읽힌다. 고르면 줄 세운 단추가 된다.
            //  🔴 자리마다 **몸 안에 남은 여유**를 같이 적어 준다 — 방금 잰 값이 곧 그것이다
            //     (테두리까지의 거리). 그리는 쪽이 이 한계 안에서만 부풀리면 절대 안 샌다.
            //  ⚠️ 글자 좌표에 sc 만 곱하면 픽셀 자리가 **안 맞는다** — 그림에는 여백(padX)이 있고
            //     밑줄이 bottom 만큼 내려가 있다. 그만큼 옮겨 줘야 획 위에 얹힌다.
            var off = new Vector2(padX * sc, -bottom * sc);
            var spots = new List<Spot>();
            {
                float acc = 0f, next = BubbleStep * 0.5f;
                int n = 0;
                foreach (var (ua, ub) in lines)
                {
                    float segLen = (ub - ua).magnitude;
                    if (segLen < 1e-6f) continue;
                    while (acc + segLen >= next)
                    {
                        float f = (next - acc) / segLen;
                        Vector2 at = Vector2.Lerp(ua, ub, f) * sc + off;
                        next += BubbleStep * Mathf.Lerp(0.70f, 1.40f, H01(n * 17 + 23));
                        n++;

                        int ix = Mathf.RoundToInt(at.x - 0.5f), iy = Mathf.RoundToInt(at.y - 0.5f);
                        if (ix < 0 || ix >= W || iy < 0 || iy >= H) continue;
                        float clear = -dist[iy * W + ix];          // 몸 안쪽이면 양수
                        //  획 끝처럼 얇은 자리는 건너뛴다 — 거품이 들어갈 데가 없다
                        if (clear < r * 0.45f) continue;
                        spots.Add(new Spot {
                            u = at.x / W,
                            v = 1f - at.y / H,                     // 🔴 그리는 쪽은 **위가 0** 이다
                            r = clear * 0.86f / H,                 // 높이 대비 비율 · 안 새는 한계
                            seed = H01(n * 31 + 7) });
                    }
                    acc += segLen;
                }
            }
            _spots = spots.ToArray();

            //  ── 두 번째 훑기: **캐릭터와 같은 방식**으로 칠한다 ──
            //  🔴 몸은 단색이다. 명암도 광택도 없다 — 게임 속 몸이 `_segs[i].color = BodyCol` 하나뿐인 것과 같다.
            //     슬라임처럼 보이게 하는 것은 표면의 빛이 아니라 **안에서 비치는 거품**이다.
            //  ⛔ 걷어낸 것: 가장자리 빛(Rim) · 속 그늘(Core) · 왼쪽 위에서 오는 흰 광
            //     (09-17 사장님: "이 빛반사가 아니고, 지금 캐릭터 슬라임 같은 느낌이어야 하거든")
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    float d = dist[i];
                    float a = Mathf.Clamp01(0.5f - d);
                    if (a <= 0.001f) { px[i] = new Color(0f, 0f, 0f, 0f); mpx[i] = px[i]; continue; }

                    //  🔴 거품은 굽지 않는다 — 움직여야 하므로 그리는 쪽이 띄운다.
                    //     (09-16 에 구워 봤더니 티끌처럼 흩뿌려져 부글거림이 아니라 **얼룩**으로 보였다)
                    Color c = Body;

                    //  🔴 **원통을 칠하듯** 한다 (09-17 사장님: "구를 그리면 밝은 부분에는 살짝 밝은 색을
                    //     칠하잖아? 그런것 처럼"). 획 중심선에서 **벗어난 쪽**이 곧 그 자리가 보는 쪽이고,
                    //     **얼마나 벗어났는지**가 곧 명암의 세기다 — 한복판은 몸 색, 가장자리로 갈수록 온전히.
                    //     중심선에서 저절로 0 으로 수렴하니 톱니가 생길 자리가 없다.
                    //
                    //  ⛔ 걷어낸 것: 거리장의 **기울기**로 방향을 재던 방식. 09-17 에 두 번 물렸다 —
                    //     ① 그냥 썼더니 m 이 빗살처럼 갈라지고 l·i 가운데에 줄이 섰다.
                    //     ② 기울기 **크기**로 걸러 보려 했더니 아무것도 안 걸러졌다. 그 크기는 한복판에서도
                    //        0 이 아니라(방향만 꺾인다) 늘 최댓값으로 잡혔기 때문이다.
                    var dv = offs[i];
                    float mag = dv.magnitude;
                    if (mag > 1e-4f)
                    {
                        //  -1~1 을 0~1 로 편다. 매끈하게 굴려 **천천히** 넘어가게 한다 —
                        //  거듭제곱으로 좁히면 그게 바로 반짝이는 점이 된다 ("이 빛반사가 아니고").
                        float lit = Vector2.Dot(dv / mag, Light) * 0.5f + 0.5f;
                        lit = lit * lit * (3f - 2f * lit);
                        c = Color.Lerp(Body, Color.Lerp(Shade, Lite, lit), Mathf.Clamp01(mag / r));
                    }

                    c.a = a;
                    px[i] = c;
                    mpx[i] = new Color(1f, 1f, 1f, a);
                }

            t.SetPixels(px);
            t.Apply();
            _tex = t;

            var m = new Texture2D(W, H, TextureFormat.RGBA32, false)
                    { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                      hideFlags = HideFlags.HideAndDontSave };
            m.SetPixels(mpx);
            m.Apply();
            _mask = m;
        }

        /// <summary>
        /// 🔴 꺾은선을 **휘어서 지나가게** 만든다 (09-16 사장님: "좀 더 라운드로").
        /// 원래 점들은 그대로 지나가고, 사이만 곡선으로 채운다 — 그래야 글자 모양이 안 변한다.
        /// 점이 둘뿐이면 **곧은 획이라는 뜻**이므로 손대지 않는다 (l·i·E 의 기둥과 가로획).
        /// </summary>
        static List<Vector2> Smooth(Vector2[] pts)
        {
            var outp = new List<Vector2>();
            if (pts.Length < 3) { outp.AddRange(pts); return outp; }
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Vector2 p0 = pts[Mathf.Max(i - 1, 0)];
                Vector2 p1 = pts[i];
                Vector2 p2 = pts[i + 1];
                Vector2 p3 = pts[Mathf.Min(i + 2, pts.Length - 1)];
                for (int s = 0; s < SubDiv; s++)
                    outp.Add(CatRom(p0, p1, p2, p3, s / (float)SubDiv));
            }
            outp.Add(pts[pts.Length - 1]);
            return outp;
        }

        /// <summary>
        /// 네 점을 보고 가운데 두 점 사이를 부드럽게 잇는 곡선.
        /// 🔴 휘는 정도(<see cref="Curve"/>)를 밖에서 정한다 — 0.5 면 보통 Catmull-Rom 이고,
        ///    키우면 두 점 사이가 더 크게 부풀어 **더 둥글어진다.**
        /// </summary>
        static Vector2 CatRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            Vector2 m1 = (p2 - p0) * Curve, m2 = (p3 - p1) * Curve;
            return (2f * t3 - 3f * t2 + 1f) * p1
                 + (t3 - 2f * t2 + t) * m1
                 + (-2f * t3 + 3f * t2) * p2
                 + (t3 - t2) * m2;
        }

        /// <summary>
        /// 선분 위에서 p 에 **가장 가까운 점**. 🔴 명암을 여기서 낸다 —
        /// 이 점에서 p 로 향하는 방향이 곧 그 자리 표면이 보는 쪽이다 (원통을 칠하는 것과 같다).
        /// </summary>
        static Vector2 SegClosest(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float len2 = Vector2.Dot(ab, ab);
            float h = len2 < 1e-5f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return a + ab * h;
        }

        /// 점에서 선분까지의 거리
        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float len2 = Vector2.Dot(ab, ab);
            float h = len2 < 1e-5f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return (p - (a + ab * h)).magnitude;
        }

        /// 두 모양을 합친다. kk 가 0 에 가까울수록 그냥 겹치고, 클수록 만나는 자리가 녹아 붙는다.
        static float SMin(float a, float b, float kk)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / kk);
            return Mathf.Lerp(b, a, h) - kk * h * (1f - h);
        }

        /// 0~1 사이 값 하나. 🔴 <see cref="PixelSprites"/> 가 쓰는 것과 **같은 식**이다 —
        /// 굽는 그림은 늘 같은 모양이어야 한다. 진짜 난수를 쓰면 켤 때마다 로고가 달라진다.
        static float H01(int n)
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }
    }
}
