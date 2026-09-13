using System.Collections.Generic;
using UnityEngine;

namespace SlimeEscape
{
    //  🔴 SnakeController 의 한 조각 (partial). 필드 · Awake · 판 진행은 SnakeController.cs.
    //     움직임 — Animate (연출은 칸을 안 건드린다)
    //     2026-09-12 에 4,400줄짜리 한 파일을 구획대로 나눴다 — 내용은 한 글자도 안 바꿨다.
    public partial class SnakeController
    {
        // ---------------- 움직임 ----------------
        void Animate(float dt)
        {
            // 🔴 **여기서 매 프레임 부른다.** SyncViews 는 걸음을 옮길 때만 불렸는데,
            //    문 열리기·별 돌기·빨려들기를 죄다 그 안에 넣어놔서 한 번 그리고 멈춰 있었다.
            //    (08-30: "아무 일도 안 일어나고" · 그 전엔 "이게 다 열린겨?")
            SyncViews();

            // 다 끝났으면 더 안 키운다 (가만히 두면 값이 무한정 커진다)
            // 판이 바뀔 때 맞춰둔 자리로 부드럽게 붙는다. 판 도중에는 안 움직인다 (rev.10)
            if (_st != null)
            {
                float k = 1f - Mathf.Exp(-CamEase * dt);
                //  ⛔ 도트 격자에 떨어뜨리던 것(SnapPos)은 뺐다 (09-12) — 도트 그림이 없다
                _cam.transform.position = Vector3.Lerp(_cam.transform.position, _camWant, k);
                _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _camSizeWant, k);
            }

            float fallSpan = FallSpan;
            //  🔴 미끄러진 만큼 걸음 구간이 길어진다. 이 값이 0 이면 옛날과 똑같다.
            float slideSpan = SlideSpan;
            float stepEnd = 1f + slideSpan;             // 걸음 구간의 끝
            float done = stepEnd + fallSpan + Lag * Mathf.Max(0, _st.Length - 1);
            if (_stepT < done + 1f) _stepT += dt / Mathf.Max(0.02f, K.stepTime);
            AnimateBoxes();
            _pop = Mathf.Max(0f, _pop - dt / 0.18f);
            _land = Mathf.Max(0f, _land - dt / 0.20f);
            if (_jigT < 9f) _jigT += dt;

            // 머리가 땅에 닿는 순간 한 번만 친다 (마디마다 치면 계속 떨린다)
            if (!_landed && _stepT >= stepEnd + fallSpan)
            {
                _landed = true;
                _land = Mathf.Clamp01(K.landSquash) * Mathf.Clamp01(_fall / 4f);
                //  🔴 닿는 순간 납작해졌다가 흔들리며 선다. 높이 떨어질수록 세다.
                float k = JigLand * Mathf.Clamp01(_fall / 3f);
                Jig(k, -k);
            }

            while (_segPos.Count < _st.Length) _segPos.Add(_segPos[_segPos.Count - 1]);
            while (_segPos.Count > _st.Length) _segPos.RemoveAt(_segPos.Count - 1);
            while (_from.Count < _segPos.Count) _from.Add(_segPos[_from.Count]);
            while (_from.Count > _segPos.Count) _from.RemoveAt(_from.Count - 1);
            while (_mid.Count < _segPos.Count) _mid.Add(CellPos(_st.Body[_mid.Count]));
            while (_mid.Count > _segPos.Count) _mid.RemoveAt(_mid.Count - 1);

