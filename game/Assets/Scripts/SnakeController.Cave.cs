using UnityEngine;

namespace SlimeEscape
{
    /// <summary>
    /// 🔴 **굴 배경** (09-16 사장님: *"스테이지 선택 화면이 너무 뒷배경에 아무것도 없으니 허전하고,
    /// 메인 페이지 거기도 너무 허전함"* · *"뭔가 슬라임이 흐르는 동굴같은 느낌으로 연출해 줬으면 좋겠으"*).
    ///
    /// 그전에는 타이틀도 판 고르기도 **단색 한 겹**이었다 — 허전한 게 당연했다.
    /// 여기서 두 겹을 깐다:
    ///  ① **굴 벽** — 위아래에서 밀고 들어온 바위 덩어리. 굴 색(<c>rock</c>)을 아주 옅게 쓴다.
    ///  ② **흐르는 슬라임** — 천장에서 아래로 천천히 흘러내리는 줄기 몇 가닥.
    ///     게임의 말랑한 덩어리(<see cref="PixelSprites.Goo"/>)를 그대로 쓴다 — 몸통 + 매달린 방울.
    ///
    /// ⚠️ **눈을 방해하면 안 된다.** 판 고르기에서는 마디와 길을, 타이틀에서는 글자를 읽어야 한다.
    ///    그래서 전부 **아주 옅고 아주 느리다** — 알파 0.1 안팎, 한 화면 내려오는 데 40초쯤.
    ///    움직이는 게 눈에 띄면 그건 이미 너무 빠른 것이다.
    ///
    /// ⚠️ 초록은 슬라임만 쓴다(색 규칙 ③). 여기 흐르는 것도 **슬라임이므로** 초록이 맞다 —
    ///    다만 배경이니 몸 색보다 훨씬 옅게.
    /// </summary>
    public partial class SnakeController
    {
        //  굴마다 다른 그림이 되게 섞는 값. 같은 굴은 늘 같은 그림이다 (난수를 새로 뽑지 않는다).
        const int CaveSalt = 977;

        /// 천장에서 늘어지는 줄기 몇 가닥.
        /// ⚠️ 6 은 비었고 11 은 **물방울로 빽빽했다** (09-17 화면: 판 고르기 마디 사이가 꽉 찼다). 그 사이로.
        const int CaveStreams = 8;
        /// 한 화면을 내려오는 데 걸리는 초 — 크면 느리다. 🔴 눈에 띄면 너무 빠른 것이다.
        const float CaveFlowSec = 42f;
        /// 🔴 (10-09) 흐린 배경 — 덩어리 수 · 색 · 초록빛 세기. 시안 slime-soft 의 흐림 22 · 진하기 65 에 맞췄다
        const int CaveBlobs = 16, CaveGlows = 5;
        static readonly Color CaveBlobCol = new Color(0.035f, 0.047f, 0.07f, 0.70f);
        const float CaveGlowA = 0.07f;
        /// 굴 안쪽을 밝히는 세기 — 예전 바위(0.55)와 비슷한 밝기가 되게
        const float CaveLiftA = 1f;
        /// 타이틀 바탕색 — 시범 판 카메라도 이 색을 써야 오려 낸 네모가 안 드러난다
        static Color CaveLift(CaveLook cave)
        {
            var c = Color.Lerp(cave.rock, new Color32(0x21, 0x2a, 0x36, 0xff), 0.5f); c.a = CaveLiftA; return c;
        }

        //  굴마다 한 마디. 판 고르기의 "제 N 굴" 밑에 붙일 이름이다.
        //  🔴 기믹 이름을 그대로 쓴다 — 그 굴에서 **처음 만나는 것**이 곧 그 굴의 이름이다.
        //  🔴 굴 이름 — 사장님이 09-20 에 잡으신 구성 그대로다 ([[wiki/stage-plan.md]]).
        //     배우기와 써먹기가 번갈아 온다: 새것을 배우고 바로 조합으로 써먹는다.
        //     ⚠️ 굴 9~12 는 아직 **만들어야 할 것**이다 (반전 바닥 · 몸 둘).
        static readonly string[] CaveNames = {
            "굴 바닥",          //  1 기본
            "상자",             //  2
            "얼음",             //  3
            "상자와 얼음",      //  4 조합
            "금 간 바닥",       //  5
            "무너지는 빙판",    //  6 조합 둘 (금간+얼음 · 상자+금간)
            "포탈",             //  7
            "포탈 너머",        //  8 조합 여럿
            "뒤바뀌는 바닥",    //  9 새 기믹
            "뒤바뀌는 길",      // 10 기믹 + 조합
            "둘이서",           // 11 몸 둘
            "둘이서 넘는다",    // 12 몸 둘 + 조합
            "출구",             // 13 엔딩
        };

