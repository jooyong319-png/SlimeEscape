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
