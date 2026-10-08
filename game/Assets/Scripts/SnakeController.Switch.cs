using System.Collections.Generic;
using UnityEngine;

namespace SlimeEscape
{
    // 🔴 **스위치 그리기** (09-23 사장님: "아싸리 슈퍼마리오랑 같은 방식으로 하자.
    //    특정 영역에 머리가 들어가면 작동하는 걸로. 스위칭 되는 거지").
    //    마리오 메이커 ON/OFF 와 같은 말투, 🔴 **색은 우리 규칙대로** (09-23 사장님: "디자인만 우리랑 어울리게"):
    //      초록=슬라임 · 주황=조각 · 형광 테=홈 은 못 쓴다. 얼음이 결로, 금 간 바닥이 금으로 말하듯
    //      **무채색 + 무늬**로 가른다.
    //      · '4' 블록 = 푸른 회색 + **가로 줄 둘** · '5' 블록 = 따뜻한 회색 + **세로 줄 둘**
    //      · **지금 벽인 쪽은 꽉 찬 돌, 아닌 쪽은 흐린 테두리** — 밟고 지나간다는 게 보인다
    //      · 스위치 버튼엔 **지금 벽인 쪽의 줄무늬**가 박힌다 — "이걸 누르면 이 무늬가 빠진다"
    //    규칙은 SnakeEngine (Dm 의 SW 비트). 🔴 tools/engine.js 와 **같아야 한다.**
    public partial class SnakeController
    {
        //  '4' — 푸른 회색 (문벽 GateEdge 보다 푸르고 밝다)
        static readonly Color SwAEdge = new Color32(0x86, 0x9a, 0xb0, 0xff);
        static readonly Color SwAIn   = new Color32(0x3a, 0x46, 0x55, 0xff);
        //  '5' — 따뜻한 회색 (금 간 바닥보다 밝다)
        static readonly Color SwBEdge = new Color32(0xb0, 0xa2, 0x8c, 0xff);
        static readonly Color SwBIn   = new Color32(0x55, 0x4c, 0x40, 0xff);
        static readonly Color SwBar   = new Color32(0xe4, 0xe8, 0xec, 0xff);

        class SwBlock { public SpriteRenderer edge, inner, bar1, bar2; public bool onSet; }
        readonly List<SwBlock> _swBlocks = new List<SwBlock>();
        readonly List<(SpriteRenderer plate, SpriteRenderer button, SpriteRenderer b1, SpriteRenderer b2, int cell)> _swButtons
            = new List<(SpriteRenderer, SpriteRenderer, SpriteRenderer, SpriteRenderer, int)>();
        /// 0 = 꺼짐 모양, 1 = 켜짐 모양 — 부드럽게 따라간다
        float _swLook;
        /// 뒤집힌 순간 (블록이 한 번 튄다)
        float _swFlipAt = -9f;
        int _swShown = -1;

        void BuildSwitchViews()
        {
            _swBlocks.Clear(); _swButtons.Clear(); _linkSparks.Clear();
            _swShown = -1; _swFlipAt = -9f;
            _swLook = (_st != null && (_st.Dm & SnakeEngine.SW) != 0) ? 1f : 0f;
            if (!_L.HasSwitch) return;
            foreach (int c in _L.SwOff) AddSwBlock(c, false);
            foreach (int c in _L.SwOn) AddSwBlock(c, true);
            foreach (int c in _L.Switches)
            {
                var plate = NewSprite("SwPlate", -1);
                plate.transform.position = CellPos(c) + new Vector2(0f, -0.40f);
                plate.transform.localScale = new Vector3(0.86f, 0.14f, 1);
                plate.color = GateEdge;
                var button = NewSprite("SwButton", 0);
                button.sprite = PixelSprites.Round();
                _swButtons.Add((plate, button, NewSprite("SwMark", 1), NewSprite("SwMark", 1), c));
            }
        }

