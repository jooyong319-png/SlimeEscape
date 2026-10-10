using System.Collections.Generic;
using UnityEngine;

namespace SlimeEscape
{
    //  🔴 SnakeController 의 한 조각 (partial). 필드 · Awake · 판 진행은 SnakeController.cs.
    //     화면 — 판 짓기 · 그리기 · 빨려듦 · SyncViews
    //     2026-09-12 에 4,400줄짜리 한 파일을 구획대로 나눴다 — 내용은 한 글자도 안 바꿨다.
    public partial class SnakeController
    {
        // ---------------- 화면 ----------------
        Vector2 CellPos(int cell) => _org + new Vector2(_L.X(cell) + 0.5f, -(_L.Y(cell) + 0.5f));

        /// 🔴 **판 밖도 가리킨다.** 칸 번호로는 -1 열을 못 쓴다 (09-03: 바깥 한 겹).
        Vector2 CellXY(int x, int y) => _org + new Vector2(x + 0.5f, -(y + 0.5f));

        /// 판 밖은 전부 돌이다 — 실제로 못 지나가니 그렇게 그리는 게 맞다.
        bool WallAt(int x, int y)
            => x < 0 || y < 0 || x >= _L.W || y >= _L.H || _L.IsWall(y * _L.W + x);

        void BuildBoard()
        {
            if (_root != null) Destroy(_root.gameObject);
            _root = new GameObject("Board").transform;
            _segs.Clear(); _foodViews.Clear(); _foodExtra.Clear(); _links.Clear();
            _zoneDots.Clear(); _boxViews.Clear(); _boxTops.Clear(); _boxInner.Clear(); _ports.Clear();
            ResetEatFx();
            _iceShines.Clear(); _cracks.Clear();

            for (int y = 0; y < _L.H; y++)
                for (int x = 0; x < _L.W; x++)
                {
                    int c = y * _L.W + x;
                    bool wall = _L.IsWall(c);
                    var sr = NewSprite(wall ? "Wall" : "Floor", wall ? -3 : -5);   // 빈 칸은 -5: 그 사이(-4)에 벽 테두리 (10-10)
                    sr.transform.position = CellPos(c);
                    //  🔴 벽은 **둥근 네모**다 (09-03 사장님). 슬라임도 홈도 둥근데
                    //     벽만 각지면 딴 세계처럼 보인다. 이웃 사이는 다리로 메워서
                    //     바깥선만 둥글고 속은 한 덩어리가 되게 한다.
                    if (wall) sr.sprite = PixelSprites.Round();
                    //  빈 칸은 높이에 따라 밝기가 다르다 — 위가 밝고 아래가 가라앉는다
                    //  🔴 **얼음은 벽과 같은 덩어리지만 색이 다르다** (rev.12-a).
                    //     밟는 바닥이라 벽 자리에 있다. 벽(차가운 회청)보다 밝고 푸르게 —
                    //     "여기는 단단하지만 미끄럽다".
                    bool isIce = wall && _L.Ice.Contains(c);
                    sr.color = isIce ? IceCol
                             : wall ? Rock
                             : Color.Lerp(FloorLit, FloorDim, _L.H < 2 ? 0f : (float)y / (_L.H - 1));

                    //  🔴 금 간 바닥 — 벽처럼 딛고 서지만 지나가면 무너진다.
                    //     벽 덩어리에 안 붙이고 **낱장**으로 그린다. "이건 따로 떨어진 판이다".
                    if (!wall && _L.CrackIdx.ContainsKey(c))
                    {
                        var cr = new Crack { cell = c, t = 0f };
                        cr.slab = NewSprite("Crack", -3);
                        cr.slab.sprite = PixelSprites.Round();
                        cr.slab.transform.position = (Vector3)CellPos(c) + new Vector3(0, 0, -0.008f);
                        cr.slab.transform.localScale = new Vector3(0.94f, 0.94f, 1f);
                        cr.slab.color = CrackCol;
                        //  윗면 — "딛고 설 수 있다"는 신호. 벽·상자와 같은 말이다.
                        cr.top = NewSprite("CrackTop", -2);
                        cr.top.sprite = PixelSprites.Solid();
                        cr.top.transform.position = (Vector3)CellPos(c) + new Vector3(0, 0.40f, -0.009f);
                        cr.top.transform.localScale = new Vector3(0.90f, 0.10f, 1f);
                        cr.top.color = CrackTop;
                        //  금 둘 — 갈라진 자리
                        cr.l1 = NewSprite("CrackLine", -2);
                        cr.l1.sprite = PixelSprites.Solid();
                        //  🔴 둘을 서로 반대로 기울였더니 「V」 자로 읽혔다 (09-05 화면).
                        //     글자처럼 보이면 무늬가 아니라 표시가 된다 — **같은 쪽**으로 눕혀
                        //     엇갈리게 놓는다. 그래야 쪼개진 결로 보인다.
                        cr.l1.transform.position = (Vector3)CellPos(c) + new Vector3(-0.14f, 0.08f, -0.010f);
                        cr.l1.transform.localScale = new Vector3(0.050f, 0.56f, 1f);
                        cr.l1.transform.localRotation = Quaternion.Euler(0, 0, 22f);
                        cr.l1.color = CrackLine;
                        cr.l2 = NewSprite("CrackLine", -2);
                        cr.l2.sprite = PixelSprites.Solid();
                        cr.l2.transform.position = (Vector3)CellPos(c) + new Vector3(0.13f, -0.16f, -0.010f);
                        cr.l2.transform.localScale = new Vector3(0.040f, 0.34f, 1f);
                        cr.l2.transform.localRotation = Quaternion.Euler(0, 0, 30f);
                        cr.l2.color = CrackLine;
                        //  🔴 (10-09 디자인) 아래쪽 두께 — 납작한 색판이 아니라 **한 장의 돌판**으로
                        var under = NewSprite("CrackUnder", -2);
                        under.sprite = PixelSprites.Solid();
                        under.transform.position = (Vector3)CellPos(c) + new Vector3(0, -0.40f, -0.009f);
                        under.transform.localScale = new Vector3(0.86f, 0.10f, 1f);
                        var uc = CrackLine; uc.a = 0.55f; under.color = uc;
                        //  잔금 — 큰 금에서 갈라져 나간 짧은 결
                        var l3 = NewSprite("CrackLine", -2);
                        l3.sprite = PixelSprites.Solid();
                        l3.transform.position = (Vector3)CellPos(c) + new Vector3(-0.03f, -0.10f, -0.010f);
                        l3.transform.localScale = new Vector3(0.030f, 0.18f, 1f);
                        l3.transform.localRotation = Quaternion.Euler(0, 0, -38f);
                        var l3c = CrackLine; l3c.a = 0.8f; l3.color = l3c;
                        _cracks.Add(cr);
                    }

                    //  🔴 무중력 구역 — 뒷벽처럼 한 겹 깔고, 떠 있는 점 몇 개를 얹는다.
                    //     점이 **안 오르고 제자리에서 흔들리는** 것이 이 칸의 뜻이다
                    //     (홈의 불빛은 위로 오른다 — 헷갈리면 안 된다).
                    if (!wall && _L.Zone.Contains(c))
                    {
                        var z = NewSprite("Zone", -3);
                        z.sprite = PixelSprites.Soft();
                        //  🔴 같은 차례끼리는 번갈아 이긴다 — z 로 못박는다
                        z.transform.position = (Vector3)CellPos(c) + new Vector3(0, 0, -0.01f);
                        z.transform.localScale = new Vector3(0.96f, 0.96f, 1f);
                        z.color = ZoneCol;
                        for (int k = 0; k < 2; k++)
                        {
                            var dsr = NewSprite("ZoneDot", -2);
                            dsr.sprite = PixelSprites.Disc();
                            dsr.transform.localScale = new Vector3(0.1f, 0.1f, 1f);
                            var dc = ZoneDot; dc.a = 0.5f;
                            dsr.color = dc;
                            _zoneDots.Add((c, k, dsr));
                        }
                    }

                    //  🔴 포탈 — 가운데를 바닥색으로 뚫어 **고리**로 만든다.
                    //     채운 원으로 두면 '조각'이나 '별'로 잘못 읽힌다.
                    if (!wall && _L.Portal.TryGetValue(c, out var hop))
                    {
                        var pr = new Port { cell = c, door = hop.Door };
                        //  빛무리 — 칸 밖으로 번져 나간다. "여기 뭔가 있다"는 건 이게 말한다
                        pr.glow = NewSprite("PortalGlow", -3);
                        //  🔴 Soft() 는 둥근 **네모**라 빛무리로 쓰면 네모가 보인다
                        //     (09-05 사장님 화면). 가장자리로 갈수록 옅어지는 동그라미를 쓴다.
                        pr.glow.sprite = PixelSprites.Halo();
                        pr.glow.transform.position = (Vector3)CellPos(c) + new Vector3(0, 0, -0.009f);
                        //  테
                        pr.ring = NewSprite("Portal", -3);
                        pr.ring.sprite = PixelSprites.Disc();
                        pr.ring.transform.position = (Vector3)CellPos(c) + new Vector3(0, 0, -0.011f);
                        //  🔴 가운데는 **바닥색이 아니라 더 어둡게** 뚫는다.
                        //     바닥색으로 채우면 그냥 도넛 무늬가 되고, 어둡게 하면 **구멍**이 된다.
                        pr.hole = NewSprite("PortalIn", -3);
                        pr.hole.sprite = PixelSprites.Disc();
                        pr.hole.transform.position = (Vector3)CellPos(c) + new Vector3(0, 0, -0.012f);
                        //  도는 점 셋 — 짝끼리 **같은 각도**로 돈다
                        //  🔴 **빨려들어가는** 알갱이 넷. 바깥에서 생겨 가운데로 빨려든다.
                        pr.dots = new SpriteRenderer[4];
                        for (int k = 0; k < 4; k++)
                        {
                            var ds = NewSprite("PortalDot", -2);
                            ds.sprite = PixelSprites.Disc();
                            pr.dots[k] = ds;
                        }
                        _ports.Add(pr);
                    }

                    if (wall)
                    {
                        // ⚠️ 속 칸을 RockDeep 으로 덮던 줄은 뺐다 (09-03).
                        //    가장자리는 대부분 속 칸이라, 그 줄이 **페이드를 덮어써서**
                        //    정작 흐려져야 할 데가 안 흐려졌다. 납작한 그림에서
                        //    RockDeep 은 어차피 Rock 과 같은 색이다.
                        bool up = y > 0 && !_L.IsWall(c - _L.W);

                        // 윗면 — "여기 딛고 설 수 있다". 두고 온 몸의 윗면과 같은 신호다.
                        //  🔴 (10-10) 돌 윗면은 두툼한 덮개로 (SnakeController.Ink.cs). 얼음은 눈 얹은 지금 모양 그대로
                        if (up && !isIce) WallCap(x, y);
                        if (up && isIce)
                        {
                            var e = NewSprite("WallTop", -2);
                            e.transform.position = CellPos(c) + new Vector2(0f, 0.42f);
                            e.transform.localScale = new Vector3(1f, 0.16f, 1);
                            //  🔴 얼음은 **윗면이 다르다.** 슬라임이 걷는 건 이 면이라,
                            //     미끄럽다는 말은 여기서 해야 한다 — 밝게 얼려 놓는다.
                            e.color = isIce ? IceTop : RockTop;
                        }
                        //  🔴 얼음 윗면의 **결** — 누운 쪽이 곧 미끄러지는 쪽이다.
                        //     ⚠️ 칸 속이 아니라 **윗면 바로 아래**에 깐다. 속에 그리면
                        //        "안을 지나간다"로 읽혀 처음에 물린 그 그림이 된다.
                        if (isIce && up)
                        {
                            for (int k = 0; k < 2; k++)
                            {
                                var ln = NewSprite("IceLine", -2);
                                ln.sprite = PixelSprites.Solid();
                                ln.transform.position = CellPos(c)
                                    + new Vector2(k == 0 ? -0.14f : 0.16f, k == 0 ? 0.24f : 0.10f);
                                ln.transform.localScale = new Vector3(k == 0 ? 0.44f : 0.30f, 0.035f, 1f);
                                var lc = IceLine; lc.a = k == 0 ? 0.40f : 0.26f;
                                ln.color = lc;
                            }
                            //  🔴 (10-09 사장님: "지금 상태에서 위에 눈이 살짝 있게") — 윗면에 눈이 얇게 쌓인다.
                            //     칸마다 봉긋한 덩이 넷을 이어 붙여 한 줄로 읽히게 (덩이 크기는 칸마다 조금씩 다르다)
                            var snowBase = NewSprite("IceSnow", -1);
                            snowBase.sprite = PixelSprites.Solid();
                            snowBase.transform.position = CellPos(c) + new Vector2(0f, 0.485f);
                            snowBase.transform.localScale = new Vector3(1.0f, 0.05f, 1f);
                            snowBase.color = SnowCol;
                            for (int k = 0; k < 4; k++)
                            {
                                var sn = NewSprite("IceSnow", -1);
                                sn.sprite = PixelSprites.Disc();
                                float sx = Mathf.Lerp(-0.37f, 0.37f, k / 3f) + (Hash(c * 31 + k * 7) - 0.5f) * 0.06f;
                                sn.transform.position = CellPos(c) + new Vector2(sx, 0.495f);
                                sn.transform.localScale = new Vector3(Mathf.Lerp(0.26f, 0.34f, Hash(c * 17 + k * 3)),
                                                                      Mathf.Lerp(0.08f, 0.13f, Hash(c * 23 + k * 11)), 1f);
                                sn.color = SnowCol;
                            }
                            //  윗면을 훑고 지나가는 윤기 — Animate 가 좌우로 흘린다
                            var sh = NewSprite("IceShine", -2);
                            sh.sprite = PixelSprites.Soft();
                            sh.transform.localScale = new Vector3(0.30f, 0.16f, 1f);
                            var sc2 = IceShine; sc2.a = 0f;
                            sh.color = sc2;
                            _iceShines.Add((c, sh));
                        }
                        // 🔴 옆·아랫면 테는 **뺐다.** 돌마다 테가 둘리니 다시 타일로 보였고,
                        //    밝은 면이 너무 많아 빈 곳보다 돌이 밝아졌다 (08-31 화면).
                        //    밝은 건 윗면 하나뿐 — 그래야 "설 수 있다"가 신호로 남는다.
                    }
                }

            //  🔴 **판 밖에 한 겹 더** (09-03 사장님). 판의 마지막 칸은 온전한 돌로 두고,
            //     이 바깥 겹이 배경으로 녹는다 — 판이 네모난 상자로 끝나지 않는다.
            //     실제로 못 지나가는 자리라, 돌로 그리는 것이 거짓말이 아니다.
            for (int y = -1; y <= _L.H; y++)
                for (int x = -1; x <= _L.W; x++)
                {
                    if (x >= 0 && y >= 0 && x < _L.W && y < _L.H) continue;   // 판 안은 이미 그렸다
                    var sr = NewSprite("WallEdge", -3);
                    sr.sprite = PixelSprites.Round();
                    sr.transform.position = CellXY(x, y);
                    sr.color = Rock;
                    //  바깥쪽으로 흘려보낸다. 모서리 칸은 두 장이 겹쳐 더 짙어진다 — 제일 바깥이니까.
                    if (x == -1)      EdgeFade(x, y,   0f);
                    if (x == _L.W)    EdgeFade(x, y, 180f);
                    if (y == -1)      EdgeFade(x, y, 270f);
                    if (y == _L.H)    EdgeFade(x, y,  90f);
                }

            //  🔴 붙어 있는 벽을 **한 덩어리**로 잇는다. 오른쪽·아래만 보면 중복이 없다.
            //     네 칸이 만나는 자리도 메운다 — 깎인 모서리 넷이 모여 구멍이 남는다.
            //     🔴 **바깥 겹까지 같이** 잇는다. 판 안과 밖이 안 이어지면
            //        마지막 칸에 금이 하나 그어져 보인다.
            for (int y = -1; y <= _L.H; y++)
                for (int x = -1; x <= _L.W; x++)
                {
                    if (!WallAt(x, y)) continue;
                    bool wR = x + 1 <= _L.W && WallAt(x + 1, y);
                    bool wD = y + 1 <= _L.H && WallAt(x, y + 1);
                    if (wR) WallBridge(x, y, new Vector3(0.42f, 1f, 1), new Vector2(0.5f, 0f));
                    if (wD) WallBridge(x, y, new Vector3(1f, 0.42f, 1), new Vector2(0f, -0.5f));
                    if (wR && wD && WallAt(x + 1, y + 1))
                        WallBridge(x, y, new Vector3(0.42f, 0.42f, 1), new Vector2(0.5f, -0.5f));
                }

            // 🔴 목표 홈 — 바닥보다 어둡게 파인 것처럼. 몸이 덮으면 한 칸씩 빛이 찬다
            _holes.Clear();
            _holeSeams.Clear();
            _holeGlow.Clear();
            _holeEdgeExtra.Clear();
            _motes.Clear();
            _holeEdges.Clear();
            _seated.Clear(); _seatAt.Clear();
            _bridges.Clear();

            // 🔴 목표는 칸 여럿이 아니라 **몸이 들어갈 한 덩어리 홈**이다.
            //    칸마다 테두리를 치면 이어진 뱀 모양이 "구멍 여러 개"로 보인다.
            //    실제로 사람이 61초 동안 못 읽었다 (2026-08-29 검증).
            //    그래서 민트를 칸 가득 깔아 서로 붙게 하고, 어두운 속만 안쪽에 둔다.
            //    이웃한 칸 사이는 속끼리 다리로 이어 붙여 테두리가 안 끼게 한다.
            _doorOf.Clear();
            for (int di = 0; di < _L.Doors.Count; di++)
                foreach (int c in _L.Doors[di].Cells) _doorOf[c] = di;

            foreach (var kvDoor in _doorOf)
            {
                int c = kvDoor.Key;
                int dr = kvDoor.Value;
                //  🔴 테두리를 **슬라임과 같은 모양**으로 (09-03 사장님).
                //     ⚠️ 09-02 에 속만 둥글렸다가 되돌린 적이 있다 — 바깥이 네모면
                //        모양 언어가 둘로 싸워서 까만 얼룩처럼 보인다.
                //        이번엔 테두리·속·다리를 **전부** 같은 둥근 모양으로 둔다.
                var edge = NewSprite("HoleEdge", -1);
                edge.sprite = PixelSprites.Round();
                edge.transform.position = CellPos(c);
                edge.transform.localScale = new Vector3(1f, 1f, 1);
                edge.color = RailOf(dr);

                //  뒤에 번지는 빛 — "여기다" 를 멀리서도 알린다. 숨은 Animate 가 쉰다.
                //  바깥 겹부터 깔아야 안쪽 겹이 위에 온다 (같은 차례라 나중 것이 이긴다).
                for (int gi = GlowRing.Length - 1; gi >= 0; gi--)
                {
                    var glow = NewSprite("HoleGlow", -2);
                    glow.sprite = PixelSprites.Round();
                    //  🔴 겹이 넷 다 차례 -2 다. **같은 차례는 번갈아 이겨서**
                    //     매 프레임 순서가 바뀌면 빛이 떤다 (09-02 외벽이 끊겨 보이던 그 함정).
                    //     z 로 앞뒤를 못박는다 — 바깥 겹일수록 뒤로.
                    var gp = CellPos(c);
                    glow.transform.position = new Vector3(gp.x, gp.y, 0.01f * gi);
                    float sc = 1f + GlowSpread * GlowRing[gi].k;
                    glow.transform.localScale = new Vector3(sc, sc, 1);
                    _holeGlow.Add((dr, glow, GlowRing[gi].a));
                }

                //  🔴 **덩어리 바깥선에만** 레일을 둔다. 칸마다 두르면
                //     이어진 모양이 "구멍 여럿 개"로 보인다 (08-29에 사람이 61초 동안 못 읽음).
                bool nU = _doorOf.TryGetValue(c - _L.W, out int du) && du == dr;
                bool nD = _doorOf.TryGetValue(c + _L.W, out int dd2) && dd2 == dr;
                bool nL = _doorOf.TryGetValue(c - 1, out int dl) && dl == dr;
                bool nR = _doorOf.TryGetValue(c + 1, out int drr) && drr == dr;

                //  ⚠️ 네모 레일(Rail)은 걷어냈다 (09-03). 둥근 테두리가 이미 윤곽이라
                //     두 벌이 겹쳐 모서리가 지저분해졌다.
                _ = nU; _ = nD; _ = nL;

                //  꾺이는 모서리에 장식을 박는다 — 참고 그림에서 틀을 틀로 만드는 것이 이것이다.
                //  🔴 모서리 밖으로 반쯤 나가 있으면 핀처럼 보인다.
                //     틀 **위에** 얙히는 자리로 당긴다 (09-02 화면).
                //  ⚠️ 모서리 장식은 걷어냈다 (09-02 사장님 그림).
                //     틀이 화려해지면 정작 봐야 하는 **마름모**를 이긴다.
                //     그리는 함수(Gem)는 남겨둔다 — 되돌리기 쉽게.

                //  ⚠️ 칸 나누는 선(Seam)은 걷어냈다 (09-03 사장님 화면).
                //     네모 틀일 땐 "몇 칸인가"를 세어줬는데, 둥근 덩어리에서는
                //     **붙어야 할 것을 끊어** 상자 여럿으로 보이게 만들었다.
                //     칸 수는 몸이 들어가면서 밝아지는 것으로 이미 세어진다.
                _ = nR; _ = nD;

                // ⚠️ 한번 슬라임 모양의 **거푸집**으로 파봤다가 되돌렸다 (09-02).
                //    바깥은 네모 틀인데 안은 둥근 덩어리가 되어 **모양 언어가 둘로 싸웠다** —
                //    까만 얼룩처럼 보였다. 바꿀 거면 틀과 속을 **같은 모양으로** 바꿔야 한다.
                var inner = NewSprite("HoleInner", 0);
                inner.sprite = PixelSprites.Round();
                inner.transform.position = CellPos(c);
                inner.transform.localScale = new Vector3(SlotInner, SlotInner, 1);
                inner.color = HoleCol;

                // ⚠️ 홈 아래 안쪽의 **빛 띠(HoleLip)** 는 걷어냈다 (09-02).
                //    레일과 차례가 같아서(둘 다 1) 서로 번갈아 이겼고,
                //    그래서 외벽이 군데군데 끊겨 보였다 — 사장님이 잡아내신 그 버그다.
                //    납작한 그림에는 이런 깊이 표현이 애초에 없다.
                _holes[c] = inner;
                _holeEdges[c] = edge;

                //  🔴 (10-10 사장님: "홈은 칸마다 나눠줘 몇칸인지 간접적으로" → 시안 C 「안쪽 점선」)
                //     ⚠️ 09-03 의 칸 나누는 선은 테두리까지 끊겨 상자 여럿으로 보였다 · 10-10 의 칸마다 옅은 네모는 "별론데".
                //     칸 경계에 **아주 옅은 점선**만 긋고, 테두리에는 안 닿게 양 끝을 비운다.
                //     오른쪽 · 아래 이웃만 보면 경계마다 한 번씩 긋는다.
                if (nR) AddSeam(c, c + 1, dr, true);
                if (nD) AddSeam(c, c + _L.W, dr, false);

                // 🔴 굳은 몸의 윗면 — 평소엔 숨어 있다가 문이 열리면 나타난다.
                //    벽의 윗면과 같은 신호라, 여기 설 수 있다는 게 바로 읽힌다.
                var top = NewSprite("SpentTop", 1);
                top.transform.position = CellPos(c) + new Vector2(0f, 0.42f);
                top.transform.localScale = new Vector3(1f, 0.16f, 1);
                top.color = SpentTop;
                top.enabled = false;
                _spentTop[c] = top;
            }

            // 🔴 화살표 표지판 — 맵에 떠 있는 안내. 테두리 · 속 · 화살표 세 겹.
            //    지형·조각·홈 어느 것과도 안 닮게 푸른빛 네모로 둔다.
            foreach (var s in _L.Signs)
            {
                var at = CellPos(s.cell) + new Vector2(0f, 0.06f);
                var frame = NewSprite("Sign", 4);
                frame.transform.position = at;
                frame.transform.localScale = new Vector3(0.74f, 0.74f, 1);
                frame.color = SignFrame;

                var fill = NewSprite("SignFill", 5);
                fill.transform.position = at;
                fill.transform.localScale = new Vector3(0.62f, 0.62f, 1);
                fill.color = SignFill;

                // 화살표 = 자루 하나 + 45도 돌린 네모(머리) 하나
                float ax = s.dir == 2 ? -1 : s.dir == 3 ? 1 : 0;
                float ay = s.dir == 0 ? 1 : s.dir == 1 ? -1 : 0;
                bool horiz = ax != 0;

                var shaft = NewSprite("SignShaft", 6);
                shaft.transform.position = at - new Vector2(ax, ay) * 0.06f;
                shaft.transform.localScale = horiz ? new Vector3(0.34f, 0.11f, 1)
                                                   : new Vector3(0.11f, 0.34f, 1);
                shaft.color = SignArrow;

                var head = NewSprite("SignHead", 6);
                head.transform.position = at + new Vector2(ax, ay) * 0.16f;
                head.transform.localScale = new Vector3(0.20f, 0.20f, 1);
                head.transform.rotation = Quaternion.Euler(0, 0, 45f);
                head.color = SignArrow;
            }

            // 🔴 받침대 — 몸을 얹어두는 선반. 홈과 달리 채워도 판이 안 끝난다.
            _padViews.Clear(); _padTops.Clear();
            foreach (int c in _L.Pads)
            {
                var pad = NewSprite("Pad", -1);
                pad.transform.position = CellPos(c) + new Vector2(0f, -0.22f);
                pad.transform.localScale = new Vector3(0.96f, 0.5f, 1);
                pad.color = PadCol;
                _padViews[c] = pad;

                var lip = NewSprite("PadTop", 1);
                lip.transform.position = CellPos(c) + new Vector2(0f, 0.02f);
                lip.transform.localScale = new Vector3(0.96f, 0.1f, 1);
                lip.color = PadEdge;
                _padTops[c] = lip;
            }

            // 속끼리 잇는 다리 — 오른쪽·아래 이웃만 보면 중복 없이 다 이어진다
            // 🔴 다리는 **같은 문끼리만** 잇는다. 다른 문끼리 이으면 한 덩어리로 보인다.
            foreach (var kvDoor in _doorOf)
            {
                int c = kvDoor.Key, mine = kvDoor.Value;
                int x = _L.X(c), y = _L.Y(c);
                bool hasR = x + 1 < _L.W && _doorOf.TryGetValue(c + 1, out var r) && r == mine;
                bool hasD = y + 1 < _L.H && _doorOf.TryGetValue(c + _L.W, out var d) && d == mine;
                //  🔴 다리를 **넉넉히** 겹친다. 딱 맞추면 둥근 모서리가 깎인 만큼
                //     이음매에서 형광이 새어 상자 여럿으로 보인다 (09-03 사장님 화면).
                if (hasR)
                {
                    RimBridge(c, mine, new Vector3(0.40f, 1f, 1), new Vector2(0.5f, 0f));
                    AddBridge(c, c + 1, new Vector3(0.44f, SlotInner, 1), new Vector2(0.5f, 0f));
                }
                if (hasD)
                {
                    RimBridge(c, mine, new Vector3(1f, 0.40f, 1), new Vector2(0f, -0.5f));
                    AddBridge(c, c + _L.W, new Vector3(SlotInner, 0.44f, 1), new Vector2(0f, -0.5f));
                }
                //  네 칸이 만나는 자리 — 깎인 모서리 넷이 모여 형광 점이 남는다
                if (hasR && hasD
                    && _doorOf.TryGetValue(c + _L.W + 1, out var dr2) && dr2 == mine)
                    AddBridge(c, c + _L.W + 1, new Vector3(0.44f, 0.44f, 1),
                              new Vector2(0.5f, -0.5f));
            }

            //  홈이 다 서고 나서 불빛을 띄운다 — 오르는 구간을 홈에서 재기 때문이다
            MakeMotes();
            MakePuffs();
            MakeBubbles();
            //  꼬리 속에서 부푸는 조각. 거품(7)과 같은 차례지만 z 로 뒤에 둔다 —
            //  거품이 덩어리 **앞에서** 떠올라야 속에 든 것으로 보인다.
            _swallow = NewSprite("Swallow", 7);
            _swallow.sprite = PixelSprites.Round();
            _swallow.enabled = false;

            //  🔴 시작 칸에서 번지는 빛. 슬라임이 **여기서 나왔다**를 말해준다.
            //     심의 울컥임(Gulp)과 같은 말투로 둔다 — 하나는 삼키고 하나는 뱉는다.
            _bornFlash = _bornRing = _bornCore = null;
            if (_L.Start >= 0)
            {
                var sp = CellPos(_L.Start);
                _bornFlash = NewSprite("BornFlash", 1);
                _bornFlash.sprite = PixelSprites.Disc();
                _bornFlash.transform.position = sp;
                _bornFlash.enabled = false;

                //  🔴 홈의 심과 **똑같은 마름모·똑같은 비율**이다 (0.40 : 0.55).
                //     몸(차례 3)보다 뒤(2)에 둬서, 나오는 슬라임이 이걸 덮는다.
                _bornRing = NewSprite("BornRing", 2);
                _bornRing.sprite = PixelSprites.Diamond();
                _bornRing.transform.position = sp;
                _bornRing.enabled = false;

                _bornCore = NewSprite("BornCore", 2);
                _bornCore.sprite = PixelSprites.Diamond();
                _bornCore.transform.position = new Vector3(sp.x, sp.y, -0.01f);
                _bornCore.enabled = false;
            }

            // 심 — 머리가 마지막에 있어야 할 자리.
            // 🔴 채워진 홈에서는 **지워야 한다.** 안 지우면 노란 표시가 그대로 남아서
            //    "어느 게 내 슬라임이지?" 가 된다 (08-31 사장님 화면).
            _coreViews.Clear();
            for (int di = 0; di < _L.Doors.Count; di++)
            {
                var dd = _L.Doors[di];
                if (dd.Core < 0) continue;
                var ring = NewSprite("CoreRing", 1);
                ring.transform.position = CellPos(dd.Core);
                //  🔴 **구멍처럼** 그린다 — 밝은 테 + 어두운 속.
                //     전에는 밝은 알맹이라 "구멍"이 아니라 놓여 있는 **물건**으로 보였다.
                //     머리에 박힌 열쇠가 꽉 찬 것이니, 구멍은 비어 있어야 짝이 맞는다.
                ring.transform.localScale = Vector3.one * 0.52f;
                ring.color = CoreCol;
                //  🔴 머리에 박힌 열쇠와 **같은 모양·같은 비율**이라야
                //     "여기에 끼워라"가 말 없이 읽힌다 (09-02 사장님 그림의 "=").
                //     세로로 길다 — 머리 쪽 0.42:0.58 과 같은 비다.
                ring.sprite = PixelSprites.Diamond();
                ring.transform.localScale = new Vector3(0.40f, 0.55f, 1f);
                //  🔴 그림에는 테가 이미 들어 있다. 코드 테를 같이 그리면
                //     노란 네모가 샐져나와 홈 한 칸이 크림색 상자로 보였다 (09-02 화면).

                var core = NewSprite("Core", 2);
                core.transform.position = CellPos(dd.Core);
                core.transform.localScale = Vector3.one * 0.30f;
                core.color = HoleCol;
                core.sprite = PixelSprites.Diamond();
                core.transform.localScale = new Vector3(0.23f, 0.32f, 1f);
                _coreViews.Add((di, ring, core));
            }

            // ⚠️ 격자선은 **걷어냈다** (09-02). 칸마다 선을 그은 것이
            //    "굴이 아니라 모눈종이"의 진짜 원인이었다 — 벽이 밋밋해서가 아니었다.
            //    붙어 있는 벽은 이음매 없이 하나의 실루엣이어야 방으로 읽힌다.

            foreach (int c in _L.Foods)
            {
                // 🔴 주황 네모는 "블록"이다. 둥글게 하고 위에 반짝임을 얹으면 **먹는 것**이 된다.
                var halo = NewSprite("FoodHalo", 0);
                halo.transform.position = CellPos(c);
                //  🔴 (10-09) 꽉 찬 원 빛은 어두운 바닥 위에서 **갈색 고리 = 구멍**으로 읽혔다 → 가장자리로 옅어지는 빛
                halo.transform.localScale = Vector3.one * 0.95f;
                halo.sprite = PixelSprites.Halo();
                halo.color = new Color(FoodCol.r, FoodCol.g, FoodCol.b, 0.30f);

                var sr = NewSprite("Food", 1);
                sr.transform.position = CellPos(c);
                sr.transform.localScale = Vector3.one * 0.38f;
                sr.sprite = PixelSprites.Disc();
                sr.color = FoodCol;
                _foodViews.Add(sr);

                var shine = NewSprite("FoodShine", 2);
                shine.transform.position = CellPos(c) + new Vector2(-0.07f, 0.08f);
                shine.transform.localScale = Vector3.one * 0.13f;
                shine.sprite = PixelSprites.Disc();
                shine.color = new Color(1f, 1f, 1f, 0.55f);
                _foodExtra.Add(halo); _foodExtra.Add(shine);
            }

            // 🔴 카메라는 AimCamera가 잡는다 — 방이 여럿이면 **지금 방만** 비춘다.
            //    지도를 통째로 보여주면 "방"이 안 생긴다 (2026-08-29 확인).

            // 🔴 문벽 — 열리면 사라진다. 벽보다 밝고 테두리가 있어 "열릴 수 있는 벽"으로 보인다.
            _gateViews.Clear(); _gateOf.Clear(); _shownDm = -1;
            for (int gi = 0; gi < _gateFadeOf.Length; gi++) _gateFadeOf[gi] = 0f;
            for (int gi = 0; gi < _L.Gates.Length; gi++)
                foreach (int c in _L.Gates[gi])
                {
                    var edge = NewSprite("Gate", -2);
                    edge.transform.position = CellPos(c);
                    edge.transform.localScale = new Vector3(0.98f, 0.98f, 1);
                    edge.color = GateEdge;
                    var inner = NewSprite("GateIn", -1);
                    inner.transform.position = CellPos(c);
                    inner.transform.localScale = new Vector3(0.78f, 0.78f, 1);
                    inner.color = GateCol;
                    _gateViews[c] = edge;
                    _gateOf[c] = gi;
                    _gateInner[c] = inner;
                }
            BuildSwitchViews();
            BuildSwapViews();
            BuildWallInk();
            if (!_demo) BuildBackdrop(); else _backBlobs.Clear();

            // 🔴 별 — 먹어도 몸이 안 자란다. 그래서 조각(주황)과 **다른 색·다른 크기**로 둔다.
            //    헷갈리면 사람이 문 계산을 틀린다.
            _starView = _starGlow = null;
            //  🔴 (10-10) 별 자리에 **수정에 갇힌 친구**를 둔다 — 규칙은 별 그대로 (SnakeController.Friend.cs)
            _frBody = null;
            if (_L.Star >= 0) BuildFriend(CellPos(_L.Star));

        }

