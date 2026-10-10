using System.Collections.Generic;
using UnityEngine;

namespace SlimeEscape
{
    // 🔴 (10-10 사장님) **Snakebird 의 모양새** — 판을 한 덩어리 섬으로, 굵은 검은 테두리로, 윗면은 두툼한 덮개로.
    //    시안 https://claude.ai/artifact/2ik9KXw6gMtyH9N3WDdRHY 의 A 안.
    //    *"지금 슬라임 외형? 출렁임? 이런걸 열심히 잡은거거든"* → 슬라임 그리는 코드는 **손대지 않는다.**
    //    *"지금 슬라임에 테두리를 좀 주면"* → 마디·이음매를 그대로 베껴 한 겹 크게, 검게, 뒤에 깐다.
    //    분위기(어두운 굴)와 색 규칙은 그대로다.
    public partial class SnakeController
    {
        static readonly Color InkCol = new Color32(0x04, 0x05, 0x08, 0xff);
        const float WallInk = 0.07f;    // 벽 테두리 두께 (칸 단위). ⚠️ 0.12 는 사장님이 "좀 줄여봐" (10-10)
        const float SlimeInk = 0.065f;  // 슬라임 테두리. ⚠️ 0.11 에서 줄였다 (10-10)
        const float CapInk = 0.045f;    // 윗면 덮개 테두리

        readonly List<SpriteRenderer> _segInk = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _linkInk = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _twinInk = new List<SpriteRenderer>();

        //  🔴 (10-10 사장님: "얇은거 ㄱㄱ") 장애물 · 물건도 같은 얇은 테두리. 홈은 빼다 — 비어 있는 자리로 읽혀야 한다.
        //     그리는 곳이 여러 파일에 흩어져 있어서 **이름표**로 고른다. 매 프레임 바깥 모양을 베끼니
        //     떠다니는 조각 · 미는 상자 · 눌리는 스위치 · 꺼지는 블록이 그대로 따라온다.
        static readonly HashSet<string> InkNames = new HashSet<string> {
            "Crack", "Box", "Food", "Star", "Portal", "SwPlate", "SwButton", "SwBlock", "Gate", "Sign", "Pad", "SwapPad", "Zone" };
        const float ObjInk = 0.06f;
        readonly List<(SpriteRenderer src, SpriteRenderer ink)> _objInk = new List<(SpriteRenderer, SpriteRenderer)>();
        Transform _inkRoot;
        int _inkSeen = -1;

        /// 벽 덩어리 바깥에 검은 테두리. 빈 칸(-5)보다 앞, 벽(-3)보다 뒤(-4)라 **벽 밖으로 삐져나온 만큼만** 보인다.
        /// 판 안쪽 빈 칸에 닿는 벽만 두른다 — 속 칸까지 두르면 스프라이트만 늘어난다.
        void BuildWallInk()
        {
            for (int y = -1; y <= _L.H; y++)
                for (int x = -1; x <= _L.W; x++)
                {
                    if (!WallAt(x, y)) continue;
                    bool edge = false;
                    for (int dy = -1; dy <= 1 && !edge; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < _L.W && ny < _L.H && !WallAt(nx, ny)) { edge = true; break; }
                        }
                    if (!edge) continue;

                    bool ring = x < 0 || y < 0 || x >= _L.W || y >= _L.H;
                    //  ⚠️ 타이틀 시범 판은 둘레가 로비 바탕으로 녹는다 — 둘레에 테두리를 두르면 네모 틀이 다시 보인다 (10-10 화면)
                    if (ring && DemoEdge != null) continue;
                    if (ring)
                    {
                        //  ⚠️ 바깥 겹은 **판 안쪽으로만** 두른다. 바깥으로 삐져나오면
                        //     EdgeFade 가 녹여 놓은 판 끝에 검은 줄이 생긴다.
                        int ex = x < 0 ? 1 : x >= _L.W ? -1 : 0;
                        int ey = y < 0 ? -1 : y >= _L.H ? 1 : 0;
                        var r = NewSprite("WallInk", -4);
                        r.sprite = PixelSprites.Solid();
                        r.transform.position = CellXY(x, y) + new Vector2(ex * WallInk * 0.5f, ey * WallInk * 0.5f);
                        r.transform.localScale = new Vector3(1f + Mathf.Abs(ex) * WallInk, 1f + Mathf.Abs(ey) * WallInk, 1f);
                        r.color = InkCol;
                        continue;
                    }
                    var o = NewSprite("WallInk", -4);
                    o.sprite = PixelSprites.Round();
                    o.transform.position = CellXY(x, y);
                    o.transform.localScale = new Vector3(1f + 2f * WallInk, 1f + 2f * WallInk, 1f);
                    o.color = InkCol;
                    //  이웃 벽과는 곧게 잇는다 — 둥근 모서리끼리 만나는 자리에 홈이 안 남게
                    if (WallAt(x + 1, y)) InkBridge(x, y, new Vector2(0.5f, 0f), new Vector3(1f, 1f + 2f * WallInk, 1f));
                    if (WallAt(x, y + 1)) InkBridge(x, y, new Vector2(0f, -0.5f), new Vector3(1f + 2f * WallInk, 1f, 1f));
                }
        }

