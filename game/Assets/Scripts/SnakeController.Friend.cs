using System.Collections.Generic;
using UnityEngine;

namespace SlimeEscape
{
    // 🔴 (10-10 사장님) **판 속 별 → 수정에 갇힌 친구 슬라임.**
    //    *"스테이지 안에 있는 별을 대신할 요소로 쓰면 괜찮아 보이는데?"* — 별과 규칙이 똑같다
    //    (머리가 닿으면 줍고, 안 주워도 판은 깨진다). 그래서 엔진 · 판 · 별 3개 기준은 그대로 두고
    //    **그림만** 바꾼다. 시안: https://claude.ai/artifact/GRjwfprtPHJqCPKHLnJ5nU
    //    ⚠️ 「친구를 구했다!」 같은 글자는 띄우지 않는다 (사장님: 너무 AI 같다) — 그림으로만 말한다.
    //    구한 친구는 나중에 로비에 모인다 (로비는 다음 단계).
    public partial class SnakeController
    {
        static readonly Color[] FriendTints = {
            new Color32(0x93, 0xdf, 0xae, 0xff), new Color32(0x7f, 0xd6, 0x9b, 0xff), new Color32(0xa6, 0xe6, 0xbd, 0xff),
            new Color32(0x86, 0xd9, 0xa8, 0xff), new Color32(0x9b, 0xe3, 0xb4, 0xff), new Color32(0x7a, 0xcf, 0x98, 0xff) };
        static readonly Color LeafCol = new Color32(0x5f, 0xb3, 0x7a, 0xff);
        static readonly Color GlassCol = new Color(0.80f, 0.86f, 0.92f, 0.24f);
        static readonly Color GlassRim = new Color(0.90f, 0.94f, 0.98f, 0.75f);

        SpriteRenderer _frBody, _frEyeL, _frEyeR, _frLeaf, _crInk, _crFill, _crRim, _crShine, _crGlow;
        readonly List<(SpriteRenderer sr, Vector2 v, float spin)> _shards = new List<(SpriteRenderer, Vector2, float)>();
        Vector2 _frHome;
        Color _frTint;
        bool _frGot;
        float _frAt = -9f;
        float _frAway = 1f;   // 구할 때 머리 반대쪽 (-1 왼쪽 · 1 오른쪽)

        // ---- 수정 그림 (여섯 모 · 위가 뾰족) — 한 번 굽고 계속 쓴다 ----
        static Sprite _hexSolid, _hexRing, _hexInk;
        static readonly Vector2[] HexPts = {
            new Vector2(0f, 1.25f), new Vector2(-0.82f, 0.55f), new Vector2(-0.9f, -0.55f),
            new Vector2(0f, -1.0f), new Vector2(0.9f, -0.55f), new Vector2(0.82f, 0.55f) };   // 반시계

        /// band ≤ 0 이면 꽉 찬 모양, 아니면 안쪽으로 band 두께의 테
        static Sprite HexSprite(float band)
        {
            const int W = 88, H = 112;
            const float X0 = -1.1f, X1 = 1.1f, Y0 = -1.4f, Y1 = 1.4f;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
                      { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[W * H];
            float unitPx = W / (X1 - X0);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var p = new Vector2(Mathf.Lerp(X0, X1, (x + 0.5f) / W), Mathf.Lerp(Y0, Y1, (y + 0.5f) / H));
                    float d = -1e9f;   // 볼록 다각형까지의 부호 거리 (밖이 +)
                    for (int i = 0; i < HexPts.Length; i++)
                    {
                        Vector2 a = HexPts[i], b = HexPts[(i + 1) % HexPts.Length];
                        Vector2 e = (b - a).normalized;
                        Vector2 n = new Vector2(e.y, -e.x);   // 반시계 → 바깥쪽
                        d = Mathf.Max(d, Vector2.Dot(p - a, n));
                    }
                    float a0 = Mathf.Clamp01(0.5f - d * unitPx);
                    if (band > 0f) a0 *= Mathf.Clamp01(0.5f + (d + band) * unitPx);
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(a0 * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            //  1 단위 폭 = 그림 폭 (X 범위 2.2) — 수정 몸은 그 안에서 폭 0.82 · 높이 1.02
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, (0f - Y0) / (Y1 - Y0)), W);
        }