            // 🔴 마디 i는 '출발선 -> 자기 칸'으로만 간다. 앞 마디 쪽으로 끌어당기지 않는다.
            //    끌리는 느낌은 **출발이 늦는 것**으로 낸다 — 그래야 끝났을 때 정확히 칸에 앉는다.
            float lag = Lag;
            for (int i = 0; i < _st.Length; i++)
            {
                float t = _stepT - i * lag;
                Vector2 end = CellPos(_st.Body[i]);

                //  🔴 **포탈로 건너뛴 마디는 미끄러지지 않는다** (rev.11).
                //     걸음 구간의 도착이 한 칸 넘게 떨어져 있으면 그건 걸은 게 아니라
                //     **건너뛴 것**이다. 이어 그리면 마디가 판을 가로질러 날아간다 —
                //     사장님이 "포털 들어가면 몸이 늘어난다" 고 하신 게 이것이다.
                //     ⚠️ **걸음 구간만** 본다. 낙하는 여러 칸이 정상이라 여기서 잡으면 안 된다.
                //  🔴 미끄러짐은 **등속**이다. Ease(감속)를 쓰면 두 칸을 슬쩍 옮긴 것처럼 보인다 —
                //     사장님이 물리신 그 느낌이다. 쭉 흐르다 부딪혀 선다.
                bool sliding = slideSpan > 0f;
                Vector2 stepTo = fallSpan <= 0f ? end : _mid[i];
                //  🔴 **얼음 미끄러짐은 건너뛴 게 아니다.** 같은 줄을 쭉 밀려간 것이라 이어 그린다.
                //     ⚠️ 전에는 "포탈이 있으면 포기"였다 (같은 줄로도 건너뛸 수 있어서).
                //        이제 **몇 칸 미끄러졌는지(_slid)를 알고 있다** — 포탈을 타면 미끄러짐이
                //        거기서 끊기므로(_sl=false) _slid 가 1 을 넘는 걸음은 **반드시 미끄러진 것**이다.
                //        그래서 포탈이 있는 판에서도 제대로 흐른다.
                bool slid2 = _slid > 1 && Mathf.Abs(stepTo.y - _from[i].y) < 0.01f;
                bool warp = !slid2 && (stepTo - _from[i]).sqrMagnitude > 2.25f;

                float su = Mathf.Clamp01(t / stepEnd);       // 걸음 구간 안에서의 진행
                float sc = sliding ? su : Ease(su);          // 미끄러지면 등속, 아니면 감속
                if (fallSpan <= 0f)
                {
                    _segPos[i] = warp ? (su < 0.5f ? _from[i] : end)
                                      : Vector2.Lerp(_from[i], end, sc);
                }
                else if (t <= stepEnd)
                {
                    // 1구간: 걸음(미끄러짐 포함). 끝을 부드럽게 놓는다
                    _segPos[i] = warp ? (su < 0.5f ? _from[i] : _mid[i])
                                      : Vector2.Lerp(_from[i], _mid[i], sc);
                }
                else
                {
                    // 2구간: 낙하. 🔴 점점 빨라져야 한다 — 걸음의 곡선(감속)을 그대로 쓰면 안 된다
                    float u = Mathf.Clamp01((t - stepEnd) / fallSpan);
                    _segPos[i] = Vector2.Lerp(_mid[i], end, u * u);
                }
            }

            for (int i = 0; i < _st.Length; i++)
            {
                float s = K.segmentSize;
                if (i == _st.Length - 1) s *= 1f + _pop;               // 새로 붙은 꼬리가 한 번 부푼다

                // 🔴 모양 실험(치마·그림자·돔)은 걷어냈다 — 비석·유령처럼 보였다.
                //    사장님이 나중에 직접 그리실 자리라, 여기서는 **둥근 몸 + 눈 + 이음매**까지만 둔다.
                //  🔴 태어나는 동안은 **심에서 비어져 나온다** (09-03 사장님).
                //     마름모를 닮아 **좁고 길게** 시작해서 옆으로 부푼다 —
                //     바닥에서 올라오게 했더니 끝의 "심으로 빨려듦"과 짝이 안 맞았다.
                //     마디마다 조금씩 늦게 나와야 한 줄로 밀려 나오는 것처럼 보인다.
                float bt = Mathf.Clamp01((BornT - i * 0.10f) / 0.90f);
                float bw = 1f, bh = 1f;
                if (bt < 1f)
                {
                    float e = Ease(bt);                        // 끝에서 부드럽게 선다
                    //  마름모 비율(0.40 : 0.55)에서 출발한다
                    bw = Mathf.Lerp(0.30f, 1f, e);
                    bh = Mathf.Lerp(0.42f, 1f, e);
                    //  비어져 나오는 순간 한 번 불룩 — 없으면 그냥 커지는 네모다
                    float pop = Mathf.Sin(bt * Mathf.PI);
                    bw *= 1f + 0.20f * pop;
                    bh *= 1f - 0.10f * pop;
                }
                //  🔴 출렁임. 넓어지면 낮아진다 — 부피가 도는 것처럼 보여야 젤리가 된다.
                //     걸음 직후(_stepT 가 작을 때)에 크게 흔들렸다 잦아든다.
                //  🔴 마디마다 **다른 씨앗**을 준다. 위상차만으로는 줄지어 흐르는
                //     한 파도로 보인다 — 끓는 것은 마디마다 제멋대로여야 한다.
                float salt = Hash(i * 7 + 13) * 6.2832f;
                float ph   = Time.time * WobbleHz - i * WobbleLag + salt;
                //  🔴 두 박자를 겹친다. 사인 하나면 기계가 떠는 것으로 보인다.
                float wave = 0.68f * Mathf.Sin(ph) + 0.32f * Mathf.Sin(ph * 1.73f + 1.1f);
                //  천천히 차올랐다 빠지는 **압력**. 늘 같은 세기로 떨면 살아 있는 게 아니다.
                float press = 0.65f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.time * 0.62f + salt));
                float amp  = (WobbleIdle + WobbleKick * Mathf.Exp(-_stepT * 1.6f)) * press;

