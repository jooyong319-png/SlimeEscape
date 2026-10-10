using System.Collections.Generic;
using UnityEngine;

namespace SlimeEscape
{
    // 🔴 (10-10) **조각을 먹는 순간** — 전에는 조각이 아무 반응 없이 사라지기만 했다.
    //    먹는 자리에서 주황 방울 여섯이 톡 튀어 흩어지고, 짧게 주황빛이 번진다 (0.45초).
    //    되돌리기로 조각이 살아날 때는 아무것도 안 한다 — 터지는 건 "먹었다"는 말이다.
    //    색은 조각 색(주황) 그대로 — 색 규칙 그대로다.
    public partial class SnakeController
    {
        readonly List<bool> _foodWasEaten = new List<bool>();
        class EatPop { public SpriteRenderer glow; public SpriteRenderer[] drops; public Vector2[] vel; public Vector2 at; public float t0; }
        readonly List<EatPop> _eatPops = new List<EatPop>();
        const float EatPopSec = 0.55f;

        /// 판을 새로 지을 때 — 지금 상태를 "원래 그랬다"로 받는다
        void ResetEatFx() { _foodWasEaten.Clear(); _eatPops.Clear(); }

        void TickEatFx()
        {
            if (_st == null || _L == null) return;
            if (_foodWasEaten.Count != _L.Foods.Count)
            {
                _foodWasEaten.Clear();
                for (int i = 0; i < _L.Foods.Count; i++) _foodWasEaten.Add(SnakeEngine.IsEaten(_st, i));
            }
            for (int i = 0; i < _L.Foods.Count; i++)
            {
                bool e = SnakeEngine.IsEaten(_st, i);
                if (e && !_foodWasEaten[i]) SpawnEatPop(CellPos(_L.Foods[i]), i);
                _foodWasEaten[i] = e;
            }
            for (int k = _eatPops.Count - 1; k >= 0; k--)
            {
                var p = _eatPops[k];
                float t = (Time.time - p.t0) / EatPopSec;
                if (p.glow == null || t >= 1f)
                {
                    if (p.glow != null) Destroy(p.glow.gameObject);
                    foreach (var d in p.drops) if (d != null) Destroy(d.gameObject);
                    _eatPops.RemoveAt(k); continue;
                }
                float u = Time.time - p.t0;
                var gc = FoodCol; gc.a = 0.8f * (1f - t);
                p.glow.color = gc;
                p.glow.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 2.0f, 1f - (1f - t) * (1f - t));
                for (int j = 0; j < p.drops.Length; j++)
                {
                    //  위로 튀었다가 무게로 떨어진다
                    Vector2 pos = p.at + p.vel[j] * u + new Vector2(0f, -3.2f * u * u);
                    p.drops[j].transform.position = pos;
                    float s = Mathf.Lerp(0.24f, 0.07f, t);   // ⚠️ 0.15 는 머리 옆 점으로만 보였다 (10-10 화면)
                    p.drops[j].transform.localScale = Vector3.one * s;
                    var dc = FoodCol; dc.a = 1f - t * t; p.drops[j].color = dc;
                }
            }
        }

        void SpawnEatPop(Vector2 at, int seed)
        {
            var p = new EatPop { at = at, t0 = Time.time, drops = new SpriteRenderer[6], vel = new Vector2[6] };
            p.glow = NewSprite("EatGlow", 2);
            p.glow.sprite = PixelSprites.Halo();
            p.glow.transform.position = at;
            for (int j = 0; j < 6; j++)
            {
                var d = NewSprite("EatDrop", 9);
                d.sprite = PixelSprites.Disc();
                d.transform.position = at;
                //  위쪽 반원으로 고르게 퍼지되 조금씩 어긋나게 (조각마다 늘 같은 모양)
                float ang = Mathf.Lerp(20f, 160f, (j + 0.5f) / 6f) + (Hash(seed * 31 + j * 7) - 0.5f) * 18f;
                float sp = Mathf.Lerp(2.4f, 3.4f, Hash(seed * 17 + j * 13));
                p.vel[j] = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * sp;
                p.drops[j] = d;
            }
            _eatPops.Add(p);
        }
    }
}