        void EnsureHex()
        {
            if (_hexSolid == null) _hexSolid = HexSprite(0f);
            if (_hexRing == null) _hexRing = HexSprite(0.06f);
            if (_hexInk == null) _hexInk = HexSprite(0.16f);
        }

        /// 판을 지을 때 별 자리에 둔다
        void BuildFriend(Vector2 at)
        {
            EnsureHex();
            _frHome = at;
            _frGot = false; _frAt = -9f;
            _shards.Clear();
            _frTint = FriendTints[Mathf.Abs(_index) % FriendTints.Length];

            _crGlow = NewSprite("CrystalGlow", -1);
            _crGlow.sprite = PixelSprites.Halo();
            _crGlow.transform.position = at;
            _crGlow.transform.localScale = Vector3.one * 1.8f;

            //  친구 — 몸 · 눈 둘 · 잎사귀 (Friend · FriendLeaf 는 테두리가 저절로 붙는다: Ink.cs InkNames)
            _frBody = NewSprite("Friend", 1);
            _frBody.sprite = PixelSprites.Drop(0);
            _frBody.color = _frTint;
            _frEyeL = NewSprite("FriendEye", 2); _frEyeL.sprite = PixelSprites.Disc(); _frEyeL.color = InkCol;
            _frEyeR = NewSprite("FriendEye", 2); _frEyeR.sprite = PixelSprites.Disc(); _frEyeR.color = InkCol;
            _frLeaf = NewSprite("FriendLeaf", 2); _frLeaf.sprite = PixelSprites.Disc(); _frLeaf.color = LeafCol;

            //  수정 — 친구 **앞**에 반투명 유리. 검은 테는 유리 바깥으로 조금 크게
            const float S = 1.0f;   // ⚠️ 0.74 는 칸의 절반이라 안 보였다 (10-10 화면)
            _crInk = NewSprite("CrystalInk", 3);
            _crInk.sprite = _hexInk;
            _crInk.transform.position = new Vector3(at.x, at.y, 0.002f);
            _crInk.transform.localScale = Vector3.one * (S * 1.10f);
            _crInk.color = InkCol;
            _crFill = NewSprite("CrystalFill", 3);
            _crFill.sprite = _hexSolid;
            _crFill.transform.position = new Vector3(at.x, at.y, 0.001f);
            _crFill.transform.localScale = Vector3.one * S;
            _crFill.color = GlassCol;
            _crRim = NewSprite("CrystalRim", 3);
            _crRim.sprite = _hexRing;
            _crRim.transform.position = new Vector3(at.x, at.y, 0f);
            _crRim.transform.localScale = Vector3.one * S;
            _crRim.color = GlassRim;
            _crShine = NewSprite("CrystalShine", 3);
            _crShine.sprite = PixelSprites.Soft();
            _crShine.transform.position = new Vector3(at.x - 0.18f, at.y + 0.27f, -0.001f);
            _crShine.transform.localScale = new Vector3(0.065f, 0.30f, 1f);
            _crShine.transform.rotation = Quaternion.Euler(0, 0, 32f);
            _crShine.color = new Color(1f, 1f, 1f, 0.55f);
        }

