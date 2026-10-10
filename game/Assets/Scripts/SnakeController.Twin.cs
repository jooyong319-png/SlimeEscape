using System.Collections.Generic;
using UnityEngine;

namespace SlimeEscape
{
    // 🔴 **슬라임 둘 — 안 조종하는 쪽 그리기** (09-23).
    //    조종하는 슬라임은 원래 그리던 대로(_segs) 그린다. 이쪽은 **쉬는 슬라임**이라
    //    같은 초록을 **흐리게** 둔다 — "초록은 슬라임만" 규칙은 그대로다.
    //    ⚠️ 지금은 기능 시험용: 떨어질 때 부드럽게 따라가기만 하고, 조종이 넘어갈 땐 딱 붙인다.
    //       넘어가는 연출·표정은 사장님이 해보고 난 뒤에.
    public partial class SnakeController
    {
        readonly List<SpriteRenderer> _twinSegs = new List<SpriteRenderer>();
        readonly List<Vector2> _twinPos = new List<Vector2>();
        bool _twinSnap = true;
        static readonly Color TwinCol = new Color32(0x4a, 0x8f, 0x62, 0xff);   // 쉬는 슬라임 — 나보다 어둡다
        //  🔴 쉬는 슬라임에도 **머리 마름모**를 단다 (10-08 화면). 없으면 어두운 초록 네모 = "밀 수 있는 블록"으로 읽혔다.
        //     조종하는 쪽처럼 번쩍이지 않는다 — 꺼진 눈. 숨만 아주 느리게 쉰다
        static readonly Color TwinKeyRing = new Color32(0xc8, 0xe6, 0xd2, 0xff);
        static readonly Color TwinKeyCore = new Color32(0x1e, 0x3a, 0x29, 0xff);
        SpriteRenderer _twinRing, _twinCore;

        //  🔴 (10-10) **조종이 넘어가는 순간** — 전에는 딱 바뀌기만 해서 어느 쪽으로 넘어갔는지 놓쳤다.
        //     ① 넘겨준 슬라임에서 넘겨받는 슬라임으로 작은 불빛이 날아가고 ② 넘겨받은 쪽에 빛 고리가 퍼지고
        //     ③ 쉬게 된 쪽은 살짝 움츠러들었다 돌아온다. 전부 0.5초 안에 끝난다 — 다음 걸음을 막지 않는다.
        int _lastHead = -1, _lastRest = -1;
        float _swapFxAt = -9f;
        Vector2 _swapFrom, _swapTo;
        SpriteRenderer _swapRing, _swapSpark;
        const float SwapFxSec = 0.75f;   // 0.5 는 눈에 다 안 들어왔다 (10-10)

        //  🔴 **교체 칸 '&'** — 09-23 에 규칙만 넣고 **그림이 없었다** (10-08 화면: 판에 아무것도 안 보였다).
        //     색 규칙(초록=슬라임 · 주황=조각 · 형광 테=홈 · 보라=포탈)을 피해 **무채색 + 마름모 둘**:
        //     머리의 마름모가 둘 = "머리가 바뀐다". 둘이 번갈아 밝아진다 = 교대
        static readonly Color SwapPad = new Color32(0x6e, 0x7a, 0x8a, 0xff);
        readonly List<(SpriteRenderer pad, SpriteRenderer a, SpriteRenderer b, int cell)> _swapViews
            = new List<(SpriteRenderer, SpriteRenderer, SpriteRenderer, int)>();

        void BuildSwapViews()
        {
            _swapViews.Clear();
            _twinRing = _twinCore = null;
            if (_L == null || _L.Swaps.Count == 0) return;
            foreach (int c in _L.Swaps)
            {
                var pad = NewSprite("SwapPad", -1);
                pad.sprite = PixelSprites.Round();
                pad.transform.position = CellPos(c);
                pad.transform.localScale = Vector3.one * 0.78f;
                var a = NewSprite("SwapKeyA", 0);
                var b = NewSprite("SwapKeyB", 0);
                a.sprite = b.sprite = PixelSprites.Diamond();
                a.transform.localScale = b.transform.localScale = new Vector3(0.20f, 0.28f, 1f);
                a.transform.position = CellPos(c) + new Vector2(-0.14f, 0f);
                b.transform.position = CellPos(c) + new Vector2(0.14f, 0f);
                _swapViews.Add((pad, a, b, c));
            }
        }