                //  밖으로는 조금, 안으로는 크게 — 벽을 뚫고 나가면 안 된다
                float wv = wave >= 0f ? wave * amp * BulgeOut : wave * amp * BulgeIn;
                float hv = 0f;
                {
                    float w2 = 0.68f * Mathf.Sin(ph - WobbleDrag)
                             + 0.32f * Mathf.Sin((ph - WobbleDrag) * 1.73f + 1.1f);
                    hv = w2 >= 0f ? w2 * amp * BulgeOut : w2 * amp * BulgeIn;
                }
                //  🔴 부푸는 몫 — 가로세로가 **같이** 는다. 이게 압력으로 읽힌다.
                float swell = SwellAmt * (0.5f + 0.5f * Mathf.Sin(Time.time * SwellHz
                                                                  - i * SwellLag + salt));
                //  세로는 가로보다 늦게 따라온다 — 무게가 뒤늦게 내려앉는 느낌
                bw *= 1f + WobbleSag + wv + swell;
                bh *= 1f - WobbleSag - hv + swell;
                //  🔴 아무리 커도 여기까지. 벽을 뚫고 나간 것처럼 보이면 안 된다.
                bw = Mathf.Min(bw, MaxBulge);
                bh = Mathf.Min(bh, MaxBulge);

                //  🔴 떨어지는 동안은 세로로 늘어난다. 이게 없으면 착지의 납작함이
                //     어디서 왔는지 안 보여서 그냥 한 번 찌그러진 것으로 보인다.
                if (!_landed && _fall > 0)
                {
                    float fs = JigFall * Mathf.Clamp01(_fall / 2.5f);
                    bw *= 1f - fs;
                    bh *= 1f + fs;
                }

                //  🔴 방향 출렁임. 마디마다 조금씩 늦게 와야 몸을 타고 흐른다.
                float jt = _jigT - i * 0.045f;
                if (jt > 0f && jt < 2f)
                {
                    float w = Mathf.Exp(-jt * JigDecay) * Mathf.Cos(jt * JigHz * 6.2832f);
                    bw *= 1f + _jigX * w;
                    bh *= 1f + _jigY * w;
                }

                float hNow = s * bh * (1f - _land * 0.35f);
                //  🔴 **밑면을 붙박이로.** 가운데를 기준으로 늘었다 줄었다 하면
                //     공중에 뜬 것처럼 보인다. 눌릴 때 아래로 주저앉아야 무게가 생긴다.
                float dy = (hNow - s) * 0.5f;
                while (_segDy.Count <= i) { _segDy.Add(0f); _segW.Add(0f); _segH.Add(0f); }
                _segDy[i] = dy;
                _segW[i] = s * bw;
                _segH[i] = hNow;

                //  🔴 **밑이 촥 퍼진다.** 조각을 깔지 않고 모양 자체를 갈아 낀다 —
                //     눌린 만큼(그리고 착지한 만큼) 아래가 넓은 단을 고른다.
                //     ⚠️ 09-02 에 치마·돔을 붙였다가 비석처럼 보여 걷어낸 적이 있다.
                //        쉴 때는 거의 대칭이라야 그 꼴이 안 난다.
                float splay = SplayIdle + SplayBy * (1f - bh) + SplayKick * _land;
                int lvl = Mathf.RoundToInt(Mathf.Clamp01(splay / SplayMax)
                                           * (PixelSprites.DropLevels - 1));
                var want = PixelSprites.Drop(lvl);
                if (_segs[i].sprite != want) _segs[i].sprite = want;