        void InkBridge(int x, int y, Vector2 off, Vector3 scale)
        {
            var b = NewSprite("WallInk", -4);
            b.sprite = PixelSprites.Solid();
            b.transform.position = CellXY(x, y) + off;
            b.transform.localScale = scale;
            b.color = InkCol;
        }

        /// 🔴 윗면 덮개 — 얇은 띠 하나였던 것을 **두툼한 돌 덮개**로. 아랫단이 봉긋봉긋 늘어지고 검은 테를 두른다.
        ///    옆이 트였으면 가장자리 밖으로 살짝 넘친다 — 덮개가 "얹힌" 것으로 읽힌다.
        ///    z 로 앞뒤를 못박는다 (같은 차례 -2): 테 덩이 → 테 → 덩이 → 덮개 → 빛줄. EdgeFade(-0.002)는 그 앞.
        void WallCap(int x, int y)
        {
            //  ⚠️ 판 끝(바깥 겹 쪽)으로는 안 넘친다 — 바깥 겹도 벽이라 계단으로 잘못 읽혀 덮개가 판 밖으로 튀어나왔다 (10-10 화면)
            bool openL = x - 1 >= 0 && (!WallAt(x - 1, y) || WallAt(x - 1, y - 1));
            bool openR = x + 1 < _L.W && (!WallAt(x + 1, y) || WallAt(x + 1, y - 1));
            float l = openL ? 0.10f : 0f, r = openR ? 0.10f : 0f;
            float w = 1f + l + r, cx = (r - l) * 0.5f;
            Vector2 p = CellXY(x, y);
            int k = x * 31 + y * 17;

            var inkBase = NewSprite("CapInk", -2);
            inkBase.sprite = PixelSprites.Soft();
            inkBase.transform.position = new Vector3(p.x + cx, p.y + 0.41f, 0.002f);
            inkBase.transform.localScale = new Vector3(w + 2f * CapInk, 0.32f + 2f * CapInk, 1f);
            inkBase.color = InkCol;
            for (int j = 0; j < 4; j++)
            {
                float bx = -0.375f + j * 0.25f;
                var ib = NewSprite("CapInk", -2);
                ib.sprite = PixelSprites.Disc();
                float big = Mathf.Lerp(0.26f, 0.34f, Hash(k + j * 5)) + 2f * CapInk;
                ib.transform.position = new Vector3(p.x + bx, p.y + 0.27f - CapInk * 0.5f, 0.003f);
                ib.transform.localScale = new Vector3(big, big * 0.82f, 1f);
                ib.color = InkCol;
                var bm = NewSprite("WallTop", -2);
                bm.sprite = PixelSprites.Disc();
                bm.transform.position = new Vector3(p.x + bx, p.y + 0.27f, 0.001f);
                float sm = Mathf.Lerp(0.26f, 0.34f, Hash(k + j * 5));
                bm.transform.localScale = new Vector3(sm, sm * 0.82f, 1f);
                bm.color = Color.Lerp(RockTop, Rock, 0.35f);   // 아랫단은 한 톤 어둡게 — 밝은 굴에서 레이스처럼 하얗게 뜨던 것 (10-10)
            }
            var cap = NewSprite("WallTop", -2);
            cap.sprite = PixelSprites.Soft();
            cap.transform.position = new Vector3(p.x + cx, p.y + 0.41f, 0f);
            cap.transform.localScale = new Vector3(w, 0.32f, 1f);
            cap.color = RockTop;
            var hi = NewSprite("WallTopHi", -2);
            hi.sprite = PixelSprites.Solid();
            hi.transform.position = new Vector3(p.x + cx, p.y + 0.52f, -0.001f);
            hi.transform.localScale = new Vector3(w - (openL ? 0.08f : 0f) - (openR ? 0.08f : 0f), 0.06f, 1f);
            hi.color = Color.Lerp(RockTop, Color.white, 0.28f);
        }