        /// 알림이 떠 있는 시간 (나타나고 · 머물고 · 사라지고)
        const float BannerIn = 0.30f, BannerHold = 0.95f, BannerOut = 0.65f;   // (10-09) 2.45 → 1.9 초

        /// <summary>
        /// 🔴 **지금 몇 층까지 올라왔나** (09-20 사장님이 받아오신 조언:
        /// *"화면 한쪽에 13칸짜리 세로 게이지로 지금 몇 번째 깊이인지만 보여줘도 비슷한 효과가 난다"*).
        ///
        /// 굴은 **아래에서 위로** 올라간다. 그래서 게이지도 세로다 —
        /// **맨 아래가 1굴(가장 깊다), 맨 위가 출구.** 지나온 아래쪽은 은은히 남고,
        /// 아직 못 간 위쪽은 어둡다. 판 고르기를 넘길 때마다 칸이 하나씩 차오른다.
        ///
        /// ⚠️ 판을 가리면 안 되므로 **왼쪽 가장자리**에 얇게만 둔다. 숫자는 안 쓴다 —
        ///    "몇 번째"를 읽게 하는 게 아니라 **얼마나 올라왔나**를 느끼게 하는 것이다.
        /// </summary>
        void CaveGauge(float w, float h, int maxCh)
        {
            int depth = Mathf.Max(CaveDepth, maxCh);
            //  ⚠️ 5~10 은 **너무 가늘어 눈에 안 들어왔다** (09-20 화면) — 굵힌다
            float gw = Mathf.Clamp(w * 0.013f, 8f, 15f);          // 막대 굵기
            float gap = Mathf.Clamp(h * 0.006f, 3f, 6f);          // 칸 사이
            float total = h * 0.56f;
            float cell = (total - gap * (depth - 1)) / depth;
            float x = Mathf.Clamp(w * 0.022f, 12f, 30f);
            float y0 = h * 0.5f - total * 0.5f;

            for (int i = 0; i < depth; i++)
            {
                int ch = i + 1;
                //  🔴 **아래가 1굴이다** — 화면은 위가 0 이므로 뒤집어 그린다
                float y = y0 + (depth - ch) * (cell + gap);
                var r = new Rect(x, y, gw, cell);

                Color c;
                if (ch == _menuCh)       c = new Color(1f, 1f, 1f, 0.92f);   // 지금 여기
                else if (ch < _menuCh)   c = new Color(1f, 1f, 1f, 0.34f);   // 지나온 아래쪽
                else                     c = new Color(1f, 1f, 1f, 0.10f);   // 아직 못 간 위쪽

                InkRing(r, PixelSprites.Round().texture, 0.9f);   // (10-10) 판 고르기 다른 것들과 같은 테두리
                Shape(r, c, PixelSprites.Round().texture);
            }

            //  출구 표시 — 맨 위 칸 위에 작은 점. 어디로 가고 있는지 보여준다
            var goal = new Color(1f, 1f, 1f, _menuCh >= depth ? 0.85f : 0.22f);
            float ds = gw * 1.5f;
            InkRing(new Rect(x + gw * 0.5f - ds * 0.5f, y0 - ds - gap * 1.5f, ds, ds), PixelSprites.Disc().texture, 0.9f);
            Shape(new Rect(x + gw * 0.5f - ds * 0.5f, y0 - ds - gap * 1.5f, ds, ds),
                  goal, PixelSprites.Disc().texture);
        }

