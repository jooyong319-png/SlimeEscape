using UnityEngine;

namespace SlimeEscape
{
    /// <summary>
    /// 🔴 **그리기 부품.** 화면이 무엇을 그리든 상관없는 공용 조각들만 모았다 —
    /// 단색 한 장 · 글자 줄이기 · 말랑한 판 · 젖은 윤 · 단추 · 소리 아이콘 · 소리 막대 · 글자 모양.
    ///
    /// 09-17 에 <c>SnakeController.UI.cs</c> 에서 뺐다. 그 파일이 1524 줄이었는데,
    /// **성격이 다른 두 가지**가 섞여 있었다 — 여기 있는 부품들과, 그 부품으로 화면을 짜는 일
    /// (타이틀 · 판 고르기 · 설정 · 결과). 뒤섞여 있으면 "이 게임의 화면이 어떻게 생겼나"를
    /// 읽으려는 사람이 단추 그리는 법부터 지나가야 한다.
    ///
    /// 🔴 여기 있는 것들은 **이 게임의 생김새를 정한다.** 고칠 때 색 규칙을 지킨다:
    ///    초록은 슬라임만 · 목표가 제일 밝다 · 나머지는 무채색.
    ///    UI 에 형광을 두르면 "여기가 목표다"라는 뜻이 묽어진다 (09-04 에 배운 것).
    /// </summary>
    public partial class SnakeController
    {
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

        /// 한 줄 글이 maxW 를 넘으면 글자를 줄인 **복사본**을 준다. 원래 스타일은 안 건드린다.
        static GUIStyle Fit(GUIStyle st, string text, float maxW)
        {
            float tw = st.CalcSize(new GUIContent(text)).x;
            if (tw <= maxW) return st;
            return new GUIStyle(st) { fontSize = Mathf.Max(10, Mathf.FloorToInt(st.fontSize * maxW / tw)) };
        }

        /// 그림 한 조각 + 그 옆에 글 한 줄. 안내 화면의 설명 줄에 쓴다.
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

        /// <summary>
        /// 🔴 **조용한 단추** (09-04 사장님: "버튼들 때문에 좀 지저분해 보임").
        ///
        /// 전에는 홈처럼 형광 테를 둘렀는데, 이 게임에서 형광은 **"여기가 목표다"** 라는 뜻이다.
        /// 단추마다 두르니 그 뜻이 묽어지고 실제 홈과 경쟁했다 —
        /// rev.8 의 색 규칙 ③ **색이 있으면 그건 정보다** 를 UI 가 어기고 있었던 것이다.
        ///
        /// 그래서 UI 는 무채색만 쓴다. 손이 올라가면 **밝아질 뿐**이다.
        /// </summary>
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
        /// 어두운 속에 찬 만큼 **흰빛**이 들어찬다.
        /// ⚠️ 전에는 홈 테두리와 같은 **형광 초록**이었다 — 초록은 슬라임만, UI 는 무채색이다 (색 규칙, 09-16).
        ///    소리 크기는 목표가 아니니 "여기가 출구" 색을 쓰면 안 된다.
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
                Shape(new Rect(r.x, r.y, fw, r.height), new Color(1f, 1f, 1f, 0.62f), PixelSprites.Soft().texture);
            }
            //  잡는 자리 — 어디를 끌면 되는지 보여준다
            float hx = r.x + Mathf.Clamp(r.width * v, r.height * 0.5f, r.width - r.height * 0.5f);
            Shape(new Rect(hx - r.height * 0.34f, r.y - r.height * 0.34f,
                           r.height * 0.68f, r.height * 1.68f),
                  Color.white, PixelSprites.Round().texture);
            return v;
        }

        /// <summary>
        /// 글자 모양 셋을 화면 높이에 맞춰 만든다. 화면 크기가 바뀔 때만 다시 만든다.
        /// 🔴 IMGUI 는 GUIStyle 마다 글꼴을 지정해야 한다 — 하나라도 빠뜨리면 거기만 깨진다.
        /// </summary>
        void Styles()
        {
            // 🔴 휴대폰은 화면이 작다. 21px 글씨는 안 보인다 — 높이에 맞춰 키운다.
            if (_sBig != null && _styledAt == Screen.height) return;
            _styledAt = Screen.height;
            //  🔴 바닥을 0.85 → **1.0** (09-16 다듬기 4단계 · 글자 크기 통일).
            //     웹 960×600 · 가로 폰 844×390 이 둘 다 바닥(0.85)에 걸려 작은 글씨가 **11px** 였다 —
            //     판 고르기 안내 · 결과의 "판 고르기로 가려면 뒤로" · "아무 키나 누르면 시작" 이 안 읽혔다.
            //     1.0 이면 작은 13 · 중간 16 · 큰 21 — 이 게임이 처음 정한 크기 그대로다.
            _uiScale = Mathf.Clamp(Screen.height / 720f, 1.0f, 2.2f);
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
    }
}