        /// <summary>
        /// 🔴 벙돌을 칸마다 골라 쓴다. 넓은 벽이 한 그림이면
        /// 도트로 바꿔놓고도 다시 **모눈종이**처럼 보인다 (09-02 사장님 참고 그림).
        ///
        /// 🔴 섮되 **아무렇게 섮지 않는다.** 칸 자리로 정해지는 값을 쓴다 —
        /// 같은 판은 언제 봐도 같아야 한다. 매번 달라지면 어제 본 판이 딴 판이 된다.
        ///
        /// 룬·결정은 **드물게만** 내보낸다. 흔해지면 무늬가 아니라 배경 소음이 된다.
        /// </summary>
        /// 돌 가장자리의 옅은 테. 윗면(설 수 있다)과 헷갈리지 않게 훨씬 어둡다.

        /// 판 위에 네모 한 장. 모양을 바꾸려면 부르는 쪽에서 sprite 를 갈아 끼운다.
        SpriteRenderer NewSprite(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelSprites.Solid();
            sr.sortingOrder = order;
            return sr;
        }

        /// 빨려드는 진행도 0~1. 이 값으로 홈이 서서히 돌이 된다.
        float SuckT => _drainDone <= 0f ? 1f
                     : Mathf.Clamp01((Time.time - _drainAt) / _drainDone);

