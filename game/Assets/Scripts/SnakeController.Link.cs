using System.Collections.Generic;
using UnityEngine;

namespace SlimeEscape
{
    // 🔴 **홈 ↔ 문벽을 잇는다** (09-23 사장님: "불친절하달까? 홈을 했을 때
    //    저기가 열릴 거라는 느낌이 전혀 안 들어").
    //    · 문벽이 자기 홈의 색을 입는다          (Board.cs 문벽 그리기)
    //    · 몸이 홈에 들어가기 시작하면 묶인 칸이 숨 쉰다 (Board.cs _coverOf)
    //    · 홈을 채우면 심에서 **불빛이 날아가** 닿은 뒤에 바뀐다 (여기)
    public partial class SnakeController
    {
        /// 문마다 몸이 덮은 비율 0~1 (예고 숨쉬기용)
        readonly float[] _coverOf = new float[3];

        class LinkSpark { public SpriteRenderer sr; public Vector2 from, to; public float t0; }
        readonly List<LinkSpark> _linkSparks = new List<LinkSpark>();
        const float SparkFly = 0.35f;

        /// 문 하나가 열렸다 — 심에서 그 문에 묶인 칸마다 불빛을 쏜다
        void StartLinkSparks(int door)
        {
            if (door < 0 || door >= _L.Gates.Length) return;
            var d = _L.Doors[door];
            Vector2 from = CellPos(d.Core >= 0 ? d.Core : d.Cells[0]);
            var cells = new List<int>(_L.Gates[door]);
            if (cells.Count == 0) return;
            //  🔴 불빛이 닿을 때까지 칸이 안 바뀌게 — 페이드를 날아가는 시간만큼 뒤로 민다
            _gateFadeOf[door] = -SparkFly / 0.45f;
            foreach (int c in cells)
            {
                var sr = NewSprite("LinkSpark", 5);
                sr.sprite = PixelSprites.Disc();
                sr.transform.localScale = Vector3.one * 0.26f;
                sr.color = RailOf(door);
                sr.transform.position = from;
                _linkSparks.Add(new LinkSpark { sr = sr, from = from, to = CellPos(c), t0 = Time.time });
            }
        }

        void TickLinkSparks()
        {
            for (int i = _linkSparks.Count - 1; i >= 0; i--)
            {
                var k = _linkSparks[i];
                if (k.sr == null) { _linkSparks.RemoveAt(i); continue; }
                float t = (Time.time - k.t0) / SparkFly;
                if (t >= 1f) { Destroy(k.sr.gameObject); _linkSparks.RemoveAt(i); continue; }
                //  살짝 위로 휘어 날아간다 — 곧게 가면 선으로 읽혀 "이어졌다"가 약하다
                float e = 1f - (1f - t) * (1f - t);
                Vector2 p = Vector2.Lerp(k.from, k.to, e) + Vector2.up * Mathf.Sin(t * Mathf.PI) * 0.6f;
                k.sr.transform.position = p;
                k.sr.transform.localScale = Vector3.one * Mathf.Lerp(0.30f, 0.18f, t);
            }
        }
    }
}