        /// <summary>
        /// 🔴 **굴에 들어섰다고 알린다** (09-16 사장님: *"굴 바뀌는 게 약하다"*).
        /// 굴이 바뀐 판에 들어설 때 한 번, 화면 위쪽에 굴 번호와 이름이 떴다 사라진다.
        ///
        /// ⚠️ **판을 가리면 안 된다.** 퍼즐은 가운데에 있으므로 위쪽 1/4 자리에만 둔다.
        ///    글자 뒤에 판을 어둡게 덮지도 않는다 — 알림 하나 때문에 판이 안 보이면 본말이 뒤집힌다.
        /// </summary>
        void CaveBanner(float w, float h)
        {
            float t = Time.time - _caveAt;
            float total = BannerIn + BannerHold + BannerOut;
            if (_caveShown <= 0 || t < 0f || t > total) return;

            float a = t < BannerIn ? t / BannerIn
                    : t < BannerIn + BannerHold ? 1f
                    : 1f - (t - BannerIn - BannerHold) / BannerOut;
            a = Mathf.Clamp01(a);
            a = a * a * (3f - 2f * a);           // 뚝 끊기지 않게

            //  살짝 위로 떠오르며 나타난다 — 자리에 박혀 있으면 글자판처럼 보인다
            float rise = (1f - a) * h * 0.02f;
            //  ⚠️ **화면 비율로 잡으면 폰에서 겹친다.** 0.14 도 0.225 도 폰(844x390)에서
            //     「☆ 0걸음 · 커트」 줄을 잘랐다 — 그 줄들의 자리는 상단 띠 높이(bh)를 따라가는데,
            //     bh 는 작은 화면에서 **바닥값(30)에 걸려 덜 줄어든다.** 그래서 비율로 내린 알림이
            //     줄보다 위로 올라온다. 두 크기 모두에서 비켜나도록 넉넉히 내린다.
            //     판 위에 겹치지만 어두운 판을 깔아 두었고 2.45 초뿐이다.
            float y = h * 0.30f + rise;

            //  🔴 글자 뒤에 **어두운 판**을 깐다.
            //  ⚠️ 처음엔 글자만 띄웠더니 판 위에 겹쳐 안 읽혔다 (09-17 화면: 굴 4 의 "얼음" 이
            //     판 안으로 들어갔다). 굴마다 판 크기가 달라 **위로 피할 자리가 없다** —
            //     상단 띠 · 홈 점 · 걸음/커트 줄이 이미 위쪽을 채우고 있다.
            //     가리는 건 2.45 초뿐이고, 사장님이 말씀하신 건 *"굴 바뀌는 게 약하다"* 였다.
            //  🔴 (10-09 디테일 루프 6) **판이 뜨기 전 짧은 장면**으로 바꿨다.
            //     반투명 판을 판 한가운데 깔아 2.45 초 가리던 것은 "판 위에 무언가 떴다"로 읽혀
            //     판도 글자도 어중간했다. 이제 화면 전체를 굴 바탕색으로 덮고 가운데에 크게 —
            //     걷히면서 판이 드러난다. 굴을 넘는 순간이 **장(章)이 바뀌는 순간**이 된다.
            //  ⚠️ 굴 바탕(밝힌 회청색)으로 덮었더니 판과 색이 같아 **비쳐 보여 흐리멍덩**했다 → 더 어둡게.
            var cover = Color.Lerp(BgCol, CaveLift(CaveOf(_caveShown)), 0.35f); cover.a = a;
            //  글자는 덮개보다 **먼저** 사라진다 — 같이 옅어지면 걷히는 동안 글자가 판 위에 겹쳐 남는다
            float ta = a * a * a * a;   // 걷힐 때 판 위에 글자 자국이 남지 않게 더 빨리
            Shape(new Rect(0, 0, w, h), cover, Solid(Color.white));
            y = h * 0.40f + rise;

            var big = new GUIStyle(_sBig) { fontSize = Px(Mathf.Clamp(h * 0.075f, 26f, 58f)) };
            big.normal.textColor = new Color(1f, 1f, 1f, 0.92f * ta);
            GUI.Label(new Rect(0, y, w, h * 0.11f), "제 " + _caveShown + " 굴", big);

            var sub = new GUIStyle(_sMid) { fontSize = Px(Mathf.Clamp(h * 0.030f, 13f, 22f)) };
            sub.normal.textColor = new Color(1f, 1f, 1f, 0.55f * ta);
            string name = _caveShown >= 1 && _caveShown <= CaveNames.Length
                        ? CaveNames[_caveShown - 1] : "";
            GUI.Label(new Rect(0, y + h * 0.095f, w, h * 0.06f), name, sub);
        }