                _segs[i].transform.position = _segPos[i] + new Vector2(0f, dy);
                // 착지 눌림은 세로로만 — 넓히면 한 칸을 넘는다
                _segs[i].transform.localScale = new Vector3(s * bw, hNow, 1);
                // 🔴 떨어지며 먹으면 다음 걸음에 길어진다 — 그 사이에 아무 표시가 없으면
                //    "먹었는데 사라졌다"로 보인다. 사람이 실제로 그렇게 헤맸다 (2026-08-29).
                // 🔴 몸은 **끝까지 한 색**이다. 머리도 꼬리도 같다 (09-02 사장님).
                //    한 마디만 다른 색이면 이어진 관이 아니라 딴 물건이 붙은 것으로 보인다.
                //    ⚠️ 삼킨 조각을 여기서 꼬리에 통째로 칠한 적이 있다 (09-03 사장님 화면:
                //       "이렇게 주황색이 되는 때가 있지"). 그건 **꼬리 속 덩어리**로 옮겼다.
                _segs[i].color = BodyCol;
            }

            // 🔴 마디 사이를 **잇는다.** 안 이으면 몸이 길어질 때 따로 떨어진 네모 여럿으로
            //    보인다 — 한 마리가 아니라 세 마리로 보였다 (08-31 사장님).
            //    이음매는 마디보다 얇게 해서 슬라임처럼 잘록하게 만든다.
            while (_links.Count < Mathf.Max(0, _st.Length - 1))
            {
                // 🔴 이음매를 몸(3·4)보다 **위**에 그린다.
                //    마디마다 옆면에 어두운 윤곽이 있어서 붙는 자리에 두 겹으로 곹쳐
                //    세로줄이 생겼다 — 이어진 관이 아니라 블록 여럿으로 보였다 (09-02).
                //    이음매가 그 자리를 가리면 윤곽은 관의 **바깥선에만** 남는다.
                var lk = NewSprite("Link", 5);
                lk.sprite = PixelSprites.Round();
                _links.Add(lk);
            }
            for (int i = 0; i < _links.Count; i++)
            {
                bool on = i + 1 < _st.Length && i + 1 < _segPos.Count && BornT >= 1f;
                //  🔴 **포탈을 지나는 동안 몸은 두 덩이로 갈라진다** (rev.11).
                //     그때 이음매를 그으면 지도를 가로지르는 막대가 된다 —
                //     사장님이 "포털 들어가면 몸이 늘어난다" 고 하신 게 이것이다.
                //     붙어 있는 마디 사이에만 긋는다. 한 칸 넘게 떨어지면 안 긋는다.
                if (on && i < _st.Body.Count && i + 1 < _st.Body.Count)
                {
                    int ca0 = _st.Body[i], cb0 = _st.Body[i + 1];
                    int dx0 = Mathf.Abs(_L.X(ca0) - _L.X(cb0));
                    int dy0 = Mathf.Abs(_L.Y(ca0) - _L.Y(cb0));
                    if (dx0 + dy0 > 1) on = false;
                }
                _links[i].enabled = on;
                if (!on) continue;
                //  🔴 이음매는 몸통과 **같은 두께**여야 이어진 한 덩어리로 보인다.
                //     얇게 하면 구슬 목걸이가 된다 (08-31 사장님 그림: 굵기가 일정한 관).
                //  🔴 두께를 K.segmentSize 에서 가져오면 **출렁임을 못 따라간다** —
                //     몸은 눌렸는데 관만 원래 굵기로 남아 이음매가 튀어나온다 (09-03 사장님).
                //     마디가 지금 실제로 몇인지(_segW/_segH)를 보고 그 사이를 잇는다.
                int j = i + 1;
                bool have = i < _segW.Count && j < _segW.Count;
                float wA = have ? _segW[i] : K.segmentSize, wB = have ? _segW[j] : K.segmentSize;
                float hA = have ? _segH[i] : K.segmentSize, hB = have ? _segH[j] : K.segmentSize;
                //  마디가 내려앉은 만큼 가운데도 내려간다. 안 맞으면 관이 층진다.
                float yA = _segPos[i].y + (i < _segDy.Count ? _segDy[i] : 0f);
                float yB = _segPos[j].y + (j < _segDy.Count ? _segDy[j] : 0f);
                Vector2 ca = new Vector2(_segPos[i].x, yA);
                Vector2 cb = new Vector2(_segPos[j].x, yB);
                Vector2 d2 = cb - ca;
                //  🔴 **그려지는 자리**로도 한 번 더 본다 (09-05 사장님: "움직일 때 살짝살짝 보여").
                //     최종 칸으로는 이웃인데 **지금 그려지는 자리는 판 반대편**인 순간이 있다 —
                //     머리는 벌써 출구 옆으로 갔는데 다음 마디는 아직 입구 자리에 남아 있을 때다.
                //     그때 이으면 막대가 한 번 번쩍인다.
                //     ⚠️ 걸을 때도 마디 사이가 **두 칸**까지 벌어진다 (뒤 마디가 늦게 뜬다).
                //        그래서 세 칸(9)을 넘을 때만 끊는다 — 걸음은 안 건드린다.
                //  🔴 미끄러지는 동안엔 마디 사이가 여러 칸 벌어진다 (뒤 마디가 늦게 뜬다).
                //     같은 줄이면 늘어난 것이지 끊어진 게 아니다 — **미끄러진 칸 수만큼** 봐준다.
                float span = _slid + 1.5f;
                float cut = (_slid > 1 && Mathf.Abs(d2.y) < 0.2f) ? span * span : 9f;
                if (d2.sqrMagnitude > cut) { _links[i].enabled = false; continue; }
                bool horiz = Mathf.Abs(d2.x) > Mathf.Abs(d2.y);

                //  🔴 **얇은 쪽에 맞춘다.** 굵은 쪽에 맞추면 가는 마디 옆으로
                //     관이 삐져나와 혹처럼 보인다. 얇은 쪽은 안으로 들어가 안 보인다.
                float thick = horiz ? Mathf.Min(hA, hB) : Mathf.Min(wA, wB);
                float len   = horiz ? Mathf.Abs(d2.x) : Mathf.Abs(d2.y);

                //  🔴 가로 이음매는 **밑면을 맞춘다.** 몸이 전부 밑면 기준으로 움직이는데
                //     이음매만 가운데 기준이면 마디가 눌릴 때 관이 위로 뜬다.
                Vector2 lp = (ca + cb) * 0.5f;
                if (horiz)
                {
                    float bot = Mathf.Max(yA - hA * 0.5f, yB - hB * 0.5f);
                    lp.y = bot + thick * 0.5f;
                }
                _links[i].transform.position = lp;
                _links[i].transform.localScale = horiz
                    ? new Vector3(len + 0.02f, thick, 1)
                    : new Vector3(thick, len + 0.02f, 1);
                _links[i].color = BodyCol;
            }

            TickPuffs();     // 🔴 마디 자리를 다 잡은 뒤라야 거기서 김이 난다
            TickBubbles();

            //  🔴 삼킨 조각이 꼬리 속에서 숨쉰다. 다음 걸음에 이게 마디가 된다.
            if (_swallow != null)
            {
                int lastI = _st.Length - 1;
                bool sw = _st.Pg > 0 && lastI >= 0
                          && lastI < _segPos.Count && lastI < _segW.Count && BornT >= 1f;
                _swallow.enabled = sw;
                if (sw)
                {
                    float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * 5.5f);
                    float cy = _segPos[lastI].y + (lastI < _segDy.Count ? _segDy[lastI] : 0f);
                    _swallow.transform.position = new Vector3(_segPos[lastI].x, cy, 0f);
                    float k = 0.46f + 0.09f * beat;
                    _swallow.transform.localScale =
                        new Vector3(_segW[lastI] * k, _segH[lastI] * k, 1);
                    var sc2 = FoodCol; sc2.a = 0.70f + 0.16f * beat;
                    _swallow.color = sc2;
                }
            }
        }

        static float Ease(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
    }
}
