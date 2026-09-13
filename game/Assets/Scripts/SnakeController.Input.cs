using System.Collections.Generic;
using UnityEngine;

namespace SlimeEscape
{
    //  🔴 SnakeController 의 한 조각 (partial). 필드 · Awake · 판 진행은 SnakeController.cs.
    //     입력 — Update · 걸음 · 되돌리기 · 손가락
    //     2026-09-12 에 4,400줄짜리 한 파일을 구획대로 나눴다 — 내용은 한 글자도 안 바꿨다.
    public partial class SnakeController
    {
        // ---------------- 입력 ----------------
        void Update()
        {
            //  🔴 판을 다 가리는 화면(타이틀 · 설정 · 판 고르기)에서는 **판 카메라를 끈다** (09-12, 모바일 손질).
            //     가림막이 불투명해 안 보일 뿐 매 프레임 판 전체를 그리고 있었다 — 폰에서 가만히 있어도 배터리를 쓴다.
            //     세 화면 모두 IMGUI 로 화면 전체를 불투명하게 먼저 칠하므로 안 지워진 자리가 안 생긴다.
            //     돌아올 때는 다음 프레임 Update 가 켜고 나서 그리므로 빈 프레임이 안 끼어야 한다 — ⚠️ 폰에서 눈으로 확인할 것.
            //  ⚠️ 시범(_demo)의 _cam 은 **안내판 카메라**다 (SetupDemo) — 건드리면 안 된다.
            //  ⚠️ 안내 화면(_intro)은 판 위에 겹쳐 그리는지 확인을 안 해서 넣지 않았다.
            //     소리는 그대로 난다 — AudioListener 는 카메라와 다른 부품이다.
            if (!_demo && _cam != null) _cam.enabled = !(_title || _settings || _menu);

            //  타이틀·설정에서는 판 입력을 안 받는다. 화살표를 눌러도 슬라임이 움직이면 안 된다.
            if (!_demo && (_title || _settings))
            {
                //  🔴 판 1-0 은 안내판 자리를 비워두려고 카메라 폭을 줄여 놓는다.
                //     타이틀에서는 그 자리를 아무도 안 비춰서 쓰레기 화소가 뜬다
                //     (09-04 밤에 안내판에서 겪은 그것). 화면을 다 쓰게 되돌린다.
                //  세로 폰 튜토리얼은 폭이 아니라 **높이**를 줄여 둔다 (09-12) — 둘 다 본다.
                if (_cam != null && (_cam.rect.width < 0.999f || _cam.rect.height < 0.999f))
                    _cam.rect = new Rect(0f, 0f, 1f, 1f);
                Animate(Time.deltaTime);
                return;
            }

            //  🔴 시범은 사람 입력을 안 받는다. 정해진 방향을 스스로 누르고,
            //     다 하면 처음으로 돌아간다. 그리는 것은 본판과 똑같이 돈다.
            if (_demo)
            {
                if (_L == null) return;
                TickDemo();
                Animate(Time.deltaTime);
                AimCamera(false);
                return;
            }
            // 🔴 이미 진 상태에서 보낸 시간을 잰다. 화면엔 아무 표시도 안 한다.
            if (_run != null && !_won && _lostSet != null && _lostSet.Ready && _lostSet.IsLost(_st))
                _run.lostSeconds += Time.deltaTime;

            if (_intro)
            {
                // 🔴 anyKeyDown 은 **마우스 버튼도 포함한다.**
                //    그대로 두면 버튼을 클릭하는 순간 Update가 먼저 안내 화면을 닫아버려서
                //    OnGUI가 화면을 안 그리고, 버튼이 클릭을 받을 기회가 없다.
                //    (08-30: "초기화 안 되는데" — 원인이 이것이었다)
                //    그래서 **키보드만** 여기서 닫는다. 마우스/터치는 OnGUI가 처리한다.
                bool mouse = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)
                          || Input.GetMouseButtonDown(2) || Input.touchCount > 0;
                if (Input.anyKeyDown && !mouse) CloseIntro();
                return;
            }

