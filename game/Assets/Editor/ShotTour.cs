using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SlimeEscape.EditorTools
{
    /// <summary>
    /// 🔴 **화면 차례로 찍기** — Play 중에 화면을 바꿔 가며 ScreenCapture 로 찍는다 (09-16 다듬기 루프).
    ///
    /// 게임 화면이 IMGUI 라 카메라 캡처로는 글자·단추가 안 찍힌다. 그래서 Play 에서 게임 창을 찍는다.
    /// 유니티 MCP 로 부를 때마다 긴 코드를 보내지 않으려고 여기에 둔다 — 한 줄로 부른다:
    ///
    ///   SlimeEscape.EditorTools.ShotTour.Run("title,settings,menu:1,level:7,result:2,intro",
    ///                                        "960x600,844x390", @"C:\...\shots\", "s4");
    ///
    /// 화면 이름 뒤 `@초` 를 붙이면 바꾼 뒤 그만큼만 기다렸다 찍는다 (넘김 연출 가운데를 찍을 때).
    /// 게임 창 크기는 "cc960x600" 처럼 이름으로 찾는다 — 없으면 그 크기는 건너뛰고 로그에 남긴다.
    /// 끝나면 로그에 `[SHOT] done` 을 남긴다.
    /// </summary>
    public static class ShotTour
    {
        const BindingFlags Bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static SnakeController _c;
        static readonly List<(string name, Action act, float wait)> _steps = new List<(string, Action, float)>();
        static int _at;
        static double _next;
        static string _pending, _dir;

        const string Key = "ShotTour.pending";

        /// <summary>
        /// 🔴 Play 가 꺼져 있으면 **스스로 켠다.**
        /// 전에는 켜지 않고 <see cref="Tick"/> 이 기다리기만 해서, 예약만 걸린 채 아무 일도 안 일어났다 —
        /// 09-17 에 두 번 연달아 같은 데서 막혔고, 그때마다 "사진이 왜 안 나오지"부터 다시 캤다.
        ///
        /// ⚠️ 그런데 Play 를 켜면 도메인이 다시 올라가면서 정적 예약과 update 구독이 **통째로 날아간다.**
        ///    그래서 먼저 켜고 나중에 걸어야 한다 — 예약을 세션에 적어 두고,
        ///    올라온 뒤 <see cref="Resume"/> 가 그걸 보고 다시 건다.
        ///
        /// ⚠️ **같은 화면을 두 번 적으면 그 화면이 다시 열린다.** `"level:1@151,level:1@1.2"` 처럼 쓰면
        ///    둘째 장에서 판을 새로 여는 셈이라, 판에 들어온 시각(_levelAt)이 갱신된다.
        ///    09-17 에 이것 때문에 힌트 전구가 둘째 장에서 사라졌다 — 150초를 기다려야 뜨는 것인데
        ///    타이머가 처음으로 돌아갔기 때문이다. 코드가 아니라 **찍는 방식** 탓이었다.
        ///    시간이 쌓여야 보이는 것(힌트·오래 막힘)은 **한 장만** 찍는다.
        /// </summary>
        public static void Run(string screens, string sizes, string dir, string prefix)
        {
            if (!EditorApplication.isPlaying)
            {
                SessionState.SetString(Key, string.Join("", screens, sizes, dir, prefix));
                EditorApplication.isPlaying = true;
                Debug.Log("[SHOT] Play 를 켠다 — 올라오면 다시 건다");
                return;
            }
            Arm(screens, sizes, dir, prefix);
        }

        /// Play 가 올라온 뒤 불린다. 적어 둔 예약이 있으면 다시 건다.
        [InitializeOnLoadMethod]
        static void Resume()
        {
            string s = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(s)) return;
            SessionState.EraseString(Key);
            var p = s.Split('');
            if (p.Length == 4) Arm(p[0], p[1], p[2], p[3]);
        }

        static void Arm(string screens, string sizes, string dir, string prefix)
        {
            _dir = dir;
            _steps.Clear();
            foreach (var sz in sizes.Split(','))
            {
                string s = sz.Trim();
                _steps.Add((null, () => SetSize(s), 0.8f));
                foreach (var raw in screens.Split(','))
                {
                    string key = raw.Trim(); float wait = 1.3f;
                    int atSign = key.IndexOf('@');
                    if (atSign > 0) { float.TryParse(key.Substring(atSign + 1), System.Globalization.NumberStyles.Float,
                                                     System.Globalization.CultureInfo.InvariantCulture, out wait);
                                      key = key.Substring(0, atSign); }
                    string k = key;
                    //  차례 번호를 붙인다 — 같은 화면을 두 번 찍으면(넘김 가운데 · 끝) 이름이 같아 앞 것이 덮였다
                    _steps.Add((prefix + "-" + s + "-" + _steps.Count.ToString("00") + "-" + k.Replace(':', '_'), () => Show(k), wait));
                }
            }
            _steps.Add((null, () => { Show("title"); SetSize("960x600"); }, 0.2f));
            _at = 0; _pending = null; _c = null;
            SnakeController.HideDevHint = true;          // 찍는 동안 「F1」 을 숨긴다 — 끝나면 되돌린다
            _next = EditorApplication.timeSinceStartup + 0.8;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("[SHOT] armed " + (_steps.Count) + " steps");
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < _next) return;
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) { _next = EditorApplication.timeSinceStartup + 0.5; return; }
            if (_c == null)
            {
                foreach (var o in UnityEngine.Object.FindObjectsByType<SnakeController>(FindObjectsSortMode.None))
                    if (!(bool)F("_demo").GetValue(o)) { _c = o; break; }
                if (_c == null) { _next = EditorApplication.timeSinceStartup + 0.5; return; }
            }
            if (_pending != null)
            {
                ScreenCapture.CaptureScreenshot(_dir + _pending + ".png");
                Debug.Log("[SHOT] " + _pending);
                _pending = null; _next = EditorApplication.timeSinceStartup + 0.35; return;
            }
            if (_at >= _steps.Count) { EditorApplication.update -= Tick; SnakeController.HideDevHint = false; Debug.Log("[SHOT] done"); return; }
            var st = _steps[_at++];
            try { st.act(); } catch (Exception e) { Debug.LogError("[SHOT] " + st.name + " " + e.Message); }
            _pending = st.name;
            _next = EditorApplication.timeSinceStartup + st.wait;
        }

        static FieldInfo F(string n) => typeof(SnakeController).GetField(n, Bf);
        static void Set(string n, object v) => F(n).SetValue(_c, v);

        static void Clear()
        {
            Set("_title", false); Set("_settings", false); Set("_menu", false);
            Set("_intro", false); Set("_result", false); Set("_allDone", false);
        }

        static void Show(string key)
        {
            string[] p = key.Split(':');
            int arg = p.Length > 1 && int.TryParse(p[1], out int a) ? a : -1;
            switch (p[0])
            {
                case "title":    Clear(); Set("_title", true); break;
                case "settings": Clear(); Set("_settings", true); Set("_settingsFrom", 0); break;
                case "menu":     Clear(); Set("_menu", true); if (arg > 0) Set("_menuCh", arg); break;
                case "intro":    Clear(); Set("_intro", true); break;
                case "level":
                    Clear();
                    typeof(SnakeController).GetMethod("Load", Bf).Invoke(_c, new object[] { arg < 0 ? 1 : arg });
                    break;
                case "ending":   Clear(); Set("_allDone", true); break;   // 마지막 판까지 깬 뒤
                //  (10-09) 막힘 안내 — 판을 연 뒤 150초가 지난 것으로 치고 "다음 한 수"를 띄운다 (level:N 다음에 쓴다)
                //  (10-10) 슬라임 둘 — 키보드 교체 한 번 (교체 순간 연출을 찍으려고)
                //  (10-10) 한 칸 움직이기 — 먹기 · 밀기 같은 순간 연출을 찍으려고
                case "left": case "right": case "up": case "down":
                {
                    var dd = p[0] == "left" ? SnakeEngine.Dir.Left : p[0] == "right" ? SnakeEngine.Dir.Right
                           : p[0] == "up" ? SnakeEngine.Dir.Up : SnakeEngine.Dir.Down;
                    typeof(SnakeController).GetMethod("Step", Bf).Invoke(_c, new object[] { dd });
                    break;
                }
                //  (10-11) 정답 재생 — 판을 실제로 끝까지 깨는 장면을 찍으려고 (F3 과 같다). @초 로 몇 초 뒤를 찍을지
                case "wait": break;   // 아무것도 안 하고 @초 뒤에 한 장 더 (재생 · 연출 도중을 이어 찍을 때)
                case "replay":
                    typeof(SnakeController).GetMethod("StartReplay", Bf).Invoke(_c, null);
                    break;
                case "swap":
                    typeof(SnakeController).GetMethod("Step", Bf).Invoke(_c, new object[] { SnakeEngine.Dir.Swap });
                    break;
                case "nudge":
                    Set("_levelAt", Time.time - 200f); Set("_nudgeShow", Time.time);
                    Set("_nudgeDir", SnakeEngine.Dir.Right); Set("_nudges", 1);
                    break;
                case "result":
                    Set("_result", true); Set("_resultStars", arg < 0 ? 2 : arg);
                    Set("_resultSteps", 20); Set("_resultAt", Time.time - 10f);
                    break;
                default: Debug.LogWarning("[SHOT] 모르는 화면: " + key); break;
            }
        }

        static void SetSize(string wanted)
        {
            var asm = typeof(Editor).Assembly;
            var gvsType = asm.GetType("UnityEditor.GameViewSizes");
            var inst = gvsType.BaseType.GetProperty("instance").GetValue(null, null);
            var group = gvsType.GetMethod("GetGroup").Invoke(inst, new object[] { (int)GameViewSizeGroupType.Standalone });
            var texts = (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null);
            int idx = Array.FindIndex(texts, t => t.Contains(wanted));
            if (idx < 0) { Debug.LogWarning("[SHOT] 게임 창 크기가 없다: " + wanted); return; }
            var gvType = asm.GetType("UnityEditor.GameView");
            var gv = EditorWindow.GetWindow(gvType, false, null, false);
            gvType.GetProperty("selectedSizeIndex").SetValue(gv, idx, null);
            gv.Repaint();
        }
    }
}
