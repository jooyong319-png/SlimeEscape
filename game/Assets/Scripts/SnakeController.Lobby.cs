using System.Collections.Generic;
using UnityEngine;

namespace SlimeEscape
{
    // 🔴 (10-10 사장님) **구한 친구들이 로비에 모인다 · 친구가 늘수록 로비가 넓어진다.**
    //    *"다 채워도 될것같다 생각해 자율적으로 움직이는 느낌으로"* · *"로비는 그러면 점점 커지면 좋겠내"*
    //    시안: https://claude.ai/artifact/GRjwfprtPHJqCPKHLnJ5nU (②)
    //    친구 수 = 친구(옛 별)가 있는 판 중 **구하고 깬** 판 수 — 별 기록(StarBit)과 같은 자리라 따로 저장하지 않는다.
    //    화면(IMGUI)에 그린다. 판 그림과 같은 모양 언어 — 검은 테 · 윗면 덮개 · 슬라임과 같은 몸.
    public partial class SnakeController
    {
        /// 시험용 — 0 이상이면 기록 대신 이 수만큼 친구를 세운다 (사진 찍을 때)
        public static int LobbyFriendsDebug = -1;

        //  선반 — 화면 비율 (왼 x · 오른 x · 윗면 y · 몇 마리부터 열리나).
        //  ⚠️ 로고(가운데 위) · 메뉴(오른쪽 가운데) · 시범 판(왼쪽 아래)을 피한 빈 자리에만 둔다.
        static readonly Vector4[] Ledges = {
            new Vector4(0.42f, 0.64f, 0.90f, 0),
            new Vector4(0.46f, 0.60f, 0.73f, 3),
            new Vector4(0.62f, 0.73f, 0.61f, 6),
            new Vector4(0.40f, 0.76f, 0.90f, 10),   // 바닥이 오른쪽으로 넓어진다
            new Vector4(0.78f, 0.97f, 0.94f, 14),
            new Vector4(0.44f, 0.56f, 0.55f, 18),
            new Vector4(0.25f, 0.38f, 0.50f, 24),
            new Vector4(0.05f, 0.20f, 0.50f, 32),
            new Vector4(0.80f, 0.95f, 0.33f, 40),
        };
        static readonly string[] Feats = { "leaf", "sprout", "horn", "bow", "none", "leaf", "sprout", "horn" };

        class Pal { public int p; public float x, y, R, t, dur, tx, face = 1f, hop, jx0, jy0, jx1, jy1, blink; public int jp, seed; public string st = "idle", feat; public Color tint; }
        readonly List<Pal> _pals = new List<Pal>();
        readonly List<Vector4> _ledgesOn = new List<Vector4>();
        int _palCount = -1;
        float _palLast = -1f;

        //  🔴 (10-10 루프) 구한 판 번호 — 로비 친구의 색 · 장식을 **판 속에서 구한 그 친구와 같게** 정한다
        //     (Friend.cs: 색 = 판 번호 % 색 수 · 장식 = Feats[(판 번호 × 7 + 3) % 8])
        readonly List<int> _rescuedIdx = new List<int>();

        float _rescuedAt = -9f;
        int _rescuedN;

        int RescuedCount()
        {
            //  ⚠️ 로비를 그릴 때마다 부른다 — 판 전부를 매 프레임 훑지 않게 1초에 한 번만 센다
            if (Time.unscaledTime - _rescuedAt < 1f && _rescuedAt >= 0f) return _rescuedN;
            _rescuedAt = Time.unscaledTime;
            _rescuedN = CountRescued();
            return _rescuedN;
        }

        int CountRescued()
        {
            _rescuedIdx.Clear();
            if (LobbyFriendsDebug >= 0) { for (int i = 0; i < LobbyFriendsDebug; i++) _rescuedIdx.Add(i * 5 + 1); return LobbyFriendsDebug; }
            if (_set == null || _set.levels == null) return 0;
            for (int i = 0; i < _set.levels.Length; i++)
            {
                var lv = _set.levels[i];
                if (lv == null || lv.grid == null || !_recs.TryGetValue(lv.id, out int rec) || rec >= StarBit) continue;
                foreach (var row in lv.grid) if (row != null && row.IndexOf('o') >= 0) { _rescuedIdx.Add(i); break; }
            }
            return _rescuedIdx.Count;
        }