            // 🔴 결과 화면 — 별이 다 나온 뒤에 눌러야 넘어간다. 안 그러면 별을 못 본다.
            if (_result)
            {
                // 별이 나오는 박자에 맞춰 소리를 낸다
                float el = Time.time - _resultAt;
                while (_shown < 3 && el >= StarLead + _shown * StarBeat)
                { if (_shown < _resultStars) Ding(_shown); _shown++; }

                //  🔴 이제 결과 화면에 **단추가 있다.** 마우스·터치로도 넘기면
                //     단추를 누르는 순간 Update 가 먼저 넘겨버려서, OnGUI 가 화면을
                //     안 그리고 **단추가 클릭을 받을 기회가 없다.**
                //     (안내 화면에서 08-30 에 똑같이 겪었다 — "초기화가 안 되는데")
                //     그래서 여기서는 **키보드만** 받는다. 손가락은 단추가 받는다.
                bool mouse = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)
                          || Input.GetMouseButtonDown(2) || Input.touchCount > 0;
                if (el > StarLead + 3 * StarBeat && Input.anyKeyDown && !mouse)
                { AdvanceNow(); return; }
                Animate(Time.deltaTime);
                return;
            }

            // 목록 화면에서도 뒤의 판은 계속 숨쉰다. 입력만 OnGUI가 받는다.
            if (_menu)
            {
                //  🔴 목록에서도 개발자 키(F1 · N · P)는 먹게 둔다.
                //     마우스 없이 판에 들어갈 수 있어야 자동 확인이 돌아간다 (09-02).
                //  🔴 유니티 에디터가 F1 을 먹어서 게임까지 안 온다 (09-02).
                //     T 를 같은 뜻으로 둔다 — 혼자 확인할 때 필요하다.
                if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.T))
                { _dev = !_dev; if (!_dev) _showPanel = false; }
                //  🔴 판 고르기에서 **나가는 길**이 없었다. Esc 로 타이틀까지 돌아간다.
                if (Input.GetKeyDown(KeyCode.Escape)) { _menu = false; _title = true; return; }
                if (_dev && Input.GetKeyDown(KeyCode.N)) { _menu = false; _allDone = false; Load(_index + 1); return; }
                if (_dev && Input.GetKeyDown(KeyCode.P)) { _menu = false; _allDone = false; Load(_index - 1); return; }
                //  굴 넘기기 — 판 고르기는 굴 하나씩 보여준다 (09-12)
                if (Input.GetKeyDown(KeyCode.LeftArrow)  || Input.GetKeyDown(KeyCode.A)) MenuChapter(-1);
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) MenuChapter(+1);
                Animate(Time.deltaTime);
                return;
            }

            //  🔴 소리 팝업이 열려 있으면 판은 **멈춘 것처럼** 둔다.
            //     연출은 계속 돌지만 걸음·되돌리기는 안 받는다.
            if (_volPanel)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) _volPanel = false;
                Animate(Time.deltaTime);
                return;
            }

            // 🔴 H — 한 걸음 밀기. 오래 막혔을 때만 열린다.
            if (Input.GetKeyDown(KeyCode.H) && Stuck) { OpenHint(); return; }

            // Esc 로 목록으로 — 언제든 나갈 데가 있어야 갇힌 느낌이 안 든다
            if (Input.GetKeyDown(KeyCode.Escape)) { _menu = true; return; }

            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.T))
            { _dev = !_dev; if (!_dev) _showPanel = false; }

            if (_dev && Input.GetKeyDown(KeyCode.F3)) { StartReplay(); return; }
            if (_replay)
            {
                if (Input.anyKeyDown && !Input.GetKeyDown(KeyCode.F3)) { _replay = false; return; }
                if (Time.time >= _replayNext) StepReplay();
                return;                                  // 재생 중엔 사람 입력을 안 받는다
            }
            if (_dev && Input.GetKeyDown(KeyCode.K)) _showPanel = !_showPanel;
            // 검증용 — 테스터를 바꿀 때 진행을 지운다
            if (_dev && Input.GetKeyDown(KeyCode.F2))
            {
                PlayerPrefs.DeleteKey(ProgressKey); PlayerPrefs.Save();
                _recs.Clear(); SaveProgress(); Load(0); return;
            }
            if (_dev && Input.GetKeyDown(KeyCode.G))
            {
                _gravity = !_gravity;
                Load(_index);
                return;
            }
            // 열렸으면 잠깐 두고 다음 방으로 — 손이 멈추지 않게
            // 🔴 출구가 있는 맵에서는 문을 채워도 **판이 안 끝난다.**
            //    문벽이 열릴 뿐이고, 넘어가려면 출구까지 걸어가야 한다.
            // 🔴 홈을 채우면 끝. 문이 스르륵 열리는 걸 보여주고 결과 화면으로 넘어간다.
            //    (출구까지 걸어가게 했더니 지루하기만 했다 — 08-30 사장님)
            // 🔴 다 빨려든 다음에 결과 화면. 연출이 잘리면 "해냈다"가 안 남는다.
            // 🔴 연출이 멈춰도 판은 **반드시** 넘어간다. 연출 하나 때문에 못 깨면 안 된다.
            if (_won && Time.time > _wonAt + WinDelay
                && (_blobs.Count == 0 || Time.time > _wonAt + WinDelay + 2f))
            { ClearAndAdvance(); return; }

            if (Input.GetKeyDown(KeyCode.R))
            {
                if (_allDone) { _allDone = false; _trail.Clear(); Load(0); return; }
                if (_run != null) _run.restart++;
                Restart(); return;
            }
            if (_dev && Input.GetKeyDown(KeyCode.N)) { Load(_index + 1); return; }
            if (_dev && Input.GetKeyDown(KeyCode.P)) { Load(_index - 1); return; }
            if (Input.GetKeyDown(KeyCode.Z)) { Undo(); return; }

            // 🔴 **떨어지는 동안에는 방향 입력을 안 받는다** (09-05 사장님).
            //    받으면 낙하 연출이 끊기고 다음 걸음이 바로 시작돼 **"팍" 하고 떨어진다.**
            //    떨어지는 건 내가 하는 게 아니라 **일어나는 일**이다 — 끝날 때까지 기다린다.
            //    ⚠️ 되돌리기(Z)·다시하기(R)·메뉴는 위에서 이미 받았다. 그것까지 막으면 답답하다.
            if (!_landed) { Animate(Time.deltaTime); return; }

            if (Swipe(out var sdir)) { Step(sdir); return; }

            // 🔴 받침대에 몸을 놓는다 — 스페이스. 받침대가 있는 판에서만 뜻이 있다.
            if (_L.Pads.Count > 0 &&
                (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.X)))
            { Step(SnakeEngine.Dir.Drop); return; }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Step(SnakeEngine.Dir.Up);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Step(SnakeEngine.Dir.Down);
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Step(SnakeEngine.Dir.Left);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Step(SnakeEngine.Dir.Right);

            Animate(Time.deltaTime);
        }

        void Step(SnakeEngine.Dir dir)
        {
            if (_won) return;                 // 열렸으면 그대로 둔다
            int before = _st.Length;
            int fromHead = _st.Head;
            if (!SnakeEngine.Step(_L, _st, dir, out var ns))
            {
                if (_run != null) _run.blocked++;      // 막힌 입력도 답답함의 신호다
                return;
            }
            if (_run != null) _run.moves++;
            _steps++;
            var dd = SnakeEngine.Delta[(int)dir];
            if (dd.dx != 0 || dd.dy != 0) _lookDir = new Vector2(dd.dx, -dd.dy);
            //  🔴 가는 쪽으로 늘어난다. 옆으로 가면 옆으로, 위아래면 위아래로.
            if (dd.dx != 0)      Jig( JigStep, -JigStep);
            else if (dd.dy != 0) Jig(-JigStep,  JigStep);
            _undo.Push(_st);
            int hadDm = _st.Dm;
            _st = ns;

            // 🔴 **어느 문이 방금 열렸나**를 Dm 이 바뀐 걸로 본다.
            //    WonDoor 로 물으면 안 된다 — 문이 채워지는 순간 엔진이 몸을 핵 하나로 줄여버려서
            //    "몸이 문에 맞나"는 이미 거짓이 되어 있다. (08-30: 판이 아예 안 깨지고 있었다)
            int opened = -1;
            for (int i = 0; i < _L.Doors.Count; i++)
                if ((hadDm & (1 << i)) == 0 && (_st.Dm & (1 << i)) != 0) { opened = i; break; }
            if (opened >= 0) StartSuck(opened);

            if (!_won && SnakeEngine.IsWin(_L, _st))
            {
                _wonBy = opened;
                _won = true; _wonAt = Time.time;
                // 🔴 시간은 "열린 순간"에 찍는다. 다음 판으로 넘어가는 1초가 섞이면 안 된다.
                if (_run != null) { _run.cleared = true; _run.seconds = Time.time - _runStart; }
            }

            if (_st.Length > before)
            {
                _pop = K.growPop;
                _segPos.Add(_segPos[_segPos.Count - 1]);               // 새 꼬리는 옛 꼬리 자리에서 시작
            }

            // 🔴 몇 칸을 떨어졌나. Settle은 몸 전체를 같은 칸수만큼 아래로 옮기므로,
            //    머리가 걸음만 했을 때의 줄보다 얼마나 아래에 있는지가 곧 낙하 칸수다.
            int steppedRow = fromHead / _L.W + SnakeEngine.Delta[(int)dir].dy;
            _fall = Mathf.Max(0, _st.Head / _L.W - steppedRow);
            // 🔴 **포탈을 지났으면 낙하 칸수를 못 센다** (09-05 사장님: "몸이 휘이익").
            //    위 셈은 "머리가 걸음만 했을 때의 줄보다 얼마나 아래인가" 인데,
            //    포탈은 머리를 판 반대편으로 보내버려서 7 같은 엉뚱한 값이 나온다.
            //    그러면 _mid[i] = 몸[i] - 7*가로 가 **음수 칸**이 되어 화면 밖으로 날아간다.
            //    건너뛴 걸음은 낙하 연출을 안 쓴다 — 어차피 뚝 건너가는 걸음이다.
            {
                var dl = SnakeEngine.Delta[(int)dir];
                int stepCell = fromHead + dl.dy * _L.W + dl.dx;
                if (_L.Portal.Count > 0 && stepCell >= 0 && stepCell < _L.W * _L.H
                    && _L.Portal.TryGetValue(stepCell, out var hop2)
                    && (hadDm & (1 << hop2.Door)) != 0)
                    //  🔴 **나온 자리에서부터 센다** (09-05 사장님: "떨어지는 느낌 없이 바로 내려와짐").
                    //     처음엔 0 으로 껐는데, 그러면 허공으로 나와 진짜 떨어질 때도
                    //     연출이 없어서 **뚝 내려앉는다.** 나온 칸은 알고 있으니 거기서부터 세면 된다.
                    _fall = Mathf.Max(0, _st.Head / _L.W - hop2.To / _L.W);
            }
            //  🔴 미끄러지는 동안에도 **조작을 막는다** (09-05 사장님: "아무 조작이 안 되는 느낌").
            //     낙하와 같은 장치를 쓴다 — 끝나기 전에는 _landed 가 false 다.
            _slid = Mathf.Max(1, _st.Slid);
            _landed = _fall == 0 && _slid <= 1;

            // 떨어지기 직전 자리 = 최종 자리에서 낙하한 만큼 도로 올린 것
            _mid.Clear();
            //  🔴 판 밖 칸은 안 만든다 — 음수가 들어오면 화면 밖 좌표가 나온다
            for (int i = 0; i < _st.Length; i++)
                _mid.Add(CellPos(Mathf.Max(0, _st.Body[i] - _fall * _L.W)));

            // 🔴 이번 걸음의 '출발선'을 지금 그려지는 자리로 잡는다.
            //    (걸음이 끝나기 전에 또 누르면 튀지 않게)
            _from.Clear();
            _from.AddRange(_segPos);
            //  🔴 상자도 '출발선'을 지금 그려지는 자리로 잡는다 — 걸음이 끝나기 전에
            //     또 누르면 상자가 튄다 (슬라임과 같은 이유다)
            _boxFrom.Clear();
            _boxFrom.AddRange(_boxDrawn);
            //  🔴 포탈을 지났나 — 머리가 이웃 칸이 아닌 데로 갔으면 지난 것이다
            if (_L.Portal.Count > 0 && _undo.Count > 0)
            {
                int was = _undo.Peek().Head, now = _st.Head;
                int dx1 = Mathf.Abs(_L.X(was) - _L.X(now)), dy1 = Mathf.Abs(_L.Y(was) - _L.Y(now));
                if (dx1 + dy1 > 1 && _L.Portal.ContainsKey(now)) { _warpIn = was; _warpOut = now; _warpT = 0f; }
            }
            _stepT = 0f;
            SyncViews();
        }

        void Undo()
        {
            if (_undo.Count == 0) return;
            if (_run != null) _run.undo++;
            _steps = Mathf.Max(0, _steps - 1);   // 되돌리면 걸음도 되돌린다
            _st = _undo.Pop();
            _won = false;
            while (_segPos.Count > _st.Length) _segPos.RemoveAt(_segPos.Count - 1);
            while (_segPos.Count < _st.Length) _segPos.Add(_segPos[_segPos.Count - 1]);
            for (int i = 0; i < _st.Length; i++) _segPos[i] = CellPos(_st.Body[i]);   // 되돌리기는 딱 붙인다
            //  상자도 딱 붙인다 — 되돌리기는 연출 없이 즉시다
            _boxDrawn.Clear(); _boxFrom.Clear(); _boxSq.Clear();
            _warpIn = _warpOut = -1; _warpT = 9f;   //  건너뛰기 표시도 지운다
            _from.Clear(); _from.AddRange(_segPos);
            _mid.Clear(); _mid.AddRange(_segPos);
            _stepT = 999f;
            _pop = 0f;
            _fall = 0; _slid = 1; _land = 0f; _landed = true;
            SyncViews();
        }

        /// 손가락을 그은 방향. 화면 짧은 쪽의 6%를 넘겨야 한 걸음으로 친다.
        bool Swipe(out SnakeEngine.Dir dir)
        {
            dir = SnakeEngine.Dir.Up;
            if (Input.touchCount == 0) { _swiping = false; return false; }
            var tch = Input.GetTouch(0);
            if (tch.phase == TouchPhase.Began) { _swipeFrom = tch.position; _swiping = true; return false; }
            if (!_swiping || (tch.phase != TouchPhase.Ended && tch.phase != TouchPhase.Moved)) return false;

            var d = (Vector2)tch.position - _swipeFrom;
            float need = Mathf.Min(Screen.width, Screen.height) * 0.06f;
            if (d.magnitude < need) return false;

            _swipeFrom = tch.position;                 // 이어서 그으면 계속 간다
            if (Mathf.Abs(d.x) > Mathf.Abs(d.y))
                dir = d.x > 0 ? SnakeEngine.Dir.Right : SnakeEngine.Dir.Left;
            else
                dir = d.y > 0 ? SnakeEngine.Dir.Up : SnakeEngine.Dir.Down;   // 화면 y는 위가 +
            return true;
        }

        /// 🔴 상자를 **무겁게** 옮긴다. 슬라임의 Ease(감속)를 쓰면 안 된다 —
        /// 그건 가벼운 것의 곡선이다. 상자는 천천히 떠서 천천히 선다(smoothstep).
        /// 떨어질 때만 점점 빨라진다 — 무거운 건 더 그래야 한다.
        void AnimateBoxes()
        {
            int n = _st == null ? 0 : _st.Bx.Count;
            while (_boxDrawn.Count < n) _boxDrawn.Add(CellPos(_st.Bx[_boxDrawn.Count]));
            while (_boxDrawn.Count > n) _boxDrawn.RemoveAt(_boxDrawn.Count - 1);
            while (_boxFrom.Count < n) _boxFrom.Add(_boxDrawn[_boxFrom.Count]);
            while (_boxFrom.Count > n) _boxFrom.RemoveAt(_boxFrom.Count - 1);
            while (_boxSq.Count < n) _boxSq.Add(0f);
            while (_boxSq.Count > n) _boxSq.RemoveAt(_boxSq.Count - 1);

            for (int i = 0; i < n; i++)
            {
                Vector2 end = CellPos(_st.Bx[i]);
                Vector2 from = _boxFrom[i];
                Vector2 d = end - from;
                if (d.sqrMagnitude < 1e-6f) { _boxDrawn[i] = end; _boxSq[i] = 0f; continue; }

                float raw = (_stepT - BoxLag) / BoxSpan;
                float tt = Mathf.Clamp01(raw);
                bool falling = Mathf.Abs(d.y) > Mathf.Abs(d.x);
                //  낙하는 가속(t²), 밀림은 **버티다 밀려 서서히 선다**(smoothstep)
                float u = falling ? tt * tt : tt * tt * (3f - 2f * tt);
                Vector2 p = Vector2.Lerp(from, end, u);

                if (!falling)
                {
                    //  🔴 다 가고 나서 한 번 떤다 — 무거운 게 놓이는 느낌은 여기서 난다
                    float over = raw - 1f;
                    if (over > 0f)
                        p.x += Mathf.Sin(over * 30f) * BoxShake * Mathf.Exp(-over * 8f) * Mathf.Sign(d.x);
                    //  미는 동안만 옆으로 퍼진다 (가운데서 제일 크고 양끝은 0)
                    _boxSq[i] = BoxSquash * Mathf.Sin(tt * Mathf.PI);
                }
                else _boxSq[i] = 0f;

                _boxDrawn[i] = p;
            }
        }
    }
}
