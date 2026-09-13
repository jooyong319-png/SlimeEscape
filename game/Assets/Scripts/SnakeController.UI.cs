using System.Collections.Generic;
using UnityEngine;

namespace SlimeEscape
{
    //  🔴 SnakeController 의 한 조각 (partial). 필드 · Awake · 판 진행은 SnakeController.cs.
    //     임시 UI (OnGUI) — 판 고르기 · 결과 · 타이틀 · 설정 · 얼굴
    //     2026-09-12 에 4,400줄짜리 한 파일을 구획대로 나눴다 — 내용은 한 글자도 안 바꿨다.
    public partial class SnakeController
    {
        // ---------------- 임시 UI ----------------
        void LoadKnobs()
        {
            var json = PlayerPrefs.GetString(KnobKey, "");
            // 🔴 FromJson은 저장된 JSON에 없는 필드를 기본값이 아니라 0으로 채운다.
            //    손잡이를 새로 추가하면 예전 설정 때문에 그 값이 0이 되어 버린다.
            //    빈 Knobs 위에 덮어써야 없는 필드가 기본값으로 남는다.
            K = new Knobs();
            if (!string.IsNullOrEmpty(json)) { try { JsonUtility.FromJsonOverwrite(json, K); } catch { K = new Knobs(); } }
        }

        /// 🔴 WebGL 빌드에서는 유니티 기본 글꼴이 **운영체제 글꼴에 기대기 때문에**
        ///    브라우저에 한글이 없어 전부 네모로 깨진다. 에디터에선 절대 재현 안 된다.
        ///    그래서 한글이 든 폰트를 프로젝트 안에 넣고 GUIStyle마다 직접 지정한다.
        ///    ⚠ Resources/Fonts/kr.ttf 는 지금 재배포 불가 폰트다 — 그 폴더의 README 참고.
        Font _krFont;

        /// 손가락용 방향 패드 + 되돌리기/다시. 🔴 판 하나에 100걸음을 넘기므로
        /// 버튼이 커야 한다 — 작은 버튼은 그 자체가 난이도가 된다.
        void Pad(float w, float h)
        {
            float b = Mathf.Clamp(Mathf.Min(w, h) * 0.12f, 46f, 108f);   // 버튼 한 변
            float m = b * 0.28f;                                          // 가장자리 여백
            // 왼쪽 아래 십자 — 판의 단추와 **같은 말투**로 (형광 테 + 어두운 속)
            float px = m, py = h - m - b * 3f;
            if (Btn(new Rect(px + b, py, b, b), "↑")) Step(SnakeEngine.Dir.Up);
            if (Btn(new Rect(px, py + b, b, b), "←")) Step(SnakeEngine.Dir.Left);
            if (Btn(new Rect(px + b * 2f, py + b, b, b), "→")) Step(SnakeEngine.Dir.Right);
            if (Btn(new Rect(px + b, py + b * 2f, b, b), "↓")) Step(SnakeEngine.Dir.Down);

            // 오른쪽 아래 — 되돌리기 · 다시
            float qx = w - m - b * 1.6f;
            //  🔴 받침대 판에서만 — 몸 놓기(키보드는 Space). 이게 없으면 받침대 판을 만드는 순간
            //     **폰에서 진행이 막힌다** (todo 모바일 손질, 09-12). 되돌리기 위 빈 줄에 둔다.
            if (_L.Pads.Count > 0 && Btn(new Rect(qx, py + b * 0.2f, b * 1.6f, b * 0.7f), "몸 놓기"))
                Step(SnakeEngine.Dir.Drop);
            if (Btn(new Rect(qx, py + b, b * 1.6f, b), "되돌리기")) Undo();
            if (Btn(new Rect(qx, py + b * 2f + m * 0.4f, b * 1.6f, b * 0.8f), "다시"))
            {
                if (_run != null) _run.restart++;
                Restart();
            }
        }

        /// <summary>
        /// 🔴 게임을 아예 처음으로. 깬 판·모은 획·안내 화면 본 기록을 다 지운다.
        /// 손맛 값(K)은 **안 지운다** — 그건 게임 진행이 아니라 사장님이 맞춰둔 설정이다.
        /// </summary>
        void ResetAll()
        {
            PlayerPrefs.DeleteKey(ProgressKey);
            PlayerPrefs.DeleteKey(IntroKey);
            PlayerPrefs.Save();
            _recs.Clear();
            _trail.Clear();
            _allDone = false;
            _askReset = false;
            _steps = 0;
            Load(0);
            //  🔴 화면은 **부르는 쪽이 정한다.** 전에는 여기서 안내 화면을 켰는데,
            //     새로 하기는 이제 튜토리얼 판으로 바로 간다 (09-04 사장님).
            _menu = false;
            _title = false;
            _settings = false;
            _intro = false;
        }

        /// <summary>
        /// 🔴 깬 직후. 별이 하나씩 **크게 나타났다 줄어들며** 박힌다 — 땅! 땅! 땅!
        /// 못 받은 별도 같은 박자에 (빈 별로) 나와야 "하나 놓쳤다"가 보인다.
        /// </summary>
        void Result(float w, float h)
        {
            float el = Time.time - _resultAt;

            //  🔴 판 위에 그냥 얹혀 있어서 "12걸음 · 커트 15" 가 홈·슬라임 위에 겹쳤다 (09-13 화면).
            //     판은 **보이게 두되 물러나게** — 반쯤 덮는다. 방금 채운 판이 뒤에 남아 있는 게 보상이다.
            var dim = BgCol; dim.a = Mathf.Clamp01(el / 0.25f) * 0.78f;
            Tex(new Rect(0, 0, w, h), dim);

            var big = new GUIStyle(_sBig) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, h * 0.5f - 128f * _uiScale, w, 36f * _uiScale), "CLEAR", big);

            // ---- 별 셋 ----
            float sz = Mathf.Clamp(64f * _uiScale, 48f, 96f), gap = sz * 0.34f;
            float total = 3 * sz + 2 * gap;
            float sy = h * 0.5f - sz * 0.5f - 18f * _uiScale;