        void LobbySetup(int n)
        {
            _palCount = n;
            _ledgesOn.Clear();
            foreach (var L in Ledges) if (L.w <= n) _ledgesOn.Add(L);
            //  넓은 바닥이 열리면 좁은 첫 선반은 안 그린다 (같은 높이에 겹친다)
            if (n >= 10) _ledgesOn.RemoveAt(0);
            _pals.Clear();
            //  ⚠️ 선반을 무작위로 골랐더니 한 선반에 몰려 겹쳐 섰다 (10-10 45마리 화면).
            //     선반 **넓이만큼** 나눠 앉히고, 선반 안에서도 고르게 띄운다.
            int m = Mathf.Min(n, 60);
            float total = 0f; foreach (var L in _ledgesOn) total += L.y - L.x;
            var per = new int[_ledgesOn.Count]; int given = 0;
            for (int k = 0; k < per.Length; k++) { per[k] = Mathf.FloorToInt(m * (_ledgesOn[k].y - _ledgesOn[k].x) / total); given += per[k]; }
            for (int k = 0; given < m; k = (k + 1) % per.Length) { per[k]++; given++; }
            int i = 0;
            for (int k = 0; k < per.Length; k++)
                for (int j = 0; j < per[k]; j++, i++)
                {
                    var L = _ledgesOn[k];
                    float u = (j + 0.5f) / per[k] + (Hash(i * 7 + 1) - 0.5f) * 0.4f / per[k];
                    _pals.Add(new Pal {
                        p = k, x = Mathf.Lerp(L.x + 0.015f, L.y - 0.015f, u), y = L.z,
                        R = Mathf.Lerp(0.8f, 1.15f, Hash(i * 31 + 5)), t = Hash(i * 53) * 2f, dur = 1f + Hash(i * 59) * 2f,
                        face = Hash(i * 61) < 0.5f ? -1f : 1f, seed = i, blink = Hash(i * 67) * 4f,
                        feat = Feats[Mathf.Abs(LvOf(i) * 7 + 3) % Feats.Length], tint = FriendTints[Mathf.Abs(LvOf(i)) % FriendTints.Length] });
                }
        }

        int LvOf(int i) => i < _rescuedIdx.Count ? _rescuedIdx[i] : i;

        /// 같은 선반에서 x 가까이에 친구가 몇이나 있나 — 걸어갈 곳은 덜 붐비는 쪽으로
        int Crowd(int p, float x, Pal self)
        {
            int c = 0;
            foreach (var o in _pals) if (o != self && o.p == p && Mathf.Abs(o.x - x) < 0.03f) c++;
            return c;
        }

        void PalPick(Pal a)
        {
            float r = Random.value; a.t = 0f;
            var L = _ledgesOn[a.p];
            if (r < 0.40f) { a.st = "idle"; a.dur = 0.8f + Random.value * 2.4f; }
            else if (r < 0.84f)
            {
                a.st = "walk";
                //  후보 셋 중 덜 붐비는 곳
                float best = a.x; int bc = int.MaxValue;
                for (int k = 0; k < 3; k++)
                {
                    float cx = Mathf.Clamp(a.x + (Random.value - 0.5f) * 0.22f, L.x + 0.015f, L.y - 0.015f);
                    int cc = Crowd(a.p, cx, a);
                    if (cc < bc) { bc = cc; best = cx; }
                }
                a.tx = best;
                a.face = a.tx > a.x ? 1f : -1f;
            }
            else
            {
                //  다른 선반으로 — 가로로 겹치고 높이 차가 적당한 곳
                var opts = new List<int>();
                for (int i = 0; i < _ledgesOn.Count; i++)
                {
                    var Q = _ledgesOn[i];
                    if (i == a.p || a.x < Q.x - 0.06f || a.x > Q.y + 0.06f || Mathf.Abs(Q.z - L.z) > 0.24f) continue;
                    opts.Add(i);
                }
                if (opts.Count == 0) { a.st = "idle"; a.dur = 1f; return; }
                int q = opts[Random.Range(0, opts.Count)];
                var Qd = _ledgesOn[q];
                a.st = "jump"; a.jx0 = a.x; a.jy0 = a.y; a.jp = q; a.jy1 = Qd.z; a.dur = 0.75f;
                a.jx1 = Mathf.Clamp(a.x + (Random.value - 0.5f) * 0.06f, Qd.x + 0.015f, Qd.y - 0.015f);
                a.face = a.jx1 > a.x ? 1f : -1f;
            }
        }

