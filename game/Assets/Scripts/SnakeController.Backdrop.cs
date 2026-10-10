using System.Collections.Generic;
using UnityEngine;

namespace SlimeEscape
{
    // 🔴 (10-10 디테일 루프) **게임 중 판 바깥** — 전에는 판이 끝나면 굴 바탕색 한 장뿐이었다.
    //    로비·판 고르기에 깐 흐린 굴(SnakeController.Cave.cs)을 **아주 옅게** 판 뒤에도 깐다.
    //    판 칸은 불투명이라 맨 뒤 차례(-30)에 두면 판 바깥에만 비친다 — 판 읽기를 방해하면 안 된다.
    //    덩어리는 판 가장자리에서 떨어뜨려 둔다. 붙이면 EdgeFade 가 녹여 놓은 끝선이 다시 드러난다.
    public partial class SnakeController
    {
        class BackBlob { public SpriteRenderer sr; public Vector2 at; public float ph, sp, amp; }
        readonly List<BackBlob> _backBlobs = new List<BackBlob>();
        const int BackBlobs = 10, BackGlows = 3;
        const float BackBlobA = 0.60f, BackGlowA = 0.07f;

        void BuildBackdrop()
        {
            _backBlobs.Clear();
            if (_L == null) return;
            float W = _L.W, H = _L.H;
            Vector2 mid = _org + new Vector2(W * 0.5f, -H * 0.5f);
            int salt = _L.W * 131 + _L.H * 977 + _index * 37;
            //  굴 돌색을 바탕 쪽으로 — 밝은 덩어리가 아니라 "저 멀리 바위가 있다" 정도
            var blobCol = Color.Lerp(BgCol, Rock, 0.85f);

            for (int i = 0; i < BackBlobs + BackGlows; i++)
            {
                bool glow = i >= BackBlobs;
                var sr = NewSprite(glow ? "BackGlow" : "BackBlob", -30);
                sr.sprite = PixelSprites.Halo();
                float r = glow ? Mathf.Lerp(3.5f, 5.5f, Hash(salt + i * 41 + 11))
                               : Mathf.Lerp(2.6f, 4.6f, Hash(salt + i * 43 + 13));
                //  ⚠️ 판 중심에서 원으로 놓았더니 넓은 판에서는 위아래 덩어리가 화면 밖으로 나갔다 (10-10 화면).
                //     판 네모(바깥 겹 포함) **둘레를 따라** 놓는다. 끝에서 반지름의 반쯤 떨어뜨린다 —
                //     Halo 는 가장자리가 아주 옅어서 녹여 놓은 끝선에 안 닿는다.
                float u = ((i - (glow ? BackBlobs : 0)) + Hash(salt + i * 29 + 7) * 0.8f) / (glow ? BackGlows : BackBlobs) + (glow ? 0.37f : 0f);
                var at = mid + Perimeter(u - Mathf.Floor(u), W * 0.5f + 1f + r * 0.5f, H * 0.5f + 1f + r * 0.5f);
                sr.transform.position = new Vector3(at.x, at.y, 0.05f);
                sr.transform.localScale = new Vector3(r * 2f * Mathf.Lerp(1f, 1.4f, Hash(salt + i * 53 + 19)), r * 2f, 1f);
                Color c;
                if (glow) { c = BodyCol; c.a = BackGlowA; }
                else { c = blobCol; c.a = BackBlobA * Mathf.Lerp(0.7f, 1f, Hash(salt + i * 59 + 23)); }
                sr.color = c;
                _backBlobs.Add(new BackBlob
                {
                    sr = sr, at = at,
                    ph = Hash(salt + i * 61 + 29) * 6.283f,
                    sp = Mathf.Lerp(0.05f, 0.12f, Hash(salt + i * 67 + 31)),
                    amp = glow ? 0.5f : 0.3f,
                });
            }
        }

        /// 반너비 hw · 반높이 hh 네모의 둘레 위 한 점 (0~1 → 한 바퀴)
        static Vector2 Perimeter(float t, float hw, float hh)
        {
            float d = t * 4f * (hw + hh);
            if (d < 2f * hw) return new Vector2(-hw + d, hh);           d -= 2f * hw;
            if (d < 2f * hh) return new Vector2(hw, hh - d);            d -= 2f * hh;
            if (d < 2f * hw) return new Vector2(hw - d, -hh);           d -= 2f * hw;
            return new Vector2(-hw, -hh + d);
        }

        /// 아주 느리게 흔들린다 — 움직이는 게 눈에 띄면 너무 빠른 것이다
        void TickBackdrop()
        {
            float now = Time.unscaledTime;
            foreach (var b in _backBlobs)
            {
                if (b.sr == null) continue;
                var p = b.at + new Vector2(Mathf.Sin(now * b.sp + b.ph) * b.amp, Mathf.Sin(now * b.sp * 0.7f + b.ph * 1.3f) * b.amp * 0.5f);
                b.sr.transform.position = new Vector3(p.x, p.y, 0.05f);
            }
        }
    }
}