            for (int k = 0; k < 3; k++)
            {
                float at = StarLead + k * StarBeat;
                if (el < at) continue;
                float u = Mathf.Clamp01((el - at) / StarPop);
                float pop = 1f + (1f - u) * (1f - u) * 1.7f;      // 크게 나왔다가 제자리로
                var r = new Rect(w * 0.5f - total * 0.5f + k * (sz + gap), sy, sz, sz);

                bool got = k < _resultStars;
                var col = got ? StarLit : new Color(1f, 1f, 1f, 0.20f);
                if (got) col = Color.Lerp(Color.white, StarLit, u);   // 박히는 순간 하얗게 번쩍

                var m = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), r.center);
                //  🔴 글꼴의 ★☆ 가 아니라 **구운 별 도형**을 그린다 (09-12 — 도트 글꼴이라 계단이 커졌다)
                Shape(r, col, (got ? PixelSprites.Star() : PixelSprites.StarRing()).texture);
                GUI.matrix = m;
            }

            // ---- 걸음 수 ----
            if (el > StarLead + 2 * StarBeat)
            {
                string line = _resultSteps + "걸음";
                if (Def.cut > 0) line += "   커트 " + Def.cut;
                if (!_resultStar) line += "   ·  별을 놓쳤다";
                GUI.Label(new Rect(0, sy + sz + 14f * _uiScale, w, 24f * _uiScale), line, _sMid);

                if (_resultBest)
                    GUI.Label(new Rect(0, sy + sz + 40f * _uiScale, w, 22f * _uiScale),
                              "새 기록", _sSmall);
            }

            //  🔴 **다시 하기**를 여기 둔다. 별 셋을 놓친 사람은 지금 다시 하고 싶다 —
            //     전에는 다음 판으로 넘어간 뒤 목록에서 되돌아와야 했다.
            if (el > StarLead + 3 * StarBeat)
            {
                float bw2 = Mathf.Clamp(w * 0.20f, 130f, 240f);
                float bh2 = Mathf.Clamp(h * 0.070f, 36f, 54f);
                float gap2 = bw2 * 0.12f;
                float by2 = h - bh2 - h * 0.10f;
                float bx2 = w * 0.5f - bw2 - gap2 * 0.5f;

                if (Btn(new Rect(bx2, by2, bw2, bh2), "다시 하기"))
                { _result = false; Restart(); return; }
                if (Btn(new Rect(bx2 + bw2 + gap2, by2, bw2, bh2), "다음 판"))
                { AdvanceNow(); return; }

                GUI.Label(new Rect(0, by2 + bh2 + 6f * _uiScale, w, 22f * _uiScale),
                          "판 고르기로 가려면 뒤로", _sSmall);
            }
        }

        Texture2D _solid;
        Color _solidCol;
        /// 화면을 덮을 단색 한 장. 파일을 두지 않고 만들어 쓴다.
        /// OnGUI 는 한 프레임에 두 번 도니 색이 바뀔 때만 새로 굽는다.
        Texture2D Solid(Color c)
        {
            if (_solid == null)
            {
                _solid = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _solidCol = new Color(-1, -1, -1, -1);
            }
            if (_solidCol != c) { _solid.SetPixel(0, 0, c); _solid.Apply(); _solidCol = c; }
            return _solid;
        }

        /// 판 고르기·안내판의 네모 한 장. Solid() 가 인스턴스 것이라 static 못 쓴다.
        void Tex(Rect r, Color col) => GUI.DrawTexture(r, Solid(col));


        /// 마디에 찍을 이름. 판 이름 "1-6" 에서 뒤쪽만 뗀다 — 자리 번호가 아니다.
        /// 가르치는 판은 수가 아니라 표로 둔다. 세는 판이 아니니까.
        static string NodeLabel(SnakeLevelJson d, int i)
        {
            if (d == null) return (i + 1).ToString();
            //  ⚠️ 가르치는 판을 `※` 로 둔 적이 있는데, 글꼴에서 흰 동그라미로 나왔다.
            //     판 이름이 1-0 이니 그냥 "0" 으로 둔다 — 나머지와 규칙도 같아지고
            //     글꼴에 있는 글자만 쓰니 **글꼴에 기대는 것이 하나 줄어든다** (09-02).
            if (string.IsNullOrEmpty(d.id)) return (i + 1).ToString();
            int dash = d.id.LastIndexOf('-');
            return dash >= 0 && dash + 1 < d.id.Length ? d.id.Substring(dash + 1) : d.id;
        }

        /// <summary>
        /// 🔴 판 고르기. 별이 있을 자리이고, "내가 어디까지 왔나"가 보이는 유일한 화면이다.
        /// 진행은 여전히 한 줄 — 앞 판을 깨야 다음 판이 열린다. 깬 판은 다시 들어갈 수 있다.
        /// </summary>
        void Menu(float w, float h)
        {
            // 🔴 격자 버튼이 아니라 **길로 이어진 지도**다. 판이 늘어선 게 아니라
            //    한 굴을 따라 내려가는 것처럼 보여야 "다음 하나만 더"가 생긴다.
            //  🔴 **굴 하나씩** 보여준다 (09-12). 40판을 한 지도에 뿌리면 마디가 잘아지고
            //     어디서 굴이 바뀌는지가 안 보였다. 아래 i 는 이 굴 안의 자리, idx[i] 가 판 번호다.
            if (_menuCh <= 0) _menuCh = ChapterOf(Def);
            //  지도도 **보고 있는 굴의 색**으로 칠한다 — 넘길 때 굴이 바뀐 게 보인다.
            var cave = CaveOf(_menuCh);
            GUI.DrawTexture(new Rect(0, 0, w, h), Solid(cave.bg));
            var idx = new List<int>();
            for (int i = 0; i < _set.levels.Length; i++)
                if (ChapterOf(_set.levels[i]) == _menuCh) idx.Add(i);
            int n = idx.Count;
            if (n == 0) { _menuCh = ChapterOf(Def); return; }
            float pad = 46f * _uiScale;
            //  🔴 아래 여백을 넉넉히 — 52 였을 때 안내 글이 「설명」 단추와 **겹쳤다** (09-12 세로 폰 화면).
            //     폰에서는 글자가 커져서 두 줄이 한 자리에 앉는다. 글은 단추 **위**에 온다.
            float top = 78f * _uiScale, bot = 84f * _uiScale;
            int maxCols = Mathf.Clamp(Mathf.FloorToInt((w - pad * 2f) / (86f * _uiScale)), 3, 6);
            int rows = Mathf.CeilToInt(n / (float)maxCols);
            //  🔴 줄을 **고르게** 나눈다 — 7판을 6칸에 넣으니 윗줄 6 · 아랫줄 1 이 되어
            //     마디 하나가 오른쪽 아래에 외톨이로 떨어지고 화면 아래 절반이 비었다 (09-13 화면).
            int cols = Mathf.CeilToInt(n / (float)rows);

            float avail = h - top - bot;
            float gx = (w - pad * 2f) / cols;
            //  줄 사이도 **칸 폭만큼만** 벌린다. 높이를 다 나눠 쓰면 두 줄이 화면 위아래 끝으로 갈라진다.
            float gy = Mathf.Min(avail / Mathf.Max(1, rows), gx * 1.15f);
            float y0 = top + (avail - gy * rows) * 0.5f;   // 남는 높이는 위아래로 반씩
            float node = Mathf.Clamp(Mathf.Min(gx, gy) * 0.62f, 34f, 76f);

            // ---- 마디 자리: 뱀처럼 왼쪽→오른쪽→왼쪽으로 내려간다 ----
            var pos = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                int row = i / cols, col = i % cols;
                if (row % 2 == 1) col = cols - 1 - col;          // 지그재그
                // 손으로 놓은 것처럼 조금씩 흔든다 (판마다 늘 같은 값)
                float j1 = (Hash(i * 7 + 3) - 0.5f) * gx * 0.20f;
                float j2 = (Hash(i * 13 + 5) - 0.5f) * gy * 0.28f;
                pos[i] = new Vector2(pad + gx * (col + 0.5f) + j1,
                                     y0 + gy * (row + 0.5f) + j2);
            }

            // ---- 길 — 점을 뿌려 굽은 오솔길처럼 ----
            for (int i = 0; i + 1 < n; i++)
            {
                Vector2 a = pos[i], b = pos[i + 1];
                Vector2 mid = (a + b) * 0.5f;
                Vector2 perp = new Vector2(-(b - a).y, (b - a).x).normalized;
                float bow = (Hash(i * 31 + 11) - 0.5f) * Mathf.Min(70f, (b - a).magnitude * 0.45f);
                Vector2 ctrl = mid + perp * bow;

                bool walked = Unlocked(idx[i + 1]);
                //  ⚠️ 옛 초록 굴 시절 색(0.62, 0.72, 0.66)이었다 — 초록은 슬라임만 (09-12). 무채색으로.
                var col = walked ? new Color(0.70f, 0.72f, 0.75f, 0.55f)
                                 : new Color(0.70f, 0.72f, 0.75f, 0.16f);
                int dots = Mathf.Clamp(Mathf.RoundToInt((b - a).magnitude / (7f * _uiScale)), 4, 40);
                float ds = Mathf.Max(2.5f, 3.4f * _uiScale);
                for (int k = 1; k < dots; k++)
                {
                    float u = k / (float)dots;
                    Vector2 p = (1 - u) * (1 - u) * a + 2 * (1 - u) * u * ctrl + u * u * b;
                    GUI.color = col;
                    GUI.DrawTexture(new Rect(p.x - ds * 0.5f, p.y - ds * 0.5f, ds, ds), Solid(Color.white));
                }
                GUI.color = Color.white;
            }

            // ---- 마디 ----
            var numS = new GUIStyle(_sMid)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Px(Mathf.Clamp(node * 0.36f, 11f, 26f)),
            };
            var starS = new GUIStyle(_sSmall)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Px(Mathf.Clamp(node * 0.26f, 11f, 18f)),
            };

            int hover = -1;
            for (int i = 0; i < n; i++)
            {
                int li = idx[i];
                var d = _set.levels[li];
                bool open = Unlocked(li);
                int s = Stars(d);
                var r = new Rect(pos[i].x - node * 0.5f, pos[i].y - node * 0.5f, node, node);
                //  잠긴 판을 눌렀으면 도리질 — 빨리 흔들리다 잦아든다 (0.35초)
                if (li == _lockShake)
                {
                    float u = Time.time - _lockShakeAt;
                    if (u < 0.35f) r.x += Mathf.Sin(u * 55f) * node * 0.08f * (1f - u / 0.35f);
                }

                // 굴 입구처럼 — 어두운 돌에 위쪽만 밝다
                //  ⚠️ 잠긴 마디를 반투명(0.55)으로 칠해서 **뒤의 점선 길이 마디를 뚫고** 보였다 (09-13 화면).
                //     흐리게 보이는 건 알파가 아니라 **배경 쪽으로 섞은 불투명한 색**으로 낸다.
                var lockCol = Color.Lerp(NodeLock, cave.bg, 0.45f);
                //  🔴 **말랑한 덩어리**로 (09-13 사장님: "슬라임이라는 이름과 어울리게").
                //     열린 판만 아래로 방울이 맺힌다 — 잠긴 판은 굳어 있다.
                //  ⚠️ 방울이 길면(0.55) **주걱·막대사탕**처럼 보였고 폰의 좁은 격자에서 옆 마디에 닿았다 (09-13 화면).
                //     짧고 통통하게 — 막 맺히는 참이다.
                Goo(r, open ? cave.rock : lockCol, open ? 1 : 0, i, 0.62f, 0.30f, 0.17f);
                //  윗면 빛 — 전엔 벽 윗면처럼 띠를 깔았다. 같은 색(굴마다 그 굴의 돌)을 **젖은 윤**으로 얹는다
                if (open) Sheen(r, cave.top, 0.85f);

                // 🔴 마디에 **판 이름**을 찍는다. 자리 번호를 찍고 있어서
                //    1-6 판이 "7"로, 튜토리얼이 "1"로 불리고 있었다 — 판 전부가 한 칸씩 어긋났다.
                //    힌트나 공략을 주고받을 때 서로 다른 판을 가리키게 된다.
                numS.normal.textColor = open ? new Color(1, 1, 1, 0.92f) : new Color(1, 1, 1, 0.4f);
                GUI.Label(new Rect(r.x, r.y + node * 0.10f, r.width, node * 0.5f), NodeLabel(d, li), numS);

                if (open)
                {
                    string ss = "";
                    for (int k = 0; k < 3; k++) ss += k < s ? "★" : "☆";
                    starS.normal.textColor = s > 0 ? StarLit : new Color(1, 1, 1, 0.24f);
                    GUI.Label(new Rect(r.x, r.yMax - node * 0.42f, r.width, node * 0.34f), ss, starS);
                }

                if (r.Contains(Event.current.mousePosition)) hover = li;
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    if (open) { _menu = false; _allDone = false; Load(li); return; }
                    _lockShake = li; _lockShakeAt = Time.time;
                    Play(Sfx.Locked);                   // 둔한 "툭" — 아직 못 연다
                }
            }

            // ---- 지금 서 있는 자리 — 다음에 할 판 위에 슬라임 ----
            //  다음 판이 다른 굴에 있으면 이 굴엔 안 그린다.
            int at = idx.IndexOf(NextUncleared());
            if (at >= 0)
            {
                float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
                float mk = node * 0.30f;
                GUI.color = new Color(1, 1, 1, 0.75f + 0.25f * beat);
                var here = new Rect(pos[at].x - mk * 0.5f,
                                    pos[at].y - node * 0.78f - beat * 3f, mk, mk);
                here.height = mk * 0.7f;
                //  네모 딱지였다 — 슬라임 몸과 같은 **아래가 퍼진 덩어리**로 (09-13)
                Shape(here, HeadCol, PixelSprites.Drop(PixelSprites.DropLevels - 1).texture);
                GUI.color = Color.white;
            }

            // ---- 굴 넘기기 — 양옆 가장자리 ----
            //  ⚠️ 새 글자를 쓰지 않는다 — 잘라낸 글꼴에 없는 글자는 빈칸이 된다 (덫 13).
            int maxCh = MaxChapter();
            var arS = new GUIStyle(_sBig) { alignment = TextAnchor.MiddleCenter };
            float aw = Mathf.Max(36f, pad * 0.9f), ah = aw * 1.6f;
            if (_menuCh > 1 &&
                GUI.Button(new Rect(4f, h * 0.5f - ah * 0.5f, aw, ah), "<", arS)) MenuChapter(-1);
            if (_menuCh < maxCh &&
                GUI.Button(new Rect(w - aw - 4f, h * 0.5f - ah * 0.5f, aw, ah), ">", arS)) MenuChapter(+1);

            // ---- 머리글 ----
            //  🔴 여기도 나가는 길이 Esc 뿐이었다. 띠를 얹어 타이틀로 돌아간다.
            //  별은 **이 굴 안에서** 센다.
            int got = 0;
            foreach (int k in idx) got += Stars(_set.levels[k]);
            float barH = TopBar(w, h, "제 " + _menuCh + " 굴  (" + _menuCh + "/" + maxCh + ")" +
                                      "    ★ " + got + " / " + (n * 3),
                                () => { _menu = false; _title = true; }, false, false);
            _ = barH;

            // ---- 가리키는 판 설명 ----
            if (hover >= 0 && Unlocked(hover))
            {
                var d = _set.levels[hover];
                string line = d.id;
                if (_recs.TryGetValue(d.id, out int rec))
                {
                    bool gs = rec < StarBit;
                    line += (line.Length > 0 ? "   ·   " : "") + (gs ? rec - 0 : rec - StarBit) + "걸음";
                }
                else if (d.cut > 0) line += (line.Length > 0 ? "   ·   " : "") + "커트 " + d.cut + "걸음";
                GUI.Label(new Rect(0, h - bot + 4f, w, 22f * _uiScale), line, _sMid);
            }
            else
            {
                //  폰에는 방향키도 Esc 도 없다 — 양옆 < > 단추를 말한다
                GUI.Label(new Rect(0, h - bot + 4f, w, 22f * _uiScale),
                          Touchy ? "판을 눌러 시작 · < > 로 굴"
                                 : "판을 눌러 시작 · ← → 굴 · 게임 중 Esc 로 여기로", _sSmall);
            }

            //  ⚠️ 유니티 기본 회색 단추(GUI.skin.button)였다 — 납작한 화면에서 혼자 옛 모양이었다 (09-12).
            if (Btn(new Rect(12f, h - 34f * _uiScale, 84f * _uiScale, 26f * _uiScale), "설명"))
            { _intro = true; _askReset = false; }
        }


        // ---- 얼굴 ----
        // 🔴 눈은 미러 뻐다 (09-02 사장님). 얼굴에 있는 건 **열쇠 하나**다.
        //    눈이 있으면 눈길이 눈으로 가는데, 사람이 실제로 봐야 하는 건
        //    "내 열쇠가 어디 있나"이다. 캐릭터다움보다 **읽히는 것**을 골랐다.
        SpriteRenderer _keyGlow, _keyRing, _keyCore;   // 🔴 머리에 박힌 열쇠

        void FaceUp()
        {
            if (_segs.Count == 0) return;
            var head = _segs[0];
            if (!head.enabled) { SetEyes(false); return; }
            SetEyes(true);

            //  뚝 돌지 않고 스르륵 따라간다 — 눈이 움직이는 속도다
            _lookNow = Vector2.Lerp(_lookNow, _lookDir,
                                    1f - Mathf.Exp(-Time.deltaTime * 11f));
            Vector2 look = _lookNow;
            Vector3 hp = head.transform.position;
            float s = Mathf.Min(head.transform.localScale.x, head.transform.localScale.y);
            KeyUp(hp, look, s);
        }

        void SetEyes(bool on)
        {
            if (_keyRing == null) return;
            _keyGlow.enabled = _keyRing.enabled = _keyCore.enabled = on;
        }

        /// <summary>
        /// 🔴 머리에 박힌 **열쇠**. (09-02 사장님)
        ///
        /// 이게 규칙 하나를 설명 없이 만든다 — "머리가 심에서 끝나야 한다"는
        /// 지금까지 **외워야 하는 조건**이었다. 머리에 열쇠가 보이면
        /// 그냥 *열쇠를 열쇠 구멍에 맞추는 일*이 된다. 튜토리얼 한 장이 줄어든다.
        /// 세계관 문장도 원래 이거였다 — **"몸이 곧 열쇠"** (brief §86).
        ///
        /// 열쇠 구멍 위에 서면 열쇠가 밝아지며 뛴다. 맞았다는 걸 손에 쥐여준다.
        /// </summary>
        void KeyUp(Vector3 hp, Vector2 look, float s)
        {
            if (_keyRing == null)
            {
                //  거품(7)·김(8) 보다 위 — 얼굴은 무엇에도 안 가린다
                _keyGlow = NewSprite("KeyGlow", 9);    // 뒤에 번지는 빛
                _keyRing = NewSprite("KeyRing", 10);   // 마름모 테
                _keyCore = NewSprite("KeyCore", 11);   // 마름모 속 — 제일 밝다
                _keyGlow.sprite = PixelSprites.Disc();
                _keyRing.sprite = PixelSprites.Diamond();
                _keyCore.sprite = PixelSprites.Diamond();
            }
            //  🔴 아직 짜여 올라오는 중이면 안 띄운다. 눌린 마디 위에
            //     마름모만 제 크기로 떠 있으면 몸에서 떨어져 나온 것처럼 보인다.
            float born = BornT;
            _keyGlow.enabled = _keyRing.enabled = _keyCore.enabled = born > 0.55f;
            if (!_keyRing.enabled) return;

            //  머리가 열쇠 구멍 위인가
            bool fit = false;
            if (_L != null && _L.Doors != null)
                for (int i = 0; i < _L.Doors.Count && !fit; i++)
                    if (_L.Doors[i].Core >= 0 && _st.Head == _L.Doors[i].Core) fit = true;
            //  🔴 **번짝번짝** (09-02 사장님). 두 박자를 겹친다 —
            //     느린 숨(늘 살아 있다) + 짧게 터지는 섬광(눈길을 끔다).
            //     섬광은 exp 로 떨어뜨린다. sin 으로 하면 번쎍이 아니라 출렁임이 된다.
            float breathe = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.3f);
            float spark   = Mathf.Exp(-Mathf.Repeat(Time.time, KeyBlink) * 7f);
            //  열쇠 구멍 위에 서면 쉬지 않고 뛴다 = 맞았다
            float lit = fit ? 0.55f + 0.45f * Mathf.Sin(Time.time * 9f)
                            : Mathf.Max(spark, 0.22f * breathe);

            //  🔴 **눈처럼 가는 쪽으로 쏠린다** (09-03 사장님).
            //     ⚠️ 09-02 엔 일부러 이마 가운데에 못박았었다 — 홈의 마름모와
            //        **같은 자리**여야 짝이 보인다고 봤기 때문이다. 그건 과했다:
            //        짝은 자리가 아니라 **모양**이 만든다. 둘 다 같은 마름모니까.
            //     조금만 민다. 많이 밀면 머리 밖으로 나가 매달린 것처럼 보인다.
            var at = hp + new Vector3(look.x * KeyLead * s,
                                      0.16f * s + look.y * KeyLead * 0.55f * s, 0f);
            float kw = 0.42f * s * (1f + 0.05f * lit);
            float kh = 0.58f * s * (1f + 0.08f * lit);

            _keyGlow.transform.position = at;
            _keyGlow.transform.localScale = new Vector3(kh * 1.9f, kh * 1.9f, 1f);
            var gc = CoreCol;
            gc.a = 0.10f + 0.26f * lit;
            _keyGlow.color = gc;

            _keyRing.transform.position = at;
            _keyRing.transform.localScale = new Vector3(kw, kh, 1f);
            //  그림이 이미 흰색이다. 번짝임은 **밝기로만** 준다.
            _keyRing.color = Color.Lerp(CoreRing, CoreCol, 0.35f + 0.65f * lit);

            _keyCore.transform.position = at;
            _keyCore.transform.localScale = new Vector3(kw * 0.52f, kh * 0.52f, 1f);
            _keyCore.color = Color.Lerp(CoreCol, Color.white, 0.75f * lit);
        }

        void CloseIntro()
        {
            _intro = false;
            _title = false;
            _menu = true;              // 🔴 안내 다음은 판 고르기다
            PlayerPrefs.SetInt(IntroKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 안내 한 줄 — 색 조각 + 설명.
        /// 🔴 조각은 게임과 **같은 모양**이어야 한다. 네모를 칠하면
        /// 배우는 자리에서 같은 것을 두 가지로 보여주는 꼴이 된다
        /// (09-03 에 안내판에서 똑같은 걸 걷어냈다 — rev.8-d).
        /// </summary>
        /// 한 줄 글이 maxW 를 넘으면 글자를 줄인 **복사본**을 준다. 원래 스타일은 안 건드린다.
        static GUIStyle Fit(GUIStyle st, string text, float maxW)
        {
            float tw = st.CalcSize(new GUIContent(text)).x;
            if (tw <= maxW) return st;
            return new GUIStyle(st) { fontSize = Mathf.Max(10, Mathf.FloorToInt(st.fontSize * maxW / tw)) };
        }

        void Row(float x, float y, float s, Color box, Color inner, string text, GUIStyle st,
                 bool gem = false, float textW = 620f)
        {
            Shape(new Rect(x, y, s, s), box, PixelSprites.Round().texture);
            if (inner.a > 0f)
            {
                //  마름모는 세로로 길다 — 머리·심의 것과 같은 비율(0.40 : 0.55)
                var q = gem ? new Rect(x + s * 0.32f, y + s * 0.12f, s * 0.36f, s * 0.76f)
                            : new Rect(x + s * 0.16f, y + s * 0.16f, s * 0.68f, s * 0.68f);
                Shape(q, inner, gem ? PixelSprites.Diamond().texture
                                    : PixelSprites.Round().texture);
            }
            var t2 = new GUIStyle(Fit(st, text, textW)) { alignment = TextAnchor.MiddleLeft };
            GUI.Label(new Rect(x + s * 1.5f, y - 2, textW, s + 4), text, t2);
        }

        /// <summary>
        /// 🔴 **상단 띠.** 어느 화면에서나 같은 자리다 — 왼쪽에 나가는 길,
        /// 가운데에 지금 어디인지, 오른쪽에 도움과 설정.
        ///
        /// 이게 없어서 **판에 들어가면 나올 길이 Esc 키뿐**이었다.
        /// 폰에는 Esc 가 없다 — 한 번 들어가면 그 판을 깨기 전엔 못 나왔다 (09-04 사장님).
        ///
        /// 🔴 이모지를 안 쓴다. 잘라낸 글꼴에 없어서 **빌드에서만** 빈칸이 된다
        /// (rev.7-a 의 ※ 사건과 같은 종류다).
        /// </summary>
        /// <summary>
        /// 🔴 **조용한 단추** (09-04 사장님: "버튼들 때문에 좀 지저분해 보임").
        ///
        /// 전에는 홈처럼 형광 테를 둘렀는데, 이 게임에서 형광은 **"여기가 목표다"** 라는 뜻이다.
        /// 단추마다 두르니 그 뜻이 묽어지고 실제 홈과 경쟁했다 —
        /// rev.8 의 색 규칙 ③ **색이 있으면 그건 정보다** 를 UI 가 어기고 있었던 것이다.
        ///
        /// 그래서 UI 는 무채색만 쓴다. 손이 올라가면 **밝아질 뿐**이다.
        /// </summary>
        /// <summary>
        /// 🔴 **말랑한 판 한 장** (09-13 사장님: "슬라임이라는 이름과 어울리면 좋겠는데").
        /// r 은 **몸통** 자리다. 방울은 r 아래로 늘어진다 (몸통 높이 × room 만큼).
        /// 색은 부르는 쪽 것 그대로 — 모양만 젤리로 바꾼다. <see cref="PixelSprites.Goo"/>
        /// </summary>
        void Goo(Rect r, Color c, int drips = 0, int seed = 0, float round = 1f, float room = 0.5f, float fat = 0.13f)
        {
            var tex = PixelSprites.Goo(r.width / Mathf.Max(1f, r.height), drips, seed, round, room, fat);
            Shape(new Rect(r.x, r.y, r.width, r.height * (1f + PixelSprites.GooRoom(drips, room))), c, tex);
        }

        /// 젖은 윤 — 왼쪽 위에 얇게 번진 빛. 테 대신 **이게 말랑하다**를 말한다.
        /// ⚠️ 폭을 **높이 기준으로 막는다** — 폭 비율로만 잡았더니 긴 단추에서 윤이 길게 늘어나
        ///    회색 **긁힌 자국**처럼 보였다 (09-13 화면). 둥근 끝 안쪽에서 시작한다.
        void Sheen(Rect r, Color c, float a)
        {
            c.a *= a;
            float sw = Mathf.Min(r.width * 0.36f, r.height * 0.9f);
            float sx = r.x + Mathf.Min(r.width * 0.16f, r.height * 0.34f);
            Shape(new Rect(sx, r.y + r.height * 0.13f, sw, r.height * 0.15f), c, PixelSprites.Disc().texture);
        }

        bool Btn(Rect r, string label, bool on = true)
        {
            bool hot = on && r.Contains(Event.current.mousePosition);
            Color fill = !on ? new Color(1, 1, 1, 0.035f)
                       : hot ? new Color(1, 1, 1, 0.155f)
                             : new Color(1, 1, 1, 0.075f);
            //  🔴 네모난 판이 아니라 **젤리 알**로 (09-13). 테는 여전히 없다 — 윤만 얹는다.
            Goo(r, fill);
            if (on) Sheen(r, Color.white, hot ? 0.10f : 0.05f);

            if (!string.IsNullOrEmpty(label))
            {
                var ls = new GUIStyle(_sMid)
                {
                    fontSize = Px(Mathf.Clamp(r.height * 0.42f, 12f, 20f)),
                    alignment = TextAnchor.MiddleCenter,
                };
                ls.normal.textColor = !on ? new Color(1, 1, 1, 0.30f)
                                    : hot ? Color.white
                                          : new Color(1, 1, 1, 0.78f);
                GUI.Label(r, label, ls);
            }
            bool click = on && GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (click) Play(Sfx.Tap);                   // 작은 "톡" (09-13)
            return click;
        }

        /// <summary>
        /// 🔴 **소리 아이콘** — 글자 "소리" 대신 눈금 셋 (09-04 사장님).
        /// 이모지는 못 쓴다. 잘라낸 글꼴에 없어서 **빌드에서만** 빈칸이 된다
        /// (rev.7-a 의 ※ 사건과 같은 종류다). 그래서 우리 도형으로 직접 그린다.
        /// 꺼져 있으면 흐려지고 빗금이 그어진다.
        /// </summary>
        void IconSound(Rect r, bool muted)
        {
            Color c = muted ? new Color(1, 1, 1, 0.34f) : new Color(1, 1, 1, 0.84f);
            Shape(r, c, PixelSprites.Speaker().texture);
            if (muted)
            {
                //  꺼진 것은 **빗금**으로. 흐리게만 하면 "덜 중요한 것" 으로 보인다.
                var mm = GUI.matrix;
                GUIUtility.RotateAroundPivot(-34f, r.center);
                Shape(new Rect(r.center.x - r.width * 0.44f, r.center.y - r.height * 0.05f,
                               r.width * 0.88f, r.height * 0.10f),
                      new Color(1, 1, 1, 0.78f), PixelSprites.Soft().texture);
                GUI.matrix = mm;
            }
        }

        /// <summary>
        /// 🔴 **소리 막대.** 유니티 기본 슬라이더는 회색이라 이 게임의 어느 것과도 안 닮았다.
        /// 홈과 같은 말투로 — 어두운 속에 형광이 찬 만큼 들어찬다.
        ///
        /// 끌기는 손을 뗄 때까지 이어진다. 막대 밖으로 나가도 놓치지 않는다.
        /// </summary>
        float VolumeBar(Rect r, float v)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && r.Contains(e.mousePosition)) _volDrag = true;
            if (e.rawType == EventType.MouseUp) _volDrag = false;
            if (_volDrag && (e.type == EventType.MouseDrag || e.type == EventType.MouseDown))
            {
                v = Mathf.Clamp01((e.mousePosition.x - r.x) / Mathf.Max(1f, r.width));
                e.Use();
            }

            Shape(r, HoleCol, PixelSprites.Soft().texture);
            if (v > 0.001f)
            {
                float fw = Mathf.Max(r.height, r.width * v);
                Shape(new Rect(r.x, r.y, fw, r.height), FrameCol, PixelSprites.Soft().texture);
            }
            //  잡는 자리 — 어디를 끌면 되는지 보여준다
            float hx = r.x + Mathf.Clamp(r.width * v, r.height * 0.5f, r.width - r.height * 0.5f);
            Shape(new Rect(hx - r.height * 0.34f, r.y - r.height * 0.34f,
                           r.height * 0.68f, r.height * 1.68f),
                  Color.white, PixelSprites.Round().texture);
            return v;
        }

        /// <summary>
        /// 🔴 **가운데 팝업**으로 여는 소리 조절 (09-04 사장님).
        /// 판 위 구석에 펼치던 것을 화면 한가운데로 옮겼다 — 판을 가리더라도
        /// 지금 무엇을 만지는지가 분명한 쪽이 낫다.
        ///
        /// 게임 안에서는 설정 전체가 아니라 **소리만** 연다.
        /// 판을 하다 바꾸고 싶은 것은 소리뿐이다.
        /// </summary>
        void VolumeModal(float w, float h)
        {
            float pw2 = Mathf.Clamp(w * 0.32f, 260f, 420f);
            float ph2 = Mathf.Clamp(h * 0.30f, 190f, 280f);
            var box = new Rect(w * 0.5f - pw2 * 0.5f, h * 0.5f - ph2 * 0.5f, pw2, ph2);

            //  뒤를 어둡게. 바깥을 누르면 닫힌다 — 닫는 길이 하나뿐이면 갇힌 느낌이 든다.
            GUI.DrawTexture(new Rect(0, 0, w, h), Solid(new Color(0, 0, 0, 0.62f)));
            var ev = Event.current;
            if (ev.type == EventType.MouseDown && !box.Contains(ev.mousePosition))
            { _volPanel = false; ev.Use(); return; }

            //  형광 테를 두르면 팝업이 **홈처럼** 보인다. 얇은 회색 선만 남긴다.
            Shape(box, new Color(1, 1, 1, 0.14f), PixelSprites.Soft().texture);
            float m2 = Mathf.Max(1.5f, ph2 * 0.008f);
            Shape(new Rect(box.x + m2, box.y + m2, box.width - m2 * 2f, box.height - m2 * 2f),
                  new Color(0.055f, 0.065f, 0.078f, 1f), PixelSprites.Soft().texture);

            float ix = box.x + pw2 * 0.10f, iw = pw2 * 0.80f;

            var hs = new GUIStyle(_sMid)
                     { alignment = TextAnchor.MiddleCenter, fontSize = Px(Mathf.Clamp(ph2 * 0.115f, 14f, 22f)) };
            hs.normal.textColor = new Color(1, 1, 1, 0.92f);
            GUI.Label(new Rect(ix, box.y + ph2 * 0.09f, iw, ph2 * 0.16f),
                      _mute ? "소리 꺼짐" : "소리  " + Mathf.RoundToInt(_volume * 100) + "%", hs);

            var barR = new Rect(ix, box.y + ph2 * 0.36f, iw, Mathf.Max(8f, ph2 * 0.075f));
            if (_mute)
            {
                Shape(barR, HoleCol, PixelSprites.Soft().texture);
            }
            else
            {
                float nv = VolumeBar(barR, _volume);
                if (!Mathf.Approximately(nv, _volume))
                {
                    _volume = nv;
                    PlayerPrefs.SetFloat(VolKey, _volume); PlayerPrefs.Save();
                }
            }

            //  🔴 단추를 **아래에서부터** 쌓는다. 위에서 내려 쌓았더니 닫기가
            //     팝업 밑변에 붙어(여백 1.5%) 넘친 것처럼 보였다 (09-04 사장님).
            float bhh = ph2 * 0.175f, bgap = ph2 * 0.05f;
            float ybot = box.yMax - ph2 * 0.10f;      // 아래 여백
            float y2 = ybot - bhh;                    // 닫기
            float y1 = y2 - bgap - bhh;               // 음소거
            if (Btn(new Rect(ix, y1, iw, bhh), _mute ? "소리 켜기" : "음소거"))
            {
                _mute = !_mute;
                PlayerPrefs.SetInt(MuteKey, _mute ? 1 : 0); PlayerPrefs.Save();
            }
            if (Btn(new Rect(ix, y2, iw, bhh), "닫기")) _volPanel = false;
        }

        float TopBar(float w, float h, string mid, System.Action back, bool help, bool sound)
        {
            //  🔴 **어디서나 같은 자리다.** 가르치는 판만 폭을 72% 로 줄여놨더니
            //     설정이 화면 오른쪽 끝이 아니라 가운데쯤에, 이름표도 36% 자리에 있었다 —
            //     같은 것이 화면마다 다른 데 있으면 그게 제일 어색하다 (09-04 사장님).
            //     안내판 내용은 이 띠 아래에서 시작하게 내려놨다 (Guide 참고).
            float bw = w;
            float bh = Mathf.Clamp(h * 0.052f, 30f, 44f);
            //  🔴 가장자리에서 **떼어 놓는다.** 여백이 10픽셀뿐이라 화면 테두리에 붙어
            //     "잘린 것" 처럼 보였다 (09-04 사장님 화면). 좌우도 같은 만큼 띄운다.
            float pad = Mathf.Clamp(h * 0.026f, 14f, 30f);

            //  🔴 어두운 **띠를 안 깐다.** 가르치는 판에서는 이 띠가 화면 72% 에서 뚝 끊기고,
            //     거기부터 안내판의 다른 어두운 색이 시작해서 위쪽에 이음매가 생겼다
            //     (09-04 사장님: "상단 버튼 위치가 좀 이상함").
            //     단추는 테와 속을 이미 갖고 있어서 판 위에 그냥 띄워도 읽힌다.

            //  🔴 글자 대신 **화살표 하나**. 단추가 작아질수록 화면이 조용해진다.
            float bwid = bh;
            float by0 = pad, bh0 = bh;
            if (back != null && Btn(new Rect(pad, by0, bwid, bh0), "←"))
            { back(); return by0 + bh0; }

            //  가운데 이름표 — 판 위에 글자만 두면 밝은 돌 위에서 안 읽힌다.
            //  단추와 같은 어두운 속을 깔고 그 위에 쓴다 (테는 없다 — 누르는 게 아니다).
            var ms = new GUIStyle(_sMid) { fontSize = Px(Mathf.Clamp(bh * 0.40f, 13f, 19f)) };
            ms.normal.textColor = new Color(1, 1, 1, 0.88f);
            //  🔴 이름표가 양옆 단추에 **닿으면 글자를 줄인다.** 세로 폰에서 "제 1 굴 (1/6) ★ 7 / 21" 이
            //     「설정」 단추에 붙어 버렸다 (09-13 화면). 가운데 정렬이라 넓은 쪽 단추 기준으로 잰다.
            float sideR = pad + (sound ? bh : Mathf.Clamp(bh * 1.7f, 56f, 96f))
                        + (help ? Mathf.Clamp(bh * 0.9f, 30f, 52f) + pad * 0.45f : 0f);
            float sideL = back != null ? pad + bwid : 0f;
            float maxMw = bw - 2f * (Mathf.Max(sideL, sideR) + pad * 0.5f);
            float mw = Mathf.Max(ms.CalcSize(new GUIContent(mid)).x + bh * 1.1f, bh * 2.6f);
            if (mw > maxMw && maxMw > 0f)
            {
                ms = Fit(ms, mid, maxMw - bh * 1.1f);
                mw = maxMw;
            }
            var mr = new Rect(bw * 0.5f - mw * 0.5f, by0, mw, bh0);
            var plate = HoleCol; plate.a = 0.82f;
            Goo(mr, plate);
            GUI.Label(mr, mid, ms);

            float rx = bw - pad;
            float sw = sound ? bh : Mathf.Clamp(bh * 1.7f, 56f, 96f);
            rx -= sw;
            if (sound)
            {
                //  여기서는 **켜기만** 한다. 팝업은 맨 마지막에 그려야 다른 것 위에 온다.
                var sr = new Rect(rx, by0, sw, bh0);
                bool click = Btn(sr, null);
                IconSound(new Rect(sr.x + sr.width * 0.22f, sr.y + sr.height * 0.22f,
                                   sr.width * 0.56f, sr.height * 0.56f), _mute);
                if (click) _volPanel = !_volPanel;
            }
            else if (Btn(new Rect(rx, by0, sw, bh0), "설정"))
            {
                _settingsFrom = _menu ? 2 : 1;
                _menu = false; _settings = true; _askReset = false; _volPanel = false;
            }
            if (help)
            {
                float qw = Mathf.Clamp(bh * 0.9f, 30f, 52f);
                rx -= qw + pad * 0.45f;
                if (Btn(new Rect(rx, by0, qw, bh0), "?"))
                { _intro = true; _askReset = false; }
            }
            return by0 + bh0;
        }

        /// <summary>
        /// 🔴 **테두리 없는 글자 단추** (09-04 사장님: "테두리 두르지 말고 깔끔하게").
        /// 판 안의 단추(<see cref="Btn"/>)는 홈처럼 테를 두르지만, 타이틀은 글자만 둔다 —
        /// 여기는 눌러야 할 것이 넷뿐이라 테로 구분해 줄 이유가 없다.
        ///
        /// 손이 올라간 줄에만 **작은 마름모**가 왼쪽에 붙는다.
        /// 색만 바꾸면 어느 줄에 있는지 한눈에 안 들어온다.
        /// </summary>
        bool BtnText(Rect r, string label, bool on = true)
        {
            bool hot = on && r.Contains(Event.current.mousePosition);
            var ls = new GUIStyle(_sMid)
            {
                fontSize = Px(Mathf.Clamp(r.height * 0.46f, 14f, 24f)),
                alignment = TextAnchor.MiddleRight,
            };
            ls.normal.textColor = !on ? new Color(1, 1, 1, 0.26f)
                                : hot ? Color.white
                                      : new Color(1, 1, 1, 0.76f);
            GUI.Label(r, label, ls);

            if (hot)
            {
                //  글자가 오른쪽에 붙어 있으니 표는 **글자 폭을 재서** 그 왼쪽에 둔다
                float tw = ls.CalcSize(new GUIContent(label)).x;
                float d = r.height * 0.26f;
                Shape(new Rect(r.xMax - tw - d * 1.9f, r.center.y - d * 0.58f,
                               d * 0.80f, d * 1.16f), FrameCol,
                      PixelSprites.Diamond().texture);
            }
            bool click = on && GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (click) Play(Sfx.Tap);
            return click;
        }

        /// <summary>
        /// 시작 연출을 켠다. 🔴 여기서 게임으로 넘어가지 **않는다** —
        /// 슬라임이 홈에 들어가고 빨려드는 걸 다 보여준 뒤에 넘어간다.
        /// </summary>
        void StartShowBegin(int what)
        {
            _startWhat = what;
            _startAt = Time.time;
            if (_guide != null) _guide.DemoRush();
        }

        /// <summary>
        /// 🔴 **타이틀.** 로고는 가운데 위, 고를 것은 오른쪽.
        ///
        /// 슬라임 모양은 뺐다 (09-04 사장님) — 뒤에 진짜 슬라임이 돌고 있어서
        /// 로고의 슬라임과 섞여 보였다. 이름 하나로 간다.
        /// </summary>
        void Title(float w, float h)
        {
            //  🔴 왼쪽 아래에 **진짜 플레이어 캐릭터**가 돈다 (09-04 사장님, 시안 2).
            //     뒤의 판을 비추는 게 아니라, 시범 인스턴스로 **작은 판 하나**를 따로 돌린다 —
            //     그래야 큰 판이 비쳐서 지저분해지는 일이 없다.
            float sw2 = w * 0.34f, sh2 = h * 0.34f;
            float sx2 = w * 0.055f, sy2 = h - h * 0.09f - sh2;

            if (_guide != null && _guideCam != null)
            {
                if (_guideSlide != GuideTitle)
                {
                    _guideSlide = GuideTitle;
                    _guide.SetupDemo(_guideCam, TitleMap, TitleKeys, _gravity);
                }
                //  🔴 배경을 타이틀과 **같은 색**으로. 안내판 무대 색(StageBg) 이면
                //     여기서 네모난 상자로 떠 보인다.
                _guideCam.backgroundColor = BgCol;
                _guideCam.rect = new Rect(sx2 / w, 1f - (sy2 + sh2) / h, sw2 / w, sh2 / h);
                _guideCam.enabled = true;
            }

            //  🔴 가림막을 **네 조각**으로 — 슬라임 자리만 비운다.
            //     통째로 덮으면 IMGUI 가 카메라보다 위라 슬라임이 가려진다.
            //     (틈을 두면 어느 카메라도 안 지우는 자리가 생긴다 — 딱 붙인다)
            var scrim = BgCol; scrim.a = 1f;
            var st = Solid(scrim);
            GUI.DrawTexture(new Rect(0, 0, w, sy2), st);
            GUI.DrawTexture(new Rect(0, sy2 + sh2, w, h - sy2 - sh2), st);
            GUI.DrawTexture(new Rect(0, sy2, sx2, sh2), st);
            GUI.DrawTexture(new Rect(sx2 + sw2, sy2, w - sx2 - sw2, sh2), st);

            //  ---- 시작 연출 : 슬라임이 홈으로 들어가는 동안 글자가 사라진다 ----
            float fade = 1f;
            if (_startAt >= 0f)
            {
                float u = (Time.time - _startAt) / StartShow;
                fade = Mathf.Clamp01(1f - u / 0.26f);        // 글자·단추가 먼저 빠진다
                float cover = Mathf.Clamp01((u - 0.74f) / 0.26f);

                if (cover > 0f)
                {
                    //  마지막에 화면을 덮는다 — 슬라임 자리까지 같이
                    var cc = BgCol; cc.a = cover;
                    GUI.DrawTexture(new Rect(0, 0, w, h), Solid(cc));
                }
                if (u >= 1f)
                {
                    _startAt = -1f;
                    if (_guideCam != null) _guideCam.enabled = false;
                    _title = false;
                    if (_startWhat == 1)
                    {
                        //  목록을 안 거치고 **하던 데로** 바로
                        _menu = false; Load(NextUncleared());
                    }
                    else
                    {
                        //  진행을 지우고 **튜토리얼 판(1-0)으로** 바로. ResetAll 이 Load(0) 한다
                        ResetAll();
                    }
                    return;
                }
            }

            //  ---- 로고 : 가운데, 위에서 조금 내려온 자리 ----
            var big = new GUIStyle(_sBig)
            {
                fontSize = Px(Mathf.Clamp(Mathf.Min(w, h) * 0.085f, 24f, 64f)),
                alignment = TextAnchor.MiddleCenter,
            };
            big.normal.textColor = new Color(1, 1, 1, 0.96f * fade);
            float ly = h * 0.145f;
            float lh = big.fontSize * 1.6f;
            GUI.Label(new Rect(0, ly, w, lh), "SlimeEscape", big);

            //  이름 아래 얇은 형광 줄 — 글자 폭에 맞춘다
            float tw2 = big.CalcSize(new GUIContent("SlimeEscape")).x;
            var rule = FrameCol; rule.a = 0.55f * fade;
            Bar(new Rect(w * 0.5f - tw2 * 0.34f, ly + lh * 0.86f, tw2 * 0.68f,
                         Mathf.Max(2f, h * 0.0035f)), rule);

            //  🔴 글자 밑으로 **방울이 맺힌다** (09-13 사장님: "슬라임이라는 이름과 어울리면 좋겠는데").
            //     09-04 에 로고의 슬라임 **모양**은 뺐다 — 뒤의 진짜 슬라임과 섞였다.
            //     그래서 캐릭터를 그리지 않고 **글자가 녹아 흐르는 것**으로 말한다. 색도 글자 색 그대로 (초록 아님).
            //  줄기 밑이 평평한 글자에만 단다 — l · m · E · a. p 는 꼬리가 있어 뺐다.
            {
                const string logo = "SlimeEscape";
                int[] at = { 1, 3, 5, 8 };
                float[] len = { 2.6f, 1.3f, 3.4f, 1.8f };
                float x0 = w * 0.5f - tw2 * 0.5f;
                float stroke = big.fontSize * 0.085f;
                float baseY = ly + lh * 0.5f + big.fontSize * 0.36f;
                var dc = big.normal.textColor;
                for (int k = 0; k < at.Length; k++)
                {
                    //  그 글자 **끝까지** 잰 폭에서 글자 반 폭을 뺀다 — 자간(kerning)까지 들어간다
                    float cxk = x0 + big.CalcSize(new GUIContent(logo.Substring(0, at[k] + 1))).x
                                   - big.CalcSize(new GUIContent(logo.Substring(at[k], 1))).x * 0.5f;
                    //  몸통은 획 굵기만 하게 글자 밑에 **묻고**, 방울은 굵게 — 가늘면 점으로 사라진다 (09-13 화면)
                    float bw3 = stroke * 2.0f, bh3 = stroke * 1.3f;
                    Goo(new Rect(cxk - bw3 * 0.5f, baseY - bh3 * 0.9f, bw3, bh3), dc, 1, k * 5 + 2, 1f, len[k], 0.42f);
                }
            }

            //  ---- 고를 것 : 오른쪽 ----
            float rowH = Mathf.Clamp(h * 0.085f, 38f, 64f);
            float colW = Mathf.Clamp(w * 0.30f, 200f, 380f);
            float rx = w - Mathf.Clamp(w * 0.10f, 28f, 120f) - colW;
            float ry = h * 0.44f, gap = rowH * 1.12f;

            //  연출이 도는 동안에는 아무것도 못 누른다 — 두 번 누르면 두 번 넘어간다
            if (_startAt >= 0f) return;

            if (_askWipe)
            {
                var ws = new GUIStyle(_sMid)
                         { alignment = TextAnchor.MiddleRight,
                           fontSize = Px(Mathf.Clamp(rowH * 0.36f, 12f, 18f)) };
                ws.normal.textColor = new Color(1, 0.82f, 0.62f, 0.95f);
                GUI.Label(new Rect(rx, ry - rowH, colW, rowH), "지금까지 한 것이 지워집니다", ws);

                if (BtnText(new Rect(rx, ry, colW, rowH), "지우고 새로 하기"))
                { _askWipe = false; StartShowBegin(0); return; }
                if (BtnText(new Rect(rx, ry + gap, colW, rowH), "그만두기"))
                { _askWipe = false; return; }
                return;
            }

            if (BtnText(new Rect(rx, ry, colW, rowH), "새로 하기"))
            {
                if (HasSave) { _askWipe = true; return; }
                StartShowBegin(0); return;
            }
            //  이어할 게 없으면 못 누른다 — BtnText 가 흐리게 그린다
            if (BtnText(new Rect(rx, ry + gap, colW, rowH), "이어하기", HasSave))
            { StartShowBegin(1); return; }

            if (BtnText(new Rect(rx, ry + gap * 2f, colW, rowH), "설정"))
            {
                //  안 끄면 설정 화면 왼쪽에 슬라임이 그대로 남는다
                if (_guideCam != null) _guideCam.enabled = false;
                _title = false; _settingsFrom = 0; _settings = true; return;
            }

            //  🔴 웹에서는 **숨긴다** — 브라우저 탭은 게임이 못 닫아서 눌러도 아무 일도 안 일어났다 (09-13)
            if (Application.platform != RuntimePlatform.WebGLPlayer
                && BtnText(new Rect(rx, ry + gap * 3f, colW, rowH), "나가기"))
            {
                //  🔴 웹에서는 아무 일도 안 일어난다 — 브라우저 탭은 게임이 못 닫는다.
                //     윈도우·안드로이드·iOS 에서만 실제로 꺼진다.
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                return;
            }
        }

        /// <summary>
        /// 설정. 🔴 **정말 쓰는 것만** 둔다 — 안 쓰는 항목을 늘어놓으면
        /// 고르는 데 시간만 들고, 저마다 저장·불러오기 자리가 하나씩 는다.
        /// </summary>
        void Settings(float w, float h)
        {
            var scrim2 = BgCol; scrim2.a = 1f;
            GUI.DrawTexture(new Rect(0, 0, w, h), Solid(scrim2));

            //  타이틀과 **같은 짜임**이라야 넘어갈 때 어색하지 않다 — 오른쪽에 글자만.
            var big = new GUIStyle(_sBig)
            {
                fontSize = Px(Mathf.Clamp(Mathf.Min(w, h) * 0.085f, 24f, 64f)),
                alignment = TextAnchor.MiddleCenter,
            };
            big.normal.textColor = new Color(1, 1, 1, 0.96f);
            float ly2 = h * 0.17f, lh2 = big.fontSize * 1.6f;
            GUI.Label(new Rect(0, ly2, w, lh2), "설정", big);
            float tw3 = big.CalcSize(new GUIContent("설정")).x;
            var rule2 = FrameCol; rule2.a = 0.55f;
            Bar(new Rect(w * 0.5f - tw3 * 0.60f, ly2 + lh2 * 0.86f, tw3 * 1.20f,
                         Mathf.Max(2f, h * 0.0035f)), rule2);

            float bh = Mathf.Clamp(h * 0.085f, 38f, 64f);
            float bw = Mathf.Clamp(w * 0.30f, 200f, 380f);
            float bx = w - Mathf.Clamp(w * 0.10f, 28f, 120f) - bw;
            float by = h * 0.44f, gap = bh * 1.12f;

            //  판 안의 소리 조절과 **같은 막대**를 쓴다 — 두 군데 생김새가 다르면 딴 기능처럼 보인다
            var vr = new Rect(bx, by + bh * 0.42f, bw, Mathf.Max(6f, bh * 0.16f));
            if (_mute)
            {
                Shape(vr, HoleCol, PixelSprites.Round().texture);
            }
            else
            {
                float nv2 = VolumeBar(vr, _volume);
                if (!Mathf.Approximately(nv2, _volume))
                {
                    _volume = nv2;
                    PlayerPrefs.SetFloat(VolKey, _volume); PlayerPrefs.Save();
                }
            }
            var vs = new GUIStyle(_sSmall)
                     { alignment = TextAnchor.MiddleRight, fontSize = Px(Mathf.Clamp(bh * 0.30f, 11f, 16f)) };
            vs.normal.textColor = new Color(1, 1, 1, _mute ? 0.30f : 0.72f);
            GUI.Label(new Rect(bx, by - bh * 0.05f, bw, bh * 0.42f),
                      _mute ? "소리 꺼짐" : "소리  " + Mathf.RoundToInt(_volume * 100) + "%", vs);

            if (BtnText(new Rect(bx, by + gap, bw, bh), _mute ? "소리 켜기" : "음소거"))
            {
                _mute = !_mute;
                PlayerPrefs.SetInt(MuteKey, _mute ? 1 : 0); PlayerPrefs.Save();
            }
            if (BtnText(new Rect(bx, by + gap * 2f, bw, bh), "조작 안내 다시 보기"))
            { _settings = false; _intro = true; return; }

            if (_askReset)
            {
                if (BtnText(new Rect(bx, by + gap * 3f, bw, bh), "정말 지웁니다"))
                {
                    _settings = false; ResetAll();
                    //  지우고 나면 타이틀로 — 거기서 이어하기가 흐려진 걸 봐야 지워진 게 보인다
                    _title = true; _guideSlide = -1;
                    return;
                }
                if (BtnText(new Rect(bx, by + gap * 4f, bw, bh), "그만두기"))
                { _askReset = false; return; }
            }
            else
            {
                if (BtnText(new Rect(bx, by + gap * 3f, bw, bh), "진행 지우기"))
                { _askReset = true; return; }
                if (BtnText(new Rect(bx, by + gap * 4f, bw, bh), "돌아가기"))
                {
                    _settings = false;
                    if (_settingsFrom == 2) _menu = true;
                    else if (_settingsFrom == 1) { }        // 판으로 그냥 돌아간다
                    else _title = true;
                    return;
                }
            }
        }

        void Intro(float w, float h)
        {
            float s = Mathf.Clamp(h * 0.055f, 22f, 52f);          // 색 조각 한 변
            float gap = s * 1.5f;
            float x = w * 0.5f - Mathf.Min(w * 0.42f, 320f);
            float y = h * 0.5f - gap * 2.6f;

            //  🔴 판 위에 **그냥 겹쳐 그리고 있었다** — 설명 글이 뒤의 판(홈·조각)과 섞여 안 읽혔다 (09-13 화면).
            //     굴 배경색으로 거의 불투명하게 먼저 덮는다. 판 카메라는 그대로 돈다 (todo 모바일 항목).
            var veil = BgCol; veil.a = 0.94f;
            Tex(new Rect(0, 0, w, h), veil);

            GUI.Label(new Rect(0, y - gap * 2.0f, w, s * 1.4f), "슬라임 탈출", _sBig);

            //  글 폭은 **화면 안에서** 잰다 — 620 고정이라 세로 폰(390)에서 오른쪽이 잘렸다 (09-13).
            float tw = w - (x + s * 1.5f) - 16f;
            Row(x, y, s, BodyCol, new Color(0, 0, 0, 0), "이게 나. 한 칸씩 움직인다", _sMid, false, tw);
            y += gap;
            Row(x, y, s, Floor, FoodCol, "조각을 먹으면 몸이 길어진다", _sMid, false, tw);
            y += gap;
            //  🔴 홈 테두리는 **FrameCol**(형광 초록)이다. HoleEdge 는 칸이 앉을 때
            //     한 번 번쩍이는 색이라, 여기 쓰면 게임에 없는 색을 가르치게 된다.
            Row(x, y, s, FrameCol, HoleCol, "여기를 몸으로 채운다  —  남아도 모자라도 안 된다", _sMid, false, tw);
            y += gap;
            Row(x, y, s, HoleCol, CoreCol, "머리의 마름모가 여기 앉아야 끝난다", _sMid, true, tw);
            y += gap * 1.5f;

            string keys = Touchy ? "버튼으로 움직인다  ·  되돌리기는 몇 번이든 된다"
                                 : "← ↑ ↓ →  움직이기      Z  되돌리기      R  처음부터";
            GUI.Label(new Rect(0, y, w, s), keys, Fit(_sMid, keys, w - 32f));
            //  ⛔ 여기 있던 「처음부터 다시」(진행 초기화) 단추를 뺐다 (09-13, todo 「없어야 할 것」).
            //     규칙을 보러 ? 로 들어오는 자리라 실수로 누르기 쉬웠고, 초기화가 세 군데였다.
            //     지우는 길은 타이틀 「새로 하기」(물어봄) · 설정 「진행 지우기」 둘로 충분하다.

            GUI.Label(new Rect(0, h - s * 2.2f, w, s),
                Touchy ? "화면을 누르면 시작" : "아무 키나 누르면 시작", _sSmall);
        }

        void Styles()
        {
            // 🔴 휴대폰은 화면이 작다. 21px 글씨는 안 보인다 — 높이에 맞춰 키운다.
            if (_sBig != null && _styledAt == Screen.height) return;
            _styledAt = Screen.height;
            _uiScale = Mathf.Clamp(Screen.height / 720f, 0.85f, 2.2f);
            _krFont = Resources.Load<Font>("Fonts/kr");
            if (_krFont == null) Debug.LogWarning("[글꼴] Resources/Fonts/kr 를 못 찾았다 — WebGL에서 한글이 깨진다");
            _sBig = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(21 * _uiScale), alignment = TextAnchor.MiddleCenter };
            _sMid = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(16 * _uiScale), alignment = TextAnchor.MiddleCenter };
            _sSmall = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * _uiScale), alignment = TextAnchor.MiddleCenter };
            _sBig.normal.textColor = new Color(1f, 1f, 1f, 0.88f);
            _sMid.normal.textColor = new Color(1f, 1f, 1f, 0.70f);
            _sSmall.normal.textColor = new Color(1f, 1f, 1f, 0.40f);

            // 🔴 IMGUI는 GUIStyle마다 지정해야 한다. 하나라도 빠뜨리면 거기만 깨진다.
            if (_krFont != null)
            {
                _sBig.font = _sMid.font = _sSmall.font = _krFont;
                GUI.skin.font = _krFont;      // GUILayout.Label 등 기본 스타일까지
            }
        }

        void OnGUI()
        {
            if (_demo) return;          // 시범은 판만 보여준다. 글자는 본판이 그린다.
            //  🔴 안내판이 안 뜨는 판에서는 시범 카메라를 꺼둔다.
            //     안 끄면 화면 한 귀퉁이에 시범이 그대로 남는다.
            //  타이틀은 시범을 **직접 켠다** (아래 Title 참고). 여기서 끄면 안 된다.
            if (_guideCam != null && _guideCam.enabled && !_title
                && (!Def.tutorial || _menu || _intro || _settings))
                _guideCam.enabled = false;
            Styles();
            float w = Screen.width, h = Screen.height;
            var def = Def;
            int filled = SnakeEngine.Filled(_L, _st);
            int need = _L.Target.Count;

            //  🔴 타이틀·설정이 제일 앞이다. 뒤에 두면 판 화면이 먼저 그려져
            //     한 프레임 깜빡인다.
            if (_title)   { Title(w, h); return; }
            if (_settings) { Settings(w, h); return; }

            if (_intro)
            {
                Intro(w, h);
                // 버튼을 눌렀으면 GUI.Button이 이미 먹었다. 그 밖을 눌렀을 때만 닫는다
                if (Event.current.type == EventType.MouseDown && !_askReset) CloseIntro();
                return;
            }

            if (_result) { Result(w, h); return; }
            if (_menu) { Menu(w, h); return; }

            // ---- 다 깼으면 결과만 보여준다 ----
            //    🔴 WebGL은 파일을 못 쓴다. 이 화면이 기록을 돌려받는 유일한 통로다.
            if (_allDone)
            {
                GUI.Label(new Rect(0, 40, w, 30), "다 깼습니다. 고맙습니다!", _sBig);
                GUI.Label(new Rect(0, 74, w, 24),
                    "이 화면을 찍어서 보내주세요", _sMid);
                // 🔴 표는 줄이 밀리면 안 된다. 화면 폭에 맞춰 상자를 잡고 글씨를 줄인다.
                //    (08-30: 좁은 화면에서 머리글이 세 줄로 접혀 판을 덮었다)
                float bw = Mathf.Min(w - 32f, 620f);
                float bh = Mathf.Min(h - 190f, 74f + 20f * (SnakeLog.Runs.Count + 2));
                var box = new Rect(w * 0.5f - bw * 0.5f, 104f, bw, bh);
                GUI.Box(box, GUIContent.none);
                var tbl = new GUIStyle(_sSmall)
                {
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = false,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(bw / 36f), 9, 15),
                };
                tbl.normal.textColor = new Color(1f, 1f, 1f, 0.78f);
                GUI.Label(new Rect(box.x + 12, box.y + 8, box.width - 24, box.height - 44),
                          SnakeLog.Table(), tbl);
                GUI.Label(new Rect(box.x + 12, box.yMax - 32, box.width - 24, 24),
                          SnakeLog.Summary(), tbl);
                GUI.Label(new Rect(0, h - 56, w, 22),
                    "재미있었는지 · 어디서 막혔는지 · 그만두고 싶었는지 한 줄만 적어주시면 큰 도움이 됩니다", _sMid);
                GUI.Label(new Rect(0, h - 32, w, 20), "R  처음부터 다시", _sSmall);
                return;
            }

            // 🔴 가르치는 판은 **오른쪽 안내판이 전부**다. 판 위에는 아무것도 안 얹는다.
            //    제목·남은 홈·걸음·커트가 네 줄로 쌓여 어디를 볼지 몰랐다 (08-31).
            //  🔴 튜토리얼 판은 여기서 **빠져나간다.** 띠를 아래에 두면 안 그려진다
            //     (09-04 사장님: "튜토리얼 스테이지에서는 버튼 안 보여").
            if (def.tutorial)
            {
                //  🔴 여기서 매 프레임 NullReferenceException 이 났다 (09-05 에디터 로그에서
                //     한 판에 8575건). 스택이 OnGUI 한 줄뿐이라 어느 문장인지 못 집었다.
                //     원인으로 짚은 것은 **지워진 그림**이다 (PixelSprites 에 HideAndDontSave 가
                //     없었다). 고쳤지만 확인은 못 했으니, **한 번만** 자세히 남겨 다음에 알게 한다.
                //     ⚠️ 삼키는 게 아니라 **한 줄로 줄이는 것**이다 — 8575줄이면 아무도 안 본다.
                try { Guide(w, h); }
                catch (System.Exception e)
                {
                    if (!_guideBlew) { _guideBlew = true; Debug.LogError("[안내판] " + e); }
                }
                string ts0 = "";
                int tg = Stars(def);
                for (int k = 0; k < 3; k++) ts0 += k < tg ? "★" : "☆";
                TopBar(w, h, def.id + "   " + ts0, () => { _menu = true; _volPanel = false; }, true, true);
                //  🔴 손가락 패드도 그린다 (09-08 사장님). 여기서 빠져나가느라 안 그려져
                //     **처음 만지는 사람에게 되돌리기가 아예 없었다.**
                //     ⚠️ 폭을 게임이 차지한 만큼만 준다 — 안 그러면 오른쪽 안내판을 덮는다.
                //     세로일 때는 안내판이 **아래 띠**라 패드는 그 위 자리만 쓴다 (09-12).
                if (Touchy && !_replay)
                {
                    if (Tall(w, h)) Pad(w, h * (1f - GuideH));
                    else Pad(w * (1f - GuideW), h);
                }
                if (_volPanel) VolumeModal(w, h);
                return;
            }

            // ---- 플레이어가 보는 것 : 번호 · 남은 홈 · 조작. 그게 전부다 ----
            // 🔴 판 이름은 없앴다. 생성기가 붙인 이름은 열네 판이 전부 "홈 하나"라
            //    되풀이되는 이름은 없느니만 못했다 (08-31 사장님: "너무 다 짜쳐서").
            //  🔴 상단 띠 — **나가는 길**이 여기 있다. 이기든 지든 늘 보인다.
            //  ⚠️ 여기에 def.id 를 _sBig 로 또 찍고 있었다 — 띠 이름표 뒤로 **흐린 판 이름이 겹쳐** 보였다 (09-13 화면).
            //     띠를 먼저 그려 그 **실제 높이**를 받고, 점·걸음 줄을 그 아래에 둔다.
            //     42·66 고정 픽셀이었을 때 폰(띠가 커진다)에서 "문 0/2" 가 ? 단추와 겹쳤다.
            float barY;
            {
                string stars = "";
                int got = Stars(def);
                for (int k = 0; k < 3; k++) stars += k < got ? "★" : "☆";
                barY = TopBar(w, h, def.id + "   " + stars, () => { _menu = true; _volPanel = false; }, true, true);
            }

            // 남은 홈을 점으로 — 숫자보다 한눈에 들어온다
            string dots = "";
            for (int i = 0; i < need; i++) dots += (i < filled ? "●" : "○") + " ";
            // 🔴 문이 여럿이면 남은 문도 보여준다
            if (_L.Doors.Count > 1)
            {
                int openCount = 0;
                for (int i = 0; i < _L.Doors.Count; i++) if ((_st.Dm & (1 << i)) != 0) openCount++;
                dots += "     문 " + openCount + "/" + _L.Doors.Count;
            }
            GUI.Label(new Rect(0, barY + 4f * _uiScale, w, 22f * _uiScale), dots, _sMid);

            //  ⛔ "몸 N칸 · 홈 M칸" 줄은 일반 판에서 뺐다 (09-12 사장님: "튜토일 때만 알려주면 됨").
            //     튜토리얼은 위에서 빠져나가 오른쪽 안내판의 Coach() 가 같은 걸 말한다.

            // 🔴 걸음 수와 커트라인은 늘 보여야 한다. 다 끝나고 알려주면 늦다.
            if (_L.Star >= 0 || def.cut > 0)
            {
                string s = (_st.Sc != 0 ? "★ " : "☆ ") + _steps + "걸음";
                if (def.cut > 0) s += "  ·  커트 " + def.cut;
                GUI.Label(new Rect(0, barY + 27f * _uiScale, w, 20f * _uiScale), s, _sSmall);
            }

            // 🔴 지도 대신 **지나온 길**을 보여준다. 어디서 갈렸는지가 남는다.
            //  🔴 몸 길이 줄(88)과 겹치지 않게 내렸다
            if (_dev && _trail.Count > 1)
                GUI.Label(new Rect(0, 110, w, 20),
                    string.Join(" › ", _trail.GetRange(Mathf.Max(0, _trail.Count - 6), Mathf.Min(6, _trail.Count))),
                    _sSmall);

            if (_won)
            {
                GUI.Label(new Rect(0, h - 40, w, 24), "문이 열렸다", _sMid);
            }
            else
            {
                // 🔴 오래 막혔을 때만 도움을 내민다. 처음부터 떠 있으면 아무도 안 푼다.
                if (Stuck)
                {
                    bool dead = _lostSet.IsLost(_st);
                    //  ⚠️ 유니티 기본 회색 단추였다 — 조용한 단추(Btn)로 (09-12).
                    float hw = 210f * _uiScale, hh = 30f * _uiScale;
                    var hr = new Rect(w - hw - 12f, h - hh - 12f, hw, hh);
                    // 🔴 단계마다 다른 도움. 필요한 만큼만 열어보게 한다.
                    int nx = NextHint();
                    string label = dead ? "되돌리세요 (Z)"
                        : nx == 1 ? "어느 홈부터? (H)"
                        : nx == 2 ? "먹으면 안 되는 조각은? (H)"
                        : nx == 3 ? "다음 한 걸음 (H) — 별 셋 포기"
                        : null;
                    if (label != null && Btn(hr, label) && !dead) OpenHint();

                    // 민 방향을 잠깐 크게 보여준다 — 답이 아니라 **다음 한 수**다
                    if (Time.time - _nudgeShow < 2.2f)
                    {
                        string face = _lostSet.IsLost(_st) ? "여긴 이미 진 자리다 — 되돌리세요"
                            : _nudgeDir == SnakeEngine.Dir.Drop ? "여기서 몸을 놓으세요"
                            : "이쪽으로   " + "↑↓←→"[(int)_nudgeDir];
                        var ns2 = new GUIStyle(_sBig) { fontSize = Mathf.RoundToInt(26 * _uiScale) };
                        ns2.normal.textColor = StarLit;
                        GUI.Label(new Rect(0, h * 0.5f - 24f, w, 44f), face, ns2);
                    }
                    string note = _tooBig ? "이 판은 너무 커서 다음 걸음은 못 알려드립니다"
                                : _hint == 1 ? "먼저 채울 홈이 깜빡입니다"
                                : _hint == 2 ? "잿빛 조각은 먹으면 안 됩니다"
                                : _nudges > 0 ? "다음 걸음을 " + _nudges + "번 봤습니다 — 이 판은 별 셋을 못 받습니다"
                                : null;
                    if (note != null) GUI.Label(new Rect(0, h - 62, w, 20), note, _sSmall);
                }

                if (!Touchy)
                    GUI.Label(new Rect(0, h - 34, w, 22),
                        _L.Pads.Count > 0
                          ? "← ↑ ↓ →      Space  몸 놓기      Z  되돌리기      R  처음부터"
                          : "← ↑ ↓ →      Z  되돌리기      R  처음부터", _sSmall);

                // ⛔ 판 아래 안내 문장(Coach)은 일반 판에서 뺐다 (09-12 사장님: "튜토일 때만").
                //    튜토리얼에서는 오른쪽 안내판(Guide)이 그대로 Coach() 를 보여준다.
            }

            if (_replay)
                GUI.Label(new Rect(0, 70, w, 22),
                    $"정답 재생 중  {_replayAt}/{def.sol.Length}   (아무 키나 누르면 멈춤)", _sMid);

            if (Touchy && !_replay) Pad(w, h);

            //  🔴 팝업은 **맨 마지막**에 그린다. 앞에 두면 십자·힌트 단추가 그 위에 얹힌다.
            if (_volPanel) VolumeModal(w, h);

            // ---- 개발자가 보는 것 : F1 ----
            if (!_dev) { if (DevAllowed) GUI.Label(new Rect(w - 60, h - 22, 52, 18), "F1", _sSmall); return; }

            GUI.Box(new Rect(12, 12, 620, 82), GUIContent.none);
            GUILayout.BeginArea(new Rect(22, 18, 600, 70));
            bool onCore = _L.Core < 0 || _st.Head == _L.Core;
            GUILayout.Label($"{_index + 1}/{_set.levels.Length}  {def.id}  {_L.W}x{_L.H}" +
                            (Cleared(def.id) ? "  (깬 판)" : "") +
                            $"   ·   홈 {filled}/{need}   ·   길이 {_st.Length}/{need}" +
                            (_L.Core >= 0 ? (onCore ? "   ·   머리 심에 있음" : "") : ""));
            GUILayout.Label($"최단 {def.best}걸음 · 이미 진 상태 {def.lost:0.0}% · 헤맴 {def.wander}" +
                            (_lostSet != null && _lostSet.Ready ? $" · 상태 {_lostSet.States}" : " · (못 잼)") +
                            $"      N/P 판   K 손맛   F3 정답재생   G 중력 {(_gravity ? "켬" : "끔")}");
            GUILayout.Label("기록: " + SnakeLog.Summary() +
                            (_run != null ? $"   ·   지금 판 {Time.time - _runStart:0}초" +
                                            (_run.lostSeconds > 0.5f ? $" (이미 진 상태 {_run.lostSeconds:0}초)" : "") : ""));
            GUILayout.EndArea();

            if (!_showPanel) return;
            var r = new Rect(Screen.width - 322, 12, 310, 212);
            GUI.Box(r, GUIContent.none);
            GUILayout.BeginArea(new Rect(r.x + 12, r.y + 10, r.width - 24, r.height - 20));
            GUILayout.Label("손맛 조절 — K로 닫기");
            K.stepTime = Row("한 칸 시간", K.stepTime, 0.03f, 0.30f);
            K.follow = Row("따라붙는 정도", K.follow, 0f, 1f);
            K.segmentSize = Row("마디 크기", K.segmentSize, 0.5f, 1f);
            K.growPop = Row("길어질 때 부풂", K.growPop, 0f, 0.6f);
            K.fallTime = Row("떨어지는 시간", K.fallTime, 0.03f, 0.50f);
            K.landSquash = Row("착지 눌림", K.landSquash, 0f, 1f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("저장")) { PlayerPrefs.SetString(KnobKey, JsonUtility.ToJson(K)); PlayerPrefs.Save(); }
            if (GUILayout.Button("기본값")) { PlayerPrefs.DeleteKey(KnobKey); K = new Knobs(); }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static float Row(string label, float v, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110));
            v = GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(120));
            GUILayout.Label(v.ToString("0.00"), GUILayout.Width(42));
            GUILayout.EndHorizontal();
            return v;
        }
    }
}