        void PalStep(Pal a, float dt, float w, float h)
        {
            a.t += dt;
            if (a.st == "idle") { a.hop = 0f; if (a.t > a.dur) PalPick(a); }
            else if (a.st == "walk")
            {
                float d = a.tx - a.x, sp = 0.045f;
                if (Mathf.Abs(d) < 0.002f) { PalPick(a); return; }
                a.x += Mathf.Sign(d) * Mathf.Min(Mathf.Abs(d), sp * dt);
                a.hop = Mathf.Abs(Mathf.Sin(a.t * 7.5f)) * 0.55f;
            }
            else if (a.st == "jump")
            {
                float u = Mathf.Min(1f, a.t / a.dur);
                a.x = Mathf.Lerp(a.jx0, a.jx1, u);
                float peak = Mathf.Min(a.jy0, a.jy1) - 0.10f;
                a.y = (1 - u) * (1 - u) * a.jy0 + 2 * (1 - u) * u * peak + u * u * a.jy1;
                a.hop = 0f;
                if (u >= 1f) { a.p = a.jp; a.y = a.jy1; a.st = "land"; a.t = 0f; a.dur = 0.25f; }
            }
            else if (a.st == "land") { a.hop = 0f; if (a.t > a.dur) { a.st = "idle"; a.t = 0f; a.dur = 0.6f + Random.value; } }
        }

        /// 타이틀에서 TitleStage 다음 · 로고 앞에 부른다
        void LobbyFriends(float w, float h, float fade)
        {
            int n = RescuedCount();
            if (n != _palCount) LobbySetup(n);
            if (Event.current != null && Event.current.type != EventType.Repaint) return;
            float now = Time.unscaledTime;
            float dt = _palLast < 0f ? 0f : Mathf.Min(0.05f, now - _palLast);
            _palLast = now;
            float th = Mathf.Max(12f, h * 0.032f);
            foreach (var L in _ledgesOn) LobbyLedge(new Rect(L.x * w, L.z * h, (L.y - L.x) * w, th), fade);
            float baseR = Mathf.Clamp(h * 0.024f, 9f, 26f);
            _pals.Sort((a, b) => a.y.CompareTo(b.y));
            foreach (var a in _pals)
            {
                PalStep(a, dt, w, h);
                float sq = 0f;
                if (a.st == "walk" && a.hop < 0.08f) sq = 0.12f;
                if (a.st == "land") sq = 0.22f * (1f - a.t / a.dur);
                if (a.st == "idle") sq = 0.035f * Mathf.Sin(now * 2.2f + a.seed);
                float look = (a.st == "idle" && Mathf.Sin(now * 0.5f + a.seed) > 0.7f) ? -1f : a.face;   // 가끔 시범 판(왼쪽) 쪽을 본다
                bool blink = ((now + a.blink) % 4.2f) < 0.13f;
                float R = baseR * a.R;
                PalDraw(a.x * w, a.y * h - a.hop * R, R, a.tint, a.feat, sq, look, blink, fade);
            }
        }