        void AddSwBlock(int c, bool onSet)
        {
            var edge = NewSprite("SwBlock", -2);
            edge.transform.position = CellPos(c);
            var inner = NewSprite("SwBlockIn", -1);
            inner.transform.position = CellPos(c);
            _swBlocks.Add(new SwBlock { edge = edge, inner = inner, onSet = onSet,
                                        bar1 = NewSprite("SwBar", 0), bar2 = NewSprite("SwBar", 0) });
        }

        /// 줄 둘 — 가로('4') 또는 세로('5'). size 는 줄 길이, squash 는 세로로 눌린 비
        static void PlaceBars(SpriteRenderer a, SpriteRenderer b, Vector2 at, bool vertical,
                              float size, Color c, float squash = 1f)
        {
            float gap = 0.13f * (vertical ? 1f : squash), thick = 0.07f;
            Vector2 off = vertical ? new Vector2(gap, 0f) : new Vector2(0f, gap);
            Vector3 sc = vertical ? new Vector3(thick, size * squash, 1) : new Vector3(size, thick, 1);
            a.transform.position = at - off; b.transform.position = at + off;
            a.transform.localScale = sc; b.transform.localScale = sc;
            a.color = c; b.color = c;
        }

        void TickSwitchViews()
        {
            if (_L == null || !_L.HasSwitch) return;
            int on = (_st.Dm & SnakeEngine.SW) != 0 ? 1 : 0;
            if (_swShown >= 0 && on != _swShown) _swFlipAt = Time.time;
            _swShown = on;
            _swLook = Mathf.MoveTowards(_swLook, on, Time.deltaTime / 0.15f);
            //  뒤집힌 직후 한 번 튄다 — "판 전체가 바뀌었다" 가 한눈에 들어온다
            float punch = Mathf.Clamp01(1f - (Time.time - _swFlipAt) / 0.25f);
            float pop = 1f + 0.12f * Mathf.Sin(punch * Mathf.PI);

            foreach (var b in _swBlocks)
            {
                //  solid 1 = 꽉 찬 블록(벽), 0 = 속 빈 테두리(지나간다)
                float solid = b.onSet ? _swLook : 1f - _swLook;
                Color col = b.onSet ? SwBEdge : SwAEdge;
                Color cin = b.onSet ? SwBIn : SwAIn;
                var ec = col; ec.a = Mathf.Lerp(0.35f, 1f, solid);
                b.edge.color = ec;
                b.edge.transform.localScale = Vector3.one * 0.96f * pop;
                b.inner.color = Color.Lerp(Floor, cin, solid);
                float s = Mathf.Lerp(0.84f, 0.72f, solid) * pop;
                b.inner.transform.localScale = new Vector3(s, s, 1);
                //  줄무늬 — 비어 있을 때도 흐리게 남긴다: "여기 무엇이 생길 수 있다"
                var bc = SwBar; bc.a = Mathf.Lerp(0.18f, 0.70f, solid);
                Vector2 at = b.edge.transform.position;
                PlaceBars(b.bar1, b.bar2, at, b.onSet, 0.44f * pop, bc);
            }

            //  스위치 = **지금 벽인 쪽의 돌색 + 줄무늬**. 머리가 올라 있으면 눌려서 납작해진다
            bool onNow = on == 1;
            foreach (var (plate, button, b1, b2, cell) in _swButtons)
            {
                bool pressed = _st.Head == cell;
                float hgt = pressed ? 0.16f : 0.34f;
                button.color = onNow ? SwBEdge : SwAEdge;
                Vector2 bp = CellPos(cell) + new Vector2(0f, -0.33f + hgt * 0.5f);
                button.transform.position = bp;
                button.transform.localScale = new Vector3(0.62f, hgt, 1);
                var mc = SwBIn; if (!onNow) mc = SwAIn;
                PlaceBars(b1, b2, bp, onNow, pressed ? 0.12f : 0.24f, mc, 0.5f);
            }
        }
    }
}