        void TickFriend()
        {
            if (_frBody == null || _st == null) return;
            bool got = _st.Sc != 0;
            if (got && !_frGot)
            {
                _frAt = Time.time; SpawnShards();
                //  ⚠️ 머리 쪽으로 뛰면 슬라임에 가려 안 보였다 (10-10 화면) — 반대쪽으로
                _frAway = (_st.Body.Count > 1 && CellPos(_st.Body[1]).x > _frHome.x + 0.01f) ? -1f : 1f;   // 머리 바로 뒤 마디가 온 쪽의 반대
            }
            if (!got && _frGot) ClearShards();   // 되돌리면 다시 갇힌다 — 터지는 건 "구했다"는 말이다
            _frGot = got;

            bool caged = !got;
            _crInk.enabled = _crFill.enabled = _crRim.enabled = _crShine.enabled = caged;
            float now = Time.time;
            if (caged)
            {
                //  수정 속에서 꼼지락 — 숨 쉬듯 눌렸다 펴지고, 슬라임 머리 쪽을 본다
                float sq = 0.05f * Mathf.Sin(now * 3f);
                float look = 0f;
                if (_st.Body.Count > 0) look = Mathf.Clamp(CellPos(_st.Head).x - _frHome.x, -1f, 1f);
                PlaceFriend(_frHome + new Vector2(0f, -0.18f), sq, look, true);
                float b = 0.5f + 0.5f * Mathf.Sin(now * 2.4f);
                _crGlow.enabled = true;
                _crGlow.transform.position = _frHome;
                _crGlow.transform.localScale = Vector3.one * 1.8f;
                _crGlow.color = new Color(0.90f, 0.95f, 1f, 0.12f + 0.07f * b);
                return;
            }

            float u = now - _frAt;
            TickShards();
            if (u < 0.9f)
            {
                //  ⚠️ 옆으로만 뛰면 수정 칸에 들어온 슬라임 머리에 가려 안 보였다 (10-10 화면).
                //     먼저 **한 칸 높이 튀어 올라** 머리 반대쪽 옆 칸에 내려앉고, 한 번 더 작게 통통.
                float x, y, land = 0f;
                if (u < 0.6f)
                {
                    float t = u / 0.6f;
                    x = _frAway * t * 0.95f;
                    y = 1.05f * 4f * t * (1f - t);
                }
                else
                {
                    float t = (u - 0.6f) / 0.3f;
                    x = _frAway * 0.95f;
                    y = 0.28f * 4f * t * (1f - t);
                    if (t < 0.18f || t > 0.85f) land = 0.16f;
                }
                PlaceFriend(_frHome + new Vector2(x, -0.18f + y), land, _frAway, true);
                _crGlow.enabled = false;
            }
            else if (u < 2.1f)
            {
                //  빛이 되어 위로 — 로비로 간다
                PlaceFriend(Vector2.zero, 0f, 0f, false);
                float v = (u - 0.9f) / 1.2f;
                _crGlow.enabled = true;
                _crGlow.transform.position = _frHome + new Vector2(_frAway * (0.95f + v * 0.4f), -0.1f + v * v * 3.2f);
                _crGlow.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 0.5f, v);
                var gc = _frTint; gc.a = 0.85f * (1f - v);
                _crGlow.color = gc;
            }
            else
            {
                PlaceFriend(Vector2.zero, 0f, 0f, false);
                _crGlow.enabled = false;
            }
        }

        /// 친구 한 마리 놓기 — at 은 몸 가운데, sq 는 눌림(+ 이면 납작), look 은 -1~1 보는 쪽
        void PlaceFriend(Vector2 at, float sq, float look, bool on)
        {
            _frBody.enabled = _frEyeL.enabled = _frEyeR.enabled = _frLeaf.enabled = on;
            if (!on) return;
            const float R = 0.52f;
            float w = R * (1f + sq), h = R * 0.92f * (1f - sq);
            _frBody.transform.position = new Vector3(at.x, at.y - (R * 0.92f - h) * 0.5f, 0f);
            _frBody.transform.localScale = new Vector3(w, h, 1f);
            float top = at.y - (R * 0.92f - h) * 0.5f + h * 0.5f;
            float ey = top - h * 0.42f, ex = look * w * 0.10f;
            float er = 0.07f;
            _frEyeL.transform.position = new Vector3(at.x - w * 0.20f + ex, ey, -0.001f);
            _frEyeR.transform.position = new Vector3(at.x + w * 0.20f + ex, ey, -0.001f);
            _frEyeL.transform.localScale = _frEyeR.transform.localScale = new Vector3(er, er, 1f);
            _frLeaf.transform.position = new Vector3(at.x + w * 0.18f, top + 0.03f, -0.001f);
            _frLeaf.transform.localScale = new Vector3(0.26f, 0.12f, 1f);
            _frLeaf.transform.rotation = Quaternion.Euler(0, 0, 32f);
        }