        /// 바위 선반 — 몸 · 윗면 덮개 · 봉긋한 아랫단, 판과 같은 검은 테
        void LobbyLedge(Rect r, float a)
        {
            var cave = CaveOf(1);
            var soft = PixelSprites.Soft().texture;
            var disc = PixelSprites.Disc().texture;
            InkBehind(r, soft, a);
            var body = cave.rock; body.a = a;
            Shape(r, body, soft);
            float capH = r.height * 0.42f;
            var capR = new Rect(r.x - 2f, r.y - capH * 0.35f, r.width + 4f, capH);
            InkBehind(capR, soft, a);
            int bumps = Mathf.Max(3, Mathf.RoundToInt(r.width / (capH * 1.6f)));
            float bw = capR.width / bumps;
            var ink = InkCol; ink.a = a;
            var top = cave.top; top.a = a;
            var dk = Color.Lerp(cave.top, cave.rock, 0.35f); dk.a = a;
            float p = UiInkPx;
            for (int i = 0; i < bumps; i++)
                Shape(new Rect(capR.x + i * bw - p, capR.y + capH * 0.45f - p, bw + 2f * p, capH * 0.8f + 2f * p), ink, disc);
            for (int i = 0; i < bumps; i++)
                Shape(new Rect(capR.x + i * bw, capR.y + capH * 0.45f, bw, capH * 0.8f), dk, disc);
            Shape(capR, top, soft);
            var hi = Color.Lerp(cave.top, Color.white, 0.28f); hi.a = a;
            Shape(new Rect(capR.x + 3f, capR.y + 1f, capR.width - 6f, Mathf.Max(1.5f, capH * 0.16f)), hi, Solid(Color.white));
        }

        /// 친구 한 마리 (화면 좌표). bottom = 발 닿는 높이
        void PalDraw(float cx, float bottom, float R, Color tint, string feat, float sq, float look, bool blink, float a)
        {
            float bw = R * 2f * (1f + sq), bh = R * 1.84f * (1f - sq);
            var body = new Rect(cx - bw * 0.5f, bottom - bh, bw, bh);
            var drop = PixelSprites.Drop(0).texture;
            InkRing(body, drop, a);
            var t = tint; t.a = a;
            Shape(body, t, drop);
            float topY = body.y;
            var ink = InkCol; ink.a = a;
            var disc = PixelSprites.Disc().texture;
            float e = Mathf.Max(2f, R * 0.16f), ex = look * bw * 0.09f, ey = topY + bh * 0.40f;
            for (int s = -1; s <= 1; s += 2)
            {
                float x = cx + s * bw * 0.20f + ex;
                if (blink) Shape(new Rect(x - e * 0.6f, ey + e * 0.35f, e * 1.2f, Mathf.Max(1f, e * 0.3f)), ink, Solid(Color.white));
                else Shape(new Rect(x - e * 0.5f, ey, e, e), ink, disc);
            }
            PalFeature(cx, topY, R, feat, a);
        }

        void PalFeature(float cx, float topY, float R, string feat, float a)
        {
            var disc = PixelSprites.Disc().texture;
            var dia = PixelSprites.Diamond().texture;
            var m = GUI.matrix;
            var leaf = LeafCol; leaf.a = a;
            switch (feat)
            {
                case "leaf":
                {
                    var lr = new Rect(cx + R * 0.05f, topY - R * 0.32f, R * 0.70f, R * 0.32f);
                    GUIUtility.RotateAroundPivot(-32f, lr.center);
                    InkRing(lr, disc, a); Shape(lr, leaf, disc);
                    break;
                }
                case "sprout":
                {
                    Shape(new Rect(cx - R * 0.05f, topY - R * 0.36f, R * 0.10f, R * 0.40f), leaf, Solid(Color.white));
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var lr = new Rect(cx + (s < 0 ? -R * 0.42f : 0f), topY - R * 0.52f, R * 0.42f, R * 0.20f);
                        GUI.matrix = m;
                        GUIUtility.RotateAroundPivot(s * 22f, lr.center);
                        Shape(lr, leaf, disc);
                    }
                    break;
                }
                case "horn":
                {
                    var hr = new Rect(cx - R * 0.14f, topY - R * 0.42f, R * 0.28f, R * 0.58f);
                    var hc = new Color(0.85f, 0.87f, 0.89f, a);
                    InkRing(hr, dia, a); Shape(hr, hc, dia);
                    break;
                }
                case "bow":
                {
                    var bc = new Color(0.91f, 0.88f, 0.83f, a);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var br = new Rect(cx + (s < 0 ? -R * 0.46f : R * 0.04f), topY - R * 0.20f, R * 0.42f, R * 0.30f);
                        InkRing(br, dia, a); Shape(br, bc, dia);
                    }
                    break;
                }
            }
            GUI.matrix = m;
        }
    }
}