        /// 슬라임 테두리 — 마디·이음매·쌍둥이를 **그대로 베낀다** (모양·자리·눌림 전부).
        /// 매 프레임 끝(LateUpdate)에 맞춘다 — 출렁임이 다 정해진 뒤라야 한 박자 늦지 않는다.
        void LateUpdate()
        {
            CopyInk(_segs, _segInk, "SegInk");
            CopyInk(_links, _linkInk, "LinkInk");
            CopyInk(_twinSegs, _twinInk, "TwinInk");
            TickObjInk();
        }

        void TickObjInk()
        {
            if (_root == null) return;
            if (_inkRoot != _root) { _objInk.Clear(); _inkRoot = _root; _inkSeen = -1; }
            //  새 물건이 생겼을 때만 훑는다 (상자는 처음 그릴 때 만들어진다)
            if (_root.childCount != _inkSeen)
            {
                var have = new HashSet<SpriteRenderer>();
                foreach (var pr in _objInk) have.Add(pr.src);
                int n = _root.childCount;
                for (int i = 0; i < n; i++)
                {
                    var t = _root.GetChild(i);
                    if (!InkNames.Contains(t.name)) continue;
                    var sr = t.GetComponent<SpriteRenderer>();
                    if (sr == null || have.Contains(sr)) continue;
                    _objInk.Add((sr, NewSprite(t.name + "Ink", sr.sortingOrder - 1)));   // 바로 한 차례 뒤
                }
                _inkSeen = _root.childCount;
            }
            for (int k = _objInk.Count - 1; k >= 0; k--)
            {
                var (s, ink) = _objInk[k];
                if (s == null || ink == null) { if (ink != null) Destroy(ink.gameObject); _objInk.RemoveAt(k); continue; }
                bool on = s.enabled && s.gameObject.activeInHierarchy && s.color.a > 0.01f;
                ink.enabled = on;
                if (!on) continue;
                ink.sprite = s.sprite;
                var t = s.transform;
                ink.transform.position = t.position + new Vector3(0f, 0f, 0.001f);
                ink.transform.rotation = t.rotation;
                var sc = t.localScale;
                ink.transform.localScale = new Vector3(Mathf.Abs(sc.x) + 2f * ObjInk, Mathf.Abs(sc.y) + 2f * ObjInk, 1f);
                var c = InkCol; c.a = s.color.a; ink.color = c;
            }
        }

        void CopyInk(List<SpriteRenderer> src, List<SpriteRenderer> dst, string name)
        {
            //  판을 새로 지으면 _root 와 함께 사라진다 — 그때 다시 만든다
            for (int i = dst.Count - 1; i >= 0; i--)
                if (dst[i] == null) { dst.Clear(); break; }
            if (_root == null) return;
            while (dst.Count < src.Count)
            {
                var s = NewSprite(name, 2);   // 몸(3) · 이음매(5) · 머리(6) 보다 뒤
                dst.Add(s);
            }
            for (int i = 0; i < dst.Count; i++)
            {
                var a = i < src.Count ? src[i] : null;
                bool on = a != null && a.enabled && a.gameObject.activeInHierarchy && a.color.a > 0.01f;
                dst[i].enabled = on;
                if (!on) continue;
                dst[i].sprite = a.sprite;
                var t = a.transform;
                dst[i].transform.position = t.position + new Vector3(0f, 0f, 0.001f);
                dst[i].transform.rotation = t.rotation;
                var sc = t.localScale;
                dst[i].transform.localScale = new Vector3(sc.x + 2f * SlimeInk, sc.y + 2f * SlimeInk, 1f);
                var c = InkCol; c.a = a.color.a; dst[i].color = c;
            }
        }
    }
}