        void SpawnShards()
        {
            ClearShards();
            for (int i = 0; i < 8; i++)
            {
                var s = NewSprite("Shard", 9);
                s.sprite = PixelSprites.Solid();
                s.transform.position = _frHome;
                s.transform.localScale = new Vector3(0.10f, 0.05f, 1f);
                s.color = new Color(0.86f, 0.91f, 0.96f, 0.95f);
                float a = (i / 8f) * Mathf.PI * 2f + 0.3f;
                float sp = Mathf.Lerp(2.2f, 3.2f, Hash(_index * 13 + i * 7));
                _shards.Add((s, new Vector2(Mathf.Cos(a), Mathf.Sin(a) + 0.6f) * sp, Mathf.Lerp(-540f, 540f, Hash(i * 31 + 5))));
            }
        }

        void TickShards()
        {
            float u = Time.time - _frAt;
            for (int i = _shards.Count - 1; i >= 0; i--)
            {
                var (s, v, spin) = _shards[i];
                if (s == null) { _shards.RemoveAt(i); continue; }
                if (u > 0.7f) { Destroy(s.gameObject); _shards.RemoveAt(i); continue; }
                Vector2 p = _frHome + v * u + new Vector2(0f, -4.5f * u * u);
                s.transform.position = p;
                s.transform.rotation = Quaternion.Euler(0, 0, spin * u);
                var c = s.color; c.a = 0.95f * (1f - u / 0.7f); s.color = c;
            }
        }

        void ClearShards()
        {
            foreach (var (s, _, _) in _shards) if (s != null) Destroy(s.gameObject);
            _shards.Clear();
        }

        // ---- 화면(IMGUI) 아이콘 — 위쪽 표시 · 결과 카드 ----
        /// 구했으면 친구 얼굴, 못 구했으면 수정. r 은 정사각형 자리
        void FriendIcon(Rect r, bool got, float a = 1f)
        {
            EnsureHex();
            if (got)
            {
                var body = new Rect(r.x + r.width * 0.08f, r.y + r.height * 0.22f, r.width * 0.84f, r.height * 0.74f);
                var drop = PixelSprites.Drop(0).texture;
                InkRing(body, drop, a);
                var t = _frTint.a > 0f ? _frTint : FriendTints[0]; t.a = a;
                Shape(body, t, drop);
                var ink = InkCol; ink.a = a;
                float e = r.width * 0.12f;
                Shape(new Rect(body.center.x - body.width * 0.2f - e * 0.5f, body.y + body.height * 0.40f, e, e), ink, PixelSprites.Disc().texture);
                Shape(new Rect(body.center.x + body.width * 0.2f - e * 0.5f, body.y + body.height * 0.40f, e, e), ink, PixelSprites.Disc().texture);
                var lc = LeafCol; lc.a = a;
                var m = GUI.matrix;
                var lr = new Rect(body.center.x, body.y - r.height * 0.10f, r.width * 0.36f, r.height * 0.16f);
                GUIUtility.RotateAroundPivot(-32f, lr.center);
                Shape(lr, lc, PixelSprites.Disc().texture);
                GUI.matrix = m;
            }
            else
            {
                var hr = new Rect(r.x + r.width * 0.05f, r.y, r.width * 0.9f, r.height);
                var ic = InkCol; ic.a = a;
                Shape(hr, ic, _hexInk.texture);
                var gc = GlassCol; gc.a = Mathf.Min(1f, GlassCol.a * 1.6f) * a;
                Shape(hr, gc, _hexSolid.texture);
                var rc = GlassRim; rc.a *= a;
                Shape(hr, rc, _hexRing.texture);
            }
        }
    }
}