        void ClearSuck()
        {
            foreach (var b in _blobs) if (b.sr != null) Destroy(b.sr.gameObject);
            _blobs.Clear();
            if (_gulp != null) { Destroy(_gulp.gameObject); _gulp = null; }
            _drainCore = -1; _drainAt = 0f; _drainDone = 0f; _lastGulp = -1f;
        }

        /// <summary>
        /// 🔴 홈이 채워진 순간. 몸은 이미 홈 칸을 정확히 덮고 있으므로,
        /// **홈을 따라** 심까지 가는 길을 칸마다 하나씩 깔고 그 위로 흘려보낸다.
        /// 심에서 먼 칸이 먼저 출발해야 바깥부터 딸려 들어가는 것처럼 보인다.
        /// </summary>
        void StartSuck(int door)
        {
            ClearSuck();
            if (door < 0 || door >= _L.Doors.Count) return;
            var d = _L.Doors[door];
            _drainCore = d.Core >= 0 ? d.Core : d.Cells[0];

            // 심에서 홈을 따라 너비우선 — 칸마다 "심 쪽으로 한 걸음"을 적어둔다
            var toward = new Dictionary<int, int>();
            var hop = new Dictionary<int, int> { [_drainCore] = 0 };
            var q = new Queue<int>(); q.Enqueue(_drainCore);
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                foreach (int n in new[] { c - 1, c + 1, c - _L.W, c + _L.W })
                {
                    if (!d.Set.Contains(n) || hop.ContainsKey(n)) continue;
                    if (Mathf.Abs(_L.X(n) - _L.X(c)) + Mathf.Abs(_L.Y(n) - _L.Y(c)) != 1) continue;
                    hop[n] = hop[c] + 1; toward[n] = c; q.Enqueue(n);
                }
            }