        void TickSwapViews()
        {
            if (_swapViews.Count == 0) return;
            //  1.2초마다 밝은 쪽이 넘어간다 — 부드럽게
            float t = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI / 1.2f);
            foreach (var (pad, a, b, cell) in _swapViews)
            {
                if (pad == null) continue;
                bool on = _st != null && _st.Head == cell;
                var pc = SwapPad; pc.a = on ? 0.75f : 0.45f;
                pad.color = pc;
                a.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.30f, 0.90f, t));
                b.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.90f, 0.30f, t));
            }
        }

        void TickSwapFx()
        {
            float t = (Time.time - _swapFxAt) / SwapFxSec;
            bool on = t >= 0f && t < 1f;
            if (_swapRing == null && on)
            {
                //  ⚠️ 차례 8(슬라임 위)에 두었더니 꽉 찬 빛이 몸을 살짝 밝힐 뿐 **보이지 않았다** (10-10 화면).
                //     몸 **뒤**(2)에 깔아 둘레로 번져 나오게 한다
                _swapRing = NewSprite("SwapRing", 2);
                _swapRing.sprite = PixelSprites.Halo();
                _swapSpark = NewSprite("SwapSpark", 9);
                _swapSpark.sprite = PixelSprites.Disc();
            }
            if (_swapRing == null) return;
            _swapRing.enabled = _swapSpark.enabled = on;
            if (!on) return;
            //  ① 불빛 — 앞 절반 동안 날아간다 (끝에서 살짝 느려진다)
            float f = Mathf.Clamp01(t / 0.5f); f = 1f - (1f - f) * (1f - f);
            _swapSpark.enabled = t < 0.5f;
            _swapSpark.transform.position = Vector2.Lerp(_swapFrom, _swapTo, f) + new Vector2(0f, 0.35f * Mathf.Sin(f * Mathf.PI));
            _swapSpark.transform.localScale = Vector3.one * 0.22f;
            var sc = Color.Lerp(BodyCol, Color.white, 0.5f); _swapSpark.color = sc;
            //  ② 빛 고리 — 불빛이 닿는 순간부터 퍼진다
            float g = Mathf.Clamp01((t - 0.35f) / 0.65f);
            _swapRing.enabled = t >= 0.35f;
            _swapRing.transform.position = _swapTo;
            _swapRing.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 2.8f, g);
            //  ⚠️ 0.65 는 밝은 바닥 위에서 거의 안 보였다 (10-10 화면) — 더 밝고 진하게
            var rc = Color.Lerp(BodyCol, Color.white, 0.35f); rc.a = 0.95f * (1f - g) * (1f - g * 0.3f); _swapRing.color = rc;
        }

        void TickTwinView()
        {
            TickSwapViews();
            var ob = (_L != null && _L.Twin && _st != null) ? _st.Ob : null;
            //  교체를 알아챈다 — 머리가 방금 전 쉬던 자리에, 쉬는 머리가 방금 전 머리 자리에 있으면 넘어간 것이다
            if (ob != null && ob.Count > 0)
            {
                int rest = ob[0];
                if (_lastHead >= 0 && _lastHead != _lastRest && _st.Head == _lastRest && rest == _lastHead)
                {
                    _swapFxAt = Time.time;
                    _swapFrom = CellPos(rest); _swapTo = CellPos(_st.Head);
                }
                _lastHead = _st.Head; _lastRest = rest;
            }
            else { _lastHead = _lastRest = -1; }
            TickSwapFx();
            //  판을 새로 지으면 _root 와 함께 스프라이트가 사라진다 — 그때 다시 만든다
            for (int i = _twinSegs.Count - 1; i >= 0; i--)
                if (_twinSegs[i] == null) { _twinSegs.Clear(); _twinPos.Clear(); _twinSnap = true; break; }
            int n = ob != null ? ob.Count : 0;
            while (_twinSegs.Count < n)
            {
                var sr = NewSprite("Twin", 3);
                sr.sprite = PixelSprites.Drop(0);
                sr.color = TwinCol;
                _twinSegs.Add(sr);
                _twinPos.Add(n > 0 ? CellPos(ob[Mathf.Min(_twinPos.Count, n - 1)]) : Vector2.zero);
            }
            for (int i = 0; i < _twinSegs.Count; i++)
            {
                bool on = i < n;
                _twinSegs[i].enabled = on;
                if (!on) continue;
                Vector2 want = CellPos(ob[i]);
                _twinPos[i] = _twinSnap ? want : Vector2.MoveTowards(_twinPos[i], want, Time.deltaTime * 9f);
                _twinSegs[i].transform.position = _twinPos[i];
                float s = i == 0 ? 0.92f : 0.86f;
                //  ③ 쉬게 된 쪽이 살짝 움츠러든다
                float st = (Time.time - _swapFxAt) / 0.3f;
                if (st >= 0f && st < 1f) s *= 1f - 0.16f * Mathf.Sin(st * Mathf.PI);
                _twinSegs[i].transform.localScale = new Vector3(s, s, 1);
            }
            _twinSnap = false;

            //  머리 마름모 — 조종하는 쪽(UI.cs 열쇠)과 같은 자리·같은 비율, 조금 작고 흐리게
            if (n == 0) { if (_twinRing != null) _twinRing.enabled = _twinCore.enabled = false; return; }
            if (_twinRing == null)
            {
                _twinRing = NewSprite("TwinKeyRing", 10);
                _twinCore = NewSprite("TwinKeyCore", 11);
                _twinRing.sprite = _twinCore.sprite = PixelSprites.Diamond();
            }
            _twinRing.enabled = _twinCore.enabled = true;
            float breathe = 0.5f + 0.5f * Mathf.Sin(Time.time * 1.1f);
            Vector2 at = _twinPos[0] + new Vector2(0f, 0.14f);
            _twinRing.transform.position = at;
            _twinRing.transform.localScale = new Vector3(0.36f, 0.50f, 1f);
            var rc = TwinKeyRing; rc.a = 0.45f + 0.15f * breathe;
            _twinRing.color = rc;
            _twinCore.transform.position = at;
            _twinCore.transform.localScale = new Vector3(0.36f * 0.52f, 0.50f * 0.52f, 1f);
            _twinCore.color = TwinKeyCore;
        }
    }
}