        /// <summary>
        /// 굴 배경을 <paramref name="area"/> 에 그린다.
        /// 🔴 <paramref name="area"/> 는 **그림 전체의 자리**다 — 잘라 그리고 싶으면
        ///    부르는 쪽이 <c>GUI.BeginGroup</c> 으로 오려 내고, 여기에는 통째 자리를 그대로 준다.
        ///    (타이틀은 시범 슬라임을 덮지 않으려고 네 조각으로 오려 부른다)
        /// </summary>
        void CaveBackdrop(Rect area, int chapter, bool lit = false)
        {
            if (Event.current != null && Event.current.type != EventType.Repaint) return;
            var cave = CaveOf(chapter);
            float w = area.width, h = area.height;
            if (w <= 1f || h <= 1f) return;
            //  ⚠️ 굴 1 과 굴 4 의 그림이 **거의 같았다** (09-17 화면) — 굴 번호에 곱만 해서는
            //     해시에 들어가는 자리가 너무 가까웠다. 굴마다 크게 벌려 준다.
            int salt = chapter * CaveSalt + chapter * chapter * 7919 + 131;

            //  🔴 **(10-09) 흐릿하게 다시 그렸다.** 사장님: *"배경도 저렇게 디테일할 필요 없고,
            //     좀 뒤에 흐리샇게 나오기만 하면 되거든"*. 브라우저 시안(slime-soft)에서 고른 그대로다.
            //     예전 ① 둥근 네모 바위 ② 흘러내리는 줄기는 **모양이 읽혀서** 뺐다 (git 에 남아 있다).
            //     이제는 무엇인지 알아볼 필요 없는 **흐린 덩어리**와 **은은한 초록빛**뿐이다.
            //     흐림은 Halo(가장자리로 옅어지는 동그라미)로 낸다 — IMGUI 에는 흐림 효과가 없다.
            float now = Time.unscaledTime;
            var halo = PixelSprites.Halo().texture;

            //  ---- ⓪ 굴 안쪽 ---- 🔴 바탕(cave.bg)은 거의 검정이다. 예전엔 밝은 바위 덩어리가 화면 대부분을
            //     덮어서 회청색으로 보였다 — 덩어리를 어둡게 바꾸면 화면 전체가 까매진다 (10-09 첫 화면).
            //     그래서 굴 색으로 먼저 한 겹 밝혀 두고, 그 위에 어두운 덩어리를 흐리게 얹는다.
            //  예전 타이틀의 회청색(33,42,54)에 맞춘다 — 굴 돌색을 푸른 회색 쪽으로 반쯤 당긴다
            //  ⚠️ 판 고르기에는 **안 깐다** — 바탕이 밝아지면 열린 판의 받침이 묻혀 숫자만 뜬다 (10-09 화면)
            if (lit) Shape(area, CaveLift(cave), Solid(Color.white));

            //  ---- ① 흐린 덩어리 ---- 위에서 늘어진 것 · 아래에서 솟은 것 · 양옆 벽
            for (int i = 0; i < CaveBlobs; i++)
            {
                int side = i < 7 ? 0 : i < 14 ? 1 : 2;           // 0 위 · 1 아래 · 2 옆
                float rx = w * Mathf.Lerp(0.12f, 0.25f, Hash(salt + i * 29 + 7));
                float ry = h * Mathf.Lerp(0.22f, 0.40f, Hash(salt + i * 41 + 11));
                float cx, cy;
                if (side == 2) { cx = (i % 2 == 0) ? -w * 0.02f : w * 1.02f; cy = h * 0.5f; rx = w * 0.14f; ry = h * 0.9f; }
                else
                {
                    cx = w * Mathf.Lerp(-0.05f, 1.05f, Hash(salt + i * 17 + 3));
                    cy = side == 0 ? h * Mathf.Lerp(-0.16f, 0.0f, Hash(salt + i * 23 + 5))
                                   : h * Mathf.Lerp(1.0f, 1.16f, Hash(salt + i * 23 + 5));
                }
                //  아주 느리게 좌우로 흔들리고 숨 쉰다 — 움직이는 게 눈에 띄면 너무 빠른 것이다
                float ph = Hash(salt + i * 53 + 13) * 6.283f;
                float sp = Mathf.Lerp(0.03f, 0.09f, Hash(salt + i * 61 + 17));
                cx += Mathf.Sin(now * sp + ph) * w * 0.02f;
                float sy = 1f + 0.04f * Mathf.Sin(now * sp * 1.3f + ph);
                var c = CaveBlobCol; c.a *= Mathf.Lerp(0.8f, 1f, Hash(salt + i * 67 + 19));
                Shape(new Rect(area.x + cx - rx, area.y + cy - ry * sy, rx * 2f, ry * 2f * sy), c, halo);
            }

            //  ---- ② 은은한 초록빛 ---- 굴 어딘가에 슬라임이 있다는 느낌만
            for (int i = 0; i < CaveGlows; i++)
            {
                float gx = w * Mathf.Lerp(0.08f, 0.92f, Hash(salt + i * 71 + 23));
                float gy = h * Mathf.Lerp(0.13f, 0.87f, Hash(salt + i * 83 + 29));
                float gr = h * Mathf.Lerp(0.26f, 0.42f, Hash(salt + i * 89 + 31));
                float ph = Hash(salt + i * 97 + 37) * 6.283f;
                float sp = Mathf.Lerp(0.15f, 0.30f, Hash(salt + i * 101 + 41));
                gx += Mathf.Sin(now * 0.05f + ph) * 20f;
                var g = BodyCol; g.a = (CaveGlowA + 0.035f * Mathf.Sin(now * sp * 2f + ph));
                Shape(new Rect(area.x + gx - gr, area.y + gy - gr, gr * 2f, gr * 2f), g, halo);
            }
        }
    }
}