            float far = 0f;
            foreach (var kv in hop) far = Mathf.Max(far, kv.Value);

            foreach (int c in d.Cells)
            {
                if (c == _drainCore || !hop.ContainsKey(c)) continue;
                var path = new List<Vector2> { CellPos(c) };
                int cur = c;
                while (cur != _drainCore && toward.ContainsKey(cur))
                { cur = toward[cur]; path.Add(CellPos(cur)); }

                var sr = NewSprite("Drain", 6);
                sr.transform.position = path[0];
                sr.color = BodyCol;
                // 먼 칸부터 출발 — 바깥이 먼저 딸려간다
                _blobs.Add(new Blob
                {
                    sr = sr, path = path, len = path.Count - 1,
                    when = (far - hop[c]) * DrainWave,
                });
            }

            //  🔴 거품과 **같은 차례(7)** 였다 — 같은 차례는 번갈아 이긴다. 위로 뺀다.
            _gulp = NewSprite("Gulp", 12);
            _gulp.transform.position = CellPos(_drainCore);
            _gulp.enabled = false;

            float last = 0f;
            foreach (var b in _blobs) last = Mathf.Max(last, b.when + b.len / DrainSpeed);
            _drainDone = last + DrainGulp;
            _drainAt = Time.time;
        }

        /// 길을 따라 t칸만큼 간 자리와, 그 순간 향하는 쪽
        static Vector2 Along(List<Vector2> path, float t, out Vector2 dir)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt(t), 0, path.Count - 2);
            float f = Mathf.Clamp01(t - i);
            dir = (path[i + 1] - path[i]).normalized;
            return Vector2.Lerp(path[i], path[i + 1], f);
        }

        void TickSuck()
        {
            if (_blobs.Count == 0) return;
            float el = Time.time - _drainAt;
            bool any = false;

            foreach (var b in _blobs)
            {
                if (b.sr == null) continue;
                float u = (el - b.when) * DrainSpeed;      // 지금까지 간 칸 수
                if (u < 0f) { b.sr.enabled = true; any = true; continue; }
                if (u >= b.len)
                {
                    if (b.sr.enabled) { b.sr.enabled = false; _lastGulp = el; }
                    continue;
                }
                any = true;

                var pos = Along(b.path, u, out var dir);
                b.sr.transform.position = pos;

                // 🔴 여기가 "쭈아아악"이다 — **가는 쪽으로 늘어나고 옆으로 홀쭉해진다.**
                float speed = Mathf.Clamp01(u / Mathf.Max(0.5f, b.len));
                float stretch = 1f + 0.85f * speed;
                float thin = 1f / (1f + 0.75f * speed);
                bool horiz = Mathf.Abs(dir.x) > Mathf.Abs(dir.y);
                b.sr.transform.localScale = horiz
                    ? new Vector3(0.94f * stretch, 0.94f * thin, 1)
                    : new Vector3(0.94f * thin, 0.94f * stretch, 1);

                var c = Color.Lerp(BodyCol, Color.white, speed * 0.55f);
                c.a = 1f - speed * 0.25f;
                b.sr.color = c;
            }

            // 심이 울컥한다 — 하나 삼킬 때마다
            if (_gulp != null)
            {
                float since = _lastGulp < 0f ? 99f : el - _lastGulp;
                bool on = since < DrainGulp;
                _gulp.enabled = on;
                if (on)
                {
                    float v = 1f - since / DrainGulp;
                    float s = 0.85f + 0.7f * v;
                    _gulp.transform.localScale = new Vector3(s, s, 1);
                    var gc = Color.Lerp(CoreCol, Color.white, v);
                    gc.a = 0.30f + 0.55f * v;
                    _gulp.color = gc;
                    any = true;
                }
            }

            if (!any) ClearSuck();
        }

        void Restart()
        {
            _st = SnakeEngine.StartState(_L);
            ClearSuck();
            _steps = 0;          // 🔴 여기를 빼먹으면 걸음이 판을 넘어 쌓여 커트라인이 무의미해진다
            _undo.Clear();
            _won = false;
            _pop = 0; _stepT = 999f; _jigT = 99f; _jigX = _jigY = 0f;
            _fall = 0; _slid = 1; _land = 0f; _landed = true;
            _segPos.Clear(); _from.Clear(); _mid.Clear();
            _boxDrawn.Clear(); _boxFrom.Clear(); _boxSq.Clear();
            _warpIn = _warpOut = -1; _warpT = 9f;   //  건너뛰기 표시도 지운다
            _segPos.Add(CellPos(_st.Head)); _from.Add(_segPos[0]); _mid.Add(_segPos[0]);
            _bornAt = Time.time;
            SyncViews();
        }

        /// 마디 스프라이트 개수를 몸 길이에 맞춘다
        void SyncViews()
        {
            //  🔴 상자는 **자리가 바뀐다** — 판이 아니라 몸처럼 매 걸음 따라 그린다.
            while (_boxViews.Count < _st.Bx.Count)
            {
                var b = NewSprite("Box", 1);
                //  🔴 (10-09 디자인) 둥근 덩어리는 **항아리**로 읽혔다 → 모서리만 살짝 둥근 네모
                b.sprite = PixelSprites.Soft();
                b.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                b.color = BoxCol;
                _boxViews.Add(b);
                //  윗면 — "여기 딛고 설 수 있다". 벽 윗면과 같은 신호다.
                //  안쪽에 한 단 들어간 판 — 상자 옆면
                var inn = NewSprite("BoxIn", 2);
                inn.sprite = PixelSprites.Soft();
                inn.color = BoxIn;
                _boxInner.Add(inn);
                var e = NewSprite("BoxTop", 3);
                e.transform.localScale = new Vector3(0.9f, 0.14f, 1f);
                e.color = BoxTop;
                _boxTops.Add(e);
            }
            for (int i = 0; i < _boxViews.Count; i++)
            {
                bool on = i < _st.Bx.Count;
                _boxViews[i].enabled = on;
                _boxTops[i].enabled = on;
                if (i < _boxInner.Count) _boxInner[i].enabled = on;
                if (!on) continue;
                //  🔴 칸 자리가 아니라 **보간된 자리**를 쓴다 — 안 그러면 순간이동한다
                Vector2 p = i < _boxDrawn.Count ? _boxDrawn[i] : CellPos(_st.Bx[i]);
                float sq = i < _boxSq.Count ? _boxSq[i] : 0f;
                float bw = 0.9f + sq, bh = 0.9f - sq;
                _boxViews[i].transform.position = (Vector3)p + new Vector3(0, -sq * 0.5f, -0.05f);
                _boxViews[i].transform.localScale = new Vector3(bw, bh, 1f);
                _boxTops[i].transform.position = (Vector3)p + new Vector3(0, bh * 0.45f - sq * 0.5f, -0.06f);
                _boxTops[i].transform.localScale = new Vector3(bw, 0.14f, 1f);
                if (i < _boxInner.Count)
                {
                    _boxInner[i].transform.position = (Vector3)p + new Vector3(0, -0.06f - sq * 0.5f, -0.055f);
                    _boxInner[i].transform.localScale = new Vector3(bw * 0.66f, bh * 0.56f, 1f);
                }
            }

            while (_segs.Count < _st.Length)
            {
                // 🔴 **머리가 맨 위**에 와야 한다. 예전엔 차례를 번갈아 매겨서
                //    뒤쪽 몸이 머리를 덮었다 — 얼굴이 뒤로 밀려 보였다 (09-02 사장님).
                //    머리 4 · 몸 3 · 이음매 2 로 못박는다.
                //  🔴 머리를 이음매(5)보다 **위**로. 안 그러면 이음매가 얼굴을 덮어
                //     눈이 사라진다 (09-02 화면). 마디 좁은 변에는 어두운 윤곽이 없어서
                //     머리를 위로 올려도 이음매와 경계가 안 생긴다.
                bool isHead = _segs.Count == 0;
                var sr = NewSprite("Seg", isHead ? 6 : 3);
                _segs.Add(sr);
            }
            for (int i = 0; i < _segs.Count; i++)
            {
                _segs[i].enabled = i < _st.Length;
                // ⚠️ 모양은 **Animate 가 정한다** — 눌린 만큼 밑이 퍼진 것으로 갈아 낀다.
                //    여기서도 정하면 매 프레임 서로 덮어써서 출렁임이 죽는다.
            }
            FaceUp();
            for (int i = 0; i < _foodExtra.Count; i++)
            {
                int fi = i / 2;                                   // 조각마다 빛+반짝임 둘
                if (fi < _foodViews.Count) _foodExtra[i].enabled = !SnakeEngine.IsEaten(_st, fi);
            }
            for (int i = 0; i < _foodViews.Count; i++)
            {
                _foodViews[i].enabled = !SnakeEngine.IsEaten(_st, i);
                //  🔴 (10-09) 조각이 **살짝 뜬다** — 먹을 것은 살아 있어 보여야 한다. 조각마다 박자를 어긋낸다
                if (i < _L.Foods.Count)
                {
                    Vector2 fp = CellPos(_L.Foods[i]) + new Vector2(0f, 0.035f * Mathf.Sin(Time.time * 2.4f + i * 1.3f));
                    _foodViews[i].transform.position = fp;
                    if (2 * i + 1 < _foodExtra.Count) _foodExtra[2 * i + 1].transform.position = fp + new Vector2(-0.08f, 0.09f);
                }
                // 🔴 힌트 2 — 먹으면 안 되는 조각은 잿빛으로 죽인다.
                //    이 게임에서 제일 안 보이는 실패가 "너무 먹은 것"이다.
                bool skip = _hint >= 2 && i < _L.Foods.Count && _skipFood.Contains(_L.Foods[i]);
                _foodViews[i].color = skip ? new Color(0.42f, 0.40f, 0.36f, 0.55f) : FoodCol;
            }

            // 목표 홈에 몸이 들어간 칸은 밝아진다 — "몇 칸 남았는지"가 눈에 보이게
            TickFriend();   // (10-10) 별 대신 갇힌 친구
            // 🔴 별 — 안 주웠으면 천천히 돈다. 주우면 사라진다.
            if (_starView != null)
            {
                bool got = _st.Sc != 0;
                _starView.enabled = !got;
                _starGlow.enabled = !got;
                if (!got)
                {
                    float b = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.2f);
                    float s = 0.48f + 0.05f * b;
                    _starView.transform.localScale = new Vector3(s, s, 1);
                    _starView.transform.rotation = Quaternion.Euler(0, 0, Time.time * 40f);
                    var gc = StarLit; gc.a = 0.24f + 0.12f * b;
                    _starGlow.color = gc;
                }
            }

            // 🔴 문이 열리면 그 문벽이 사라지고, 그 홈은 "두고 온 몸"이 된다.
            //    화면을 매번 다시 만들지 않고 색만 바꾼다 — 몸이 움직이는 중에도 안 튄다.
            // 🔴 문벽이 툭 사라지면 허전하다 — 스르르 꺼지게 한다
            //  🔴 포탈 — 짝끼리 **같은 각도로** 돈다. 그게 "이 둘은 하나"라는 말이다.
            //     켜지기 전엔 어둡고 안 돈다 — 아직 아무 일도 안 일어난다는 뜻이다.
            foreach (var pr in _ports)
            {
                bool on = (_st.Dm & (1 << pr.door)) != 0;
                Vector2 at = CellPos(pr.cell);
                //  숨쉬듯 커졌다 작아진다 — 켜졌을 때만
                float pulse = on ? 1f + Mathf.Sin(Time.time * 2.2f + pr.door * 1.7f) * 0.07f : 1f;

                //  빛무리 — 켜지면 번지고, 꺼지면 없다
                var gc = PortGlow; gc.a = on ? 0.30f + Mathf.Sin(Time.time * 2.2f + pr.door * 1.7f) * 0.07f : 0f;
                pr.glow.color = gc;
                float gs = 1.45f * pulse;
                pr.glow.transform.localScale = new Vector3(gs, gs, 1f);

                //  테 — 🔴 크기를 **매 프레임 되돌린다.** 안 그러면 건너뛰기 표시가 키워놓은
                //       크기가 그대로 굳는다.
                pr.ring.color = on ? PortOn : PortOff;
                float rs = 0.82f * pulse;
                pr.ring.transform.localScale = new Vector3(rs, rs, 1f);

                //  구멍 — 어두울수록 "뚫렸다"로 읽힌다
                var hc = PortHole; hc.a = on ? 1f : 0.65f;
                pr.hole.color = hc;
                float hs = 0.5f * pulse;
                pr.hole.transform.localScale = new Vector3(hs, hs, 1f);

                //  🔴 **빨려들어가는 느낌** (09-05 사장님). 알갱이가 바깥에서 생겨
                //     **가운데로 감기며 빨려든다.** 제자리에서 도는 것과는 뜻이 다르다 —
                //     도는 건 "여기 뭐가 있다", 빨려드는 건 "여기로 들어간다" 이다.
                //     🔴 짝끼리 **같은 박자**로 움직인다 (칸이 아니라 door 로 위상을 정한다).
                int nd = pr.dots.Length;
                for (int k = 0; k < nd; k++)
                {
                    var dsr = pr.dots[k];
                    dsr.enabled = on;
                    if (!on) continue;
                    //  0 → 1 을 도는 값. 알갱이마다 시작이 어긋나 줄지어 빨려든다
                    float u = Mathf.Repeat(Time.time * 0.85f + pr.door * 0.37f + k / (float)nd, 1f);
                    float rad = Mathf.Lerp(0.62f, 0.06f, u * u);        // 안으로 갈수록 빨라진다
                    float a2 = (u * 2.4f + k / (float)nd) * Mathf.PI * 2f + pr.door * 1.1f;
                    //  들어올 때 살짝 뜨고, 가운데서 사라진다
                    float fade = Mathf.Min(1f, u * 6f) * (1f - Mathf.Clamp01((u - 0.75f) / 0.25f));
                    var dc2 = PortOn; dc2.a = 0.95f * fade;
                    dsr.color = dc2;
                    float dsz = Mathf.Lerp(0.17f, 0.05f, u);            // 멀어질수록 작아진다
                    dsr.transform.localScale = new Vector3(dsz, dsz, 1f);
                    dsr.transform.position = (Vector3)at
                        + new Vector3(Mathf.Cos(a2) * rad, Mathf.Sin(a2) * rad, -0.02f);
                }
            }

            //  🔴 건너뛴 자리에 고리가 한 번 크게 퍼진다. 들어간 쪽·나온 쪽 둘 다.
            if (_warpT < 1f)
            {
                _warpT = Mathf.Min(1f, _warpT + Time.deltaTime / 0.35f);
                foreach (var pr in _ports)
                {
                    bool inp = pr.cell == _warpIn, outp = pr.cell == _warpOut;
                    if (!inp && !outp) continue;
                    //  🔴 들어가는 쪽은 **오므라들고**, 나오는 쪽은 **퍼진다.**
                    //     둘 다 퍼지게 하면 "빨려들어갔다"가 아니라 그냥 반짝인 게 된다.
                    float s = inp ? Mathf.Lerp(1.35f, 0.35f, _warpT)
                                  : Mathf.Lerp(0.35f, 1.55f, _warpT);
                    pr.ring.transform.localScale = new Vector3(s, s, 1f);
                    var c2 = PortOn; c2.a = inp ? 1f - _warpT * 0.4f : 1f - _warpT;
                    pr.ring.color = c2;
                    //  빛무리도 같이 — 들어갈 땐 잦아들고 나올 땐 확 번진다
                    float g2s = (inp ? Mathf.Lerp(2.0f, 0.7f, _warpT) : Mathf.Lerp(0.7f, 2.2f, _warpT));
                    pr.glow.transform.localScale = new Vector3(g2s, g2s, 1f);
                    var gc2 = PortGlow; gc2.a = 0.55f * (1f - _warpT);
                    pr.glow.color = gc2;
                }
            }

            //  🔴 얼음 — 결 위로 **빛이 한 줄기 흘러간다.** 가만있으면 그냥 다른 바닥으로 읽힌다.
            //     칸마다 어긋나게 흘려서 판 전체가 한꺼번에 번쩍이지 않게 한다.
            for (int i = 0; i < _iceShines.Count; i++)
            {
                var (ic, isr) = _iceShines[i];
                if (isr == null) continue;
                float u = Mathf.Repeat(Time.time * 0.26f + (ic * 0.37f), 1f);
                //  🔴 윗면(+0.42) 바로 아래를 훑는다. 칸 가운데를 지나면 "속"으로 읽힌다.
                isr.transform.position = (Vector3)CellPos(ic)
                    + new Vector3(Mathf.Lerp(-0.62f, 0.62f, u), 0.34f, -0.0095f);
                var sc3 = IceShine;
                //  가장자리에선 안 보이고 가운데를 지날 때만 — 스치는 빛으로 읽힌다.
                //  🔴 0.22 는 너무 진했다 (막대로 보였다). 있는 듯 없는 듯이 남긴다.
                sc3.a = 0.085f * Mathf.Sin(u * Mathf.PI);
                isr.color = sc3;
            }

            //  🔴 금 간 바닥 — 세 단계로 말한다.
            //     ① 평소     : 가는 금
            //     ② 밟는 중  : **금이 굵어진다** — "곧 무너진다"를 미리 알려준다 (안 알려주면 함정이다)
            //     ③ 부서짐   : 아래로 기울어 떨어지며 사라진다
            for (int i = 0; i < _cracks.Count; i++)
            {
                var cr = _cracks[i];
                if (cr.slab == null) continue;
                bool gone = _L.CrackIdx.TryGetValue(cr.cell, out int cbi)
                            && (_st.Cm & (1 << cbi)) != 0;
                if (gone) cr.t = Mathf.Min(1f, cr.t + Time.deltaTime / 0.24f);
                else if (cr.t > 0f)
                {
                    //  🔴 되돌리기(Z)·다시하기(R) — 부서진 게 **도로 붙는다.**
                    //     안 되돌리면 화면에서만 사라진 채로 남아 판이 깨진 것처럼 보인다.
                    cr.t = 0f;
                    cr.slab.enabled = true; cr.top.enabled = true;
                    cr.l1.enabled = true; cr.l2.enabled = true;
                    cr.slab.transform.localRotation = Quaternion.identity;
                    cr.slab.transform.localScale = new Vector3(0.94f, 0.94f, 1f);
                }
                _cracks[i] = cr;

                bool load = false;
                for (int k = 0; k < _st.Body.Count; k++)
                    if (_st.Body[k] + _L.W == cr.cell) { load = true; break; }

                float t = cr.t;
                if (t <= 0f)
                {
                    //  🔴 **색·기울기·크기를 늘 제자리로 놓는다** (09-05 사장님: "R/Z 로 리셋하면
                    //     외형만 사라진 걸로 됨"). 되살릴 때 enabled 만 켜고 알파를 안 되돌려서
                    //     켜져 있는데 투명한 채로 남았다. 여기서 매 판 다시 칠하면
                    //     어떤 길로 되돌아오든 복구된다.
                    cr.slab.color = CrackCol;
                    cr.top.color  = CrackTop;
                    cr.slab.transform.localRotation = Quaternion.identity;
                    cr.slab.transform.localScale = new Vector3(0.94f, 0.94f, 1f);
                    //  밟고 있으면 금이 굵고 진해진다
                    float wgt = load ? 1.9f : 1f;
                    cr.l1.transform.localScale = new Vector3(0.050f * wgt, 0.56f, 1f);
                    cr.l2.transform.localScale = new Vector3(0.040f * wgt, 0.34f, 1f);
                    var lc1 = CrackLine; lc1.a = load ? 1f : 0.75f;
                    cr.l1.color = lc1; cr.l2.color = lc1;
                    //  밟으면 살짝 내려앉는다 — 무게가 실린 것으로 읽힌다
                    float sag = load ? -0.045f : 0f;
                    cr.slab.transform.position = (Vector3)CellPos(cr.cell) + new Vector3(0, sag, -0.008f);
                    cr.top.transform.position = (Vector3)CellPos(cr.cell) + new Vector3(0, 0.40f + sag, -0.009f);
                    //  🔴 **금의 자리도 되돌린다.** 안 하면 부서지며 내려간 자리에 금 두 줄만
                    //     바닥 밖에 남는다 (09-05 화면에서 실제로 그랬다) —
                    //     슬래브만 제자리로 보내고 금은 놔뒀기 때문이다.
                    cr.l1.transform.position = (Vector3)CellPos(cr.cell) + new Vector3(-0.14f, 0.08f + sag, -0.010f);
                    cr.l2.transform.position = (Vector3)CellPos(cr.cell) + new Vector3(0.13f, -0.16f + sag, -0.010f);
                    continue;
                }
                //  🔴 t*t — 처음엔 버티다 **한번에** 무너진다. 등속이면 스르르 사라져 안 읽힌다.
                float e = t * t;
                float dy = -e * 1.1f;
                float al = 1f - t;
                var sl = CrackCol; sl.a = al; cr.slab.color = sl;
                var tp = CrackTop;  tp.a = al; cr.top.color  = tp;
                var ln2 = CrackLine; ln2.a = al * 0.8f; cr.l1.color = ln2; cr.l2.color = ln2;
                cr.slab.transform.position = (Vector3)CellPos(cr.cell) + new Vector3(0, dy, -0.008f);
                cr.slab.transform.localScale = new Vector3(0.94f * (1f - 0.22f * t), 0.94f * (1f - 0.30f * t), 1f);
                cr.slab.transform.localRotation = Quaternion.Euler(0, 0, -18f * e);
                cr.top.transform.position = (Vector3)CellPos(cr.cell) + new Vector3(0, 0.40f + dy, -0.009f);
                cr.l1.transform.position = (Vector3)CellPos(cr.cell) + new Vector3(-0.14f, 0.08f + dy, -0.010f);
                cr.l2.transform.position = (Vector3)CellPos(cr.cell) + new Vector3(0.13f, -0.16f + dy, -0.010f);
                if (t >= 1f)
                {
                    cr.slab.enabled = false; cr.top.enabled = false;
                    cr.l1.enabled = false; cr.l2.enabled = false;
                }
            }

            //  🔴 무중력 점은 **제자리에서 떠 있다.** 위로 오르면 홈의 불빛과 헷갈린다.
            for (int i = 0; i < _zoneDots.Count; i++)
            {
                var (zc, zk, zsr) = _zoneDots[i];
                if (zsr == null) continue;
                float ph = zc * 0.7f + zk * 2.1f;
                float ox = Mathf.Sin(Time.time * 0.9f + ph) * 0.22f;
                float oy = Mathf.Cos(Time.time * 0.7f + ph * 1.3f) * 0.22f;
                zsr.transform.position = (Vector3)CellPos(zc) + new Vector3(ox, oy, -0.02f);
            }

            // 🔴 **문벽마다 따로 센다** (09-05 사장님: "다른 홈 깨도 계속 없어지는 모션 나와").
            //    전에는 값이 하나뿐이라, 아무 홈이나 채우면 그 값이 0 으로 돌아가
            //    **이미 사라진 문벽이 도로 나타났다 다시 사라졌다.**
            //    자기 문이 열릴 때만 자기 값이 오르게 한다. 되돌리면 도로 0 이 된다.
            for (int gi = 0; gi < _gateFadeOf.Length; gi++)
                _gateFadeOf[gi] = (_st.Dm & (1 << gi)) != 0
                    ? Mathf.Min(1f, _gateFadeOf[gi] + Time.deltaTime / 0.45f)
                    : 0f;
            for (int di = 0; di < _coverOf.Length; di++)
            {
                _coverOf[di] = 0f;
                if (di >= _L.Doors.Count) continue;
                int n = 0;
                foreach (int c in _st.Body) if (_L.Doors[di].Set.Contains(c)) n++;
                _coverOf[di] = n / (float)_L.Doors[di].Cells.Count;
            }
            foreach (var kv in _gateViews)
            {
                int gi2 = _gateOf[kv.Key];
                bool open = (_st.Dm & (1 << gi2)) != 0;
                //  🔴 불빛이 날아오는 동안은 음수 — 도착한 **뒤에** 바뀐다
                float fade = Mathf.Clamp01(_gateFadeOf[gi2]);
                //  🔴 **자기 홈의 색**을 입는다 (09-23 사장님: "홈 하면 저기가 열릴 거라는 느낌이 전혀 안 들어").
                //     회색이면 어느 홈에 묶였는지 알 길이 없다.
                Color rail = gi2 < _L.Doors.Count ? RailOf(gi2) : GateEdge;
                //  🔴 몸이 홈에 들어가기 시작하면 묶인 칸이 **숨 쉰다** — "이걸 채우면 저게 바뀐다" 예고
                float beat = !open && _coverOf[gi2] > 0
                    ? 0.5f + 0.5f * Mathf.Sin(Time.time * (4f + 6f * _coverOf[gi2]))
                    : 0f;
                Color ec = Color.Lerp(Color.Lerp(GateEdge, rail, 0.45f), rail, beat);
                float ea = open ? 1f - fade : 1f;
                ec.a = ea;
                kv.Value.color = ec;
                kv.Value.enabled = ea > 0.01f;
                if (_gateInner.TryGetValue(kv.Key, out var gin))
                {
                    Color ic = GateCol; ic.a = ea;
                    float s = Mathf.Lerp(0.78f, 0.2f, open ? fade : 0f);
                    gin.color = ic;
                    gin.enabled = ic.a > 0.01f;
                    gin.transform.localScale = new Vector3(s, s, 1);
                }
            }
            TickLinkSparks();
            TickSwitchViews();
            TickTwinView();
            TickEatFx();
            TickBackdrop();

            TickSuck();

            // 🔴 안 채운 홈만 눈에 띄게 둔다 — 남은 칸이 저절로 세어진다
            var body = new HashSet<int>(_st.Body);

            foreach (var kv in _holes)
            {
                bool covered = body.Contains(kv.Key);
                int door = _doorOf.TryGetValue(kv.Key, out var dn) ? dn : 0;
                // 🔴 이미 연 문은 몸을 두고 온 자리다 — 굳은 색으로 남긴다
                bool spent = (_st.Dm & (1 << door)) != 0;

                // 🔴 칸 하나가 **앉는 순간** 짧게 밝아진다. 판이 클수록 한 칸씩
                //    맞춰 넣게 되는데, 그때마다 "이 칸 됐다"가 손에 잡혀야 한다.
                //    되돌리면 기록도 지운다 — 다시 넣으면 또 반응한다.
                if (covered) { if (_seated.Add(kv.Key)) _seatAt[kv.Key] = Time.time; }
                else if (_seated.Remove(kv.Key)) _seatAt.Remove(kv.Key);

                var fill = FillOf(door);
                if (!spent && _seatAt.TryGetValue(kv.Key, out var sat))
                {
                    float k = (Time.time - sat) / SeatFlash;
                    if (k < 1f) fill = Color.Lerp(EdgeOf(door), fill, k);
                }

                // 🔴 굳는 것도 한 번에 안 한다 — 빨려드는 동안 서서히 돌이 된다
                kv.Value.color = spent
                    ? Color.Lerp(fill, SpentCol, SuckT)
                    : covered ? fill : HoleCol;

                // 윗면은 **덩어리의 맨 위 칸**에만 켠다. 속에까지 그리면 줄무늬가 된다.
                if (_spentTop.TryGetValue(kv.Key, out var topSr))
                {
                    bool above = _doorOf.TryGetValue(kv.Key - _L.W, out var upDoor)
                                 && (_st.Dm & (1 << upDoor)) != 0;
                    topSr.enabled = spent && !above;
                }
                if (_holeEdges.TryGetValue(kv.Key, out var e))
                    e.color = RimColor(door, spent);
            }
            //  홈 속 점선 — 양쪽 칸이 다 몸으로 찼거나 문이 열렸으면 끈다 (남은 경계만 보인다)
            foreach (var sm in _holeSeams)
            {
                bool off = (_st.Dm & (1 << sm.door)) != 0 || (body.Contains(sm.a) && body.Contains(sm.b));
                foreach (var d in sm.dash) if (d != null) d.enabled = !off;
            }
            // 🔴 채워진 홈의 심 표시는 사라진다 — 몸이 빨려드는 동안 같이 흐려진다
            foreach (var cv in _coreViews)
            {
                // 🔴 판이 바뀌면 스프라이트가 먼저 사라진다. 건드리면 예외가 나고
                //    **그 뒤 코드가 통째로 안 돈다** — 그래서 판이 안 깨졌다 (08-31).
                if (cv.ring == null || cv.core == null) continue;
                bool done = (_st.Dm & (1 << cv.door)) != 0;
                float a = done ? 1f - SuckT : 1f;
                cv.ring.enabled = a > 0.02f;
                cv.core.enabled = a > 0.02f;
                if (a > 0.02f)
                {
                    //  머리가 들어와 있으면 구멍 속이 찬다 = 열쇠가 꿂혔다
                    bool inKey = _st.Head >= 0 && _L.Doors[cv.door].Core == _st.Head;
                    var rc = CoreCol; rc.a = a; cv.ring.color = rc;
                    //  그림이 있으면 덧칠하지 않는다. 머리가 꿂혔을 때만 한 단 밝혀준다.
                    var cc = inKey ? CoreCol : HoleCol;
                    cc.a = a; cv.core.color = cc;
                }
            }

            // 🔴 받침대: 몸을 놓으면 칸이 꽉 차고 윗면이 밝아진다 = 여기 설 수 있다
            foreach (var kv in _padViews)
            {
                int i = _L.PadIdx[kv.Key];
                bool put = (_st.Pm & (1 << i)) != 0;
                kv.Value.transform.localScale = new Vector3(0.96f, put ? 0.96f : 0.5f, 1);
                kv.Value.transform.position = CellPos(kv.Key) + new Vector2(0f, put ? 0f : -0.22f);
                kv.Value.color = put ? SpentCol : PadCol;
                if (_padTops.TryGetValue(kv.Key, out var lip))
                {
                    lip.transform.position = CellPos(kv.Key) + new Vector2(0f, put ? 0.44f : 0.02f);
                    lip.color = put ? SpentTop : PadEdge;
                }
            }

            //  테두리 이음매도 테두리와 같은 색으로
            foreach (var xe in _holeEdgeExtra)
                if (xe.sr != null)
                    xe.sr.color = RimColor(xe.door, (_st.Dm & (1 << xe.door)) != 0);

            //  🔴 뒤에 번지는 빛. 문을 열면 꺼진다 — 다 쓴 출구는 안 부른다.
            foreach (var g in _holeGlow)
            {
                if (g.sr == null) continue;
                bool gSpent = (_st.Dm & (1 << g.door)) != 0;
                var gc = RailOf(g.door);
                //  숨은 **진하기 전체**에 곱한다 — 겹끼리 따로 뛰면 층이 드러난다
                //  🔴 테두리와 **같은 박자·같은 위상**으로 (09-16). 홈마다 어긋내고 진폭을 절반으로 —
                //     번지는 빛까지 다 같이 뛰면 판 전체가 한 덩어리로 깜빡인다.
                float pulse = gSpent ? Mathf.Max(0f, 1f - SuckT)
                                     : 0.88f + 0.12f * (0.5f + 0.5f * Mathf.Sin(Time.time * 1.15f + g.door * 1.9f));
                gc.a = g.a * pulse;
                g.sr.color = gc;
            }

            TickMotes();

            //  🔴 **심에서 나온다.** 마름모가 열렸다가, 슬라임이 다 나오면 닫힌다.
            //     끝의 "심으로 빨려듦"을 거꾸로 돌린 것이다 — 저쪽은 삼키고 이쪽은 뱉는다.
            if (_bornRing != null)
            {
                float b = BornT;
                bool on = b < 1f;
                _bornRing.enabled = _bornCore.enabled = on;
                if (on)
                {
                    //  앞쪽 3할은 **벌어지고**, 나머지는 슬라임에 밀려 **닫힌다**
                    float open  = Mathf.Clamp01(b / 0.30f);
                    float close = Mathf.Clamp01((b - 0.30f) / 0.70f);
                    float k = Ease(open) * (1f - Ease(close));

                    _bornRing.transform.localScale = new Vector3(0.40f * k, 0.55f * k, 1);
                    var rc = CoreCol; rc.a = k;
                    _bornRing.color = rc;

                    //  속은 **비어 있다** — 구멍이라야 "여기서 나왔다"가 된다.
                    //  홈의 심과 같은 짜임이다 (밝은 테 + 어두운 속).
                    _bornCore.transform.localScale = new Vector3(0.23f * k, 0.32f * k, 1);
                    var cc = HoleCol; cc.a = k;
                    _bornCore.color = cc;
                }
            }
            //  마름모 뒤에서 한 번 퍼지는 빛
            if (_bornFlash != null)
            {
                float b = BornT;
                bool on = b < 1f;
                _bornFlash.enabled = on;
                if (on)
                {
                    float v = 1f - b;
                    float bs = 0.35f + 1.15f * b;          // 퍼져 나간다
                    _bornFlash.transform.localScale = new Vector3(bs, bs, 1);
                    var bc = Color.Lerp(BodyCol, Color.white, v);
                    bc.a = 0.50f * v * v;                  // 끝으로 갈수록 빨리 옅어진다
                    _bornFlash.color = bc;
                }
            }

            // 다리는 양쪽이 다 덮였을 때만 같이 물든다 — 안 그러면 끊겨 보인다
            foreach (var br in _bridges)
            {
                int door = _doorOf.TryGetValue(br.a, out var dn) ? dn : 0;
                bool spent = (_st.Dm & (1 << door)) != 0;
                br.sr.color = spent ? SpentCol
                    : (body.Contains(br.a) && body.Contains(br.b)) ? FillOf(door) : HoleCol;
            }
        }
    }
}
