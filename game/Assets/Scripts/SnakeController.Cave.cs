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
        const float BannerIn = 0.45f, BannerHold = 1.25f, BannerOut = 0.75f;

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

                Shape(r, c, PixelSprites.Round().texture);
            }

            //  출구 표시 — 맨 위 칸 위에 작은 점. 어디로 가고 있는지 보여준다
            var goal = new Color(1f, 1f, 1f, _menuCh >= depth ? 0.85f : 0.22f);
            float ds = gw * 1.5f;
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
            float pw = Mathf.Min(w * 0.44f, 430f);
            var plate = HoleCol; plate.a = 0.78f * a;
            Goo(new Rect(w * 0.5f - pw * 0.5f, y - h * 0.015f, pw, h * 0.145f), plate);

            var big = new GUIStyle(_sBig) { fontSize = Px(Mathf.Clamp(h * 0.052f, 20f, 40f)) };
            big.normal.textColor = new Color(1f, 1f, 1f, 0.92f * a);
            GUI.Label(new Rect(0, y, w, h * 0.09f), "제 " + _caveShown + " 굴", big);

            var sub = new GUIStyle(_sMid) { fontSize = Px(Mathf.Clamp(h * 0.030f, 13f, 22f)) };
            sub.normal.textColor = new Color(1f, 1f, 1f, 0.55f * a);
            string name = _caveShown >= 1 && _caveShown <= CaveNames.Length
                        ? CaveNames[_caveShown - 1] : "";
            GUI.Label(new Rect(0, y + h * 0.075f, w, h * 0.06f), name, sub);
        }

        /// <summary>
        /// 굴 배경을 <paramref name="area"/> 에 그린다.
        /// 🔴 <paramref name="area"/> 는 **그림 전체의 자리**다 — 잘라 그리고 싶으면
        ///    부르는 쪽이 <c>GUI.BeginGroup</c> 으로 오려 내고, 여기에는 통째 자리를 그대로 준다.
        ///    (타이틀은 시범 슬라임을 덮지 않으려고 네 조각으로 오려 부른다)
        /// </summary>
        void CaveBackdrop(Rect area, int chapter)
        {
            if (Event.current != null && Event.current.type != EventType.Repaint) return;
            var cave = CaveOf(chapter);
            float w = area.width, h = area.height;
            if (w <= 1f || h <= 1f) return;
            //  ⚠️ 굴 1 과 굴 4 의 그림이 **거의 같았다** (09-17 화면) — 굴 번호에 곱만 해서는
            //     해시에 들어가는 자리가 너무 가까웠다. 굴마다 크게 벌려 준다.
            int salt = chapter * CaveSalt + chapter * chapter * 7919 + 131;

            //  ---- ① 굴 벽 ----
            //  🔴 위아래에서 밀고 들어온 덩어리. **둥근 네모**를 반쯤 화면 밖으로 빼서 그린다 —
            //     모서리만 보이므로 바위처럼 읽히고, 가운데는 비어 있어 글자를 안 가린다.
            var rock = cave.rock; rock.a = 0.55f;
            var round = PixelSprites.Round().texture;
            for (int i = 0; i < 7; i++)
            {
                bool up = i % 2 == 0;
                float rx = Hash(salt + i * 17 + 3);
                //  ⚠️ **알약 여러 개가 떠 있는 꼴**이었다 (09-17 화면: 판 고르기 위쪽에 가로로 긴
                //     둥근 판이 그대로 드러났다). 둥근 네모의 모서리가 다 보였기 때문이다.
                //     🔴 훨씬 넓게 만들어 **거의 다 화면 밖으로** 밀어 넣는다 — 걸치는 건 모서리 하나뿐이다.
                float rw = w * Mathf.Lerp(0.55f, 1.15f, Hash(salt + i * 29 + 7));
                float rh = h * Mathf.Lerp(0.30f, 0.60f, Hash(salt + i * 41 + 11));
                float x = area.x + w * Mathf.Lerp(-0.25f, 1.25f, rx) - rw * 0.5f;
                float y = up ? area.y - rh * 0.86f : area.y + h - rh * 0.14f;
                var c = rock; c.a *= Mathf.Lerp(0.5f, 1f, Hash(salt + i * 53 + 13));
                Shape(new Rect(x, y, rw, rh), c, round);
            }

            //  ---- ② 흐르는 슬라임 ----
            //  천장에서 한 가닥씩 늘어져 내려온다. 게임의 말랑한 덩어리를 그대로 쓴다 —
            //  몸통 아래 방울이 매달려 있어 "흘러내리는 것"으로 읽힌다.
            float now = Time.unscaledTime;
            for (int i = 0; i < CaveStreams; i++)
            {
                float lane = (i + 0.5f) / CaveStreams;
                float jx = (Hash(salt + i * 61 + 5) - 0.5f) * (w / CaveStreams) * 0.7f;
                float x = area.x + w * lane + jx;

                //  ⚠️ 화면 높이의 26~52% 로 잡았더니 **이끼 낀 기둥**처럼 보였다 (09-17 화면) —
                //     너무 길쭉하면 매달려 흐르는 게 아니라 **서 있는 것**으로 읽힌다.
                //     짧고 굵게, 아래 방울을 키워야 "늘어진 것"이 된다.
                //  다 자랐을 때의 길이. 아래에서 이 길이의 0.22~1 배 사이를 오간다.
                float len = h * Mathf.Lerp(0.17f, 0.31f, Hash(salt + i * 71 + 9));
                float wide = len * Mathf.Lerp(0.26f, 0.40f, Hash(salt + i * 83 + 15));

                //  가닥마다 빠르기와 박자를 어긋낸다 — 나란히 움직이면 발처럼 보인다
                float sp = Mathf.Lerp(0.72f, 1.35f, Hash(salt + i * 97 + 21));
                float t = Mathf.Repeat(now / CaveFlowSec * sp + Hash(salt + i * 31 + 2), 1f);

                //  🔴 **천장에 붙여 둔다.** 화면을 훑고 내려가게 했더니 한가운데 뜬 가닥이 잔뜩 생겨
                //     흘러내리는 것이 아니라 **떠 있는 알약**으로 보였다 (09-17 화면).
                //     흘러내리는 것은 어딘가에 **매달려 있어야** 그렇게 읽힌다 —
                //     천장에서 자라 내려오다 도로 줄어든다. 떠 있는 가닥이 아예 안 생긴다.
                float ease = t < 0.5f ? t * 2f : (1f - t) * 2f;
                ease = ease * ease * (3f - 2f * ease);
                float grow = 0.22f + 0.78f * ease;
                float y = area.y - wide * 0.35f;

                var tex = PixelSprites.Goo(wide / len, 1, i + chapter, 1f, 0.55f, 0.34f);
                float room = PixelSprites.GooRoom(1, 0.55f);
                var col = BodyCol;
                col.a = 0.085f * Mathf.Lerp(0.6f, 1f, Hash(salt + i * 13 + 1));
                Shape(new Rect(x - wide * 0.5f, y, wide, len * grow * (1f + room)), col, tex);
            }
        }
    }
}
