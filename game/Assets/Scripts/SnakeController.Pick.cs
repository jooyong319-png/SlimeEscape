using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SlimeEscape
{
    /// <summary>
    /// 🔴 **판 걸러내기** (09-20 사장님: *"최대한 많은 스테이지를 만들어서 내가 직접 해보면서
    /// 걸러내는 작업이 필요할것같아"*).
    ///
    /// 못에 쌓아 둔 판이 1,575 개다. 다 해보는 건 불가능하다 — 판당 2분이면 50시간이 넘는다.
    /// 그래서 **많이 만드는 것보다 빨리 거르는 것**이 일이 된다.
    /// 판을 하다 `1`·`2`·`3` 을 누르면 그 자리에서 표가 찍히고 **바로 다음 판**으로 넘어간다.
    /// 몇 수 두어 보고 "아니다" 싶으면 `3` 을 치면 되므로, 한 자리에서 백 판을 돌 수 있다.
    ///
    /// ⚠️ **개발자 모드에서만** 먹는다 (F1 · T). 다듬는 도구지 게임 기능이 아니다.
    ///
    /// 🔴 남는 곳이 둘이다:
    ///    - 브라우저 저장소(PlayerPrefs) — 웹에서도 쌓인다.
    ///    - `wiki/picks.csv` — **에디터에서만.** 제가 이 파일을 읽어 정리하려고 둔다.
    ///      (SnakeLog 가 기록을 잃은 적이 있다 — 09-16 에 41판치를 통째로 날렸다. 같은 실수를 안 한다)
    /// </summary>
    public partial class SnakeController
    {
        /// 판 이름 → 1 좋음 · 2 보통 · 3 버림
        readonly Dictionary<string, int> _picks = new Dictionary<string, int>();
        bool _picksRead;

        const string PickKey = "picks_csv";
        static readonly string[] PickName = { "", "좋음", "보통", "버림" };
        static readonly Color[] PickCol = {
            Color.white,
            new Color32(0x9b, 0xe8, 0xad, 0xff),   // 좋음 — 슬라임 초록
            new Color32(0xcc, 0xcf, 0xd4, 0xff),   // 보통 — 무채색
            new Color32(0x8a, 0x8f, 0x97, 0xff),   // 버림 — 가라앉은 회색
        };

        /// 쌓인 표를 한 번만 읽어 들인다
        void ReadPicks()
        {
            if (_picksRead) return;
            _picksRead = true;
            try
            {
                foreach (var line in PlayerPrefs.GetString(PickKey, "").Split('\n'))
                {
                    var c = line.Split(',');
                    if (c.Length >= 2 && int.TryParse(c[1], out int v) && v >= 1 && v <= 3)
                        _picks[c[0]] = v;
                }
            }
            catch { /* 읽기 실패는 조용히 넘긴다 — 표 하나 때문에 판이 안 열리면 안 된다 */ }
        }

        /// 지금 판에 표를 찍고 **바로 다음 판으로**
        void Pick(int score)
        {
            if (Def == null) return;
            ReadPicks();
            _picks[Def.id] = score;
            SavePicks();
            Load(_index + 1);
        }

        void SavePicks()
        {
            var sb = new StringBuilder();
            foreach (var kv in _picks) sb.Append(kv.Key).Append(',').Append(kv.Value).Append('\n');
            try { PlayerPrefs.SetString(PickKey, sb.ToString()); PlayerPrefs.Save(); }
            catch { }
#if UNITY_EDITOR
            //  🔴 사람이 읽을 표로도 남긴다. 사장님이 쭉 찍고 나면 이 파일만 보면 된다.
            try
            {
                //  ⚠️ 기믹은 따로 안 적는다 — `SnakeLevelJson` 에 `gim` **선언이 없어서** 못 읽는다
                //     (판 자료에는 있지만 JsonUtility 가 모르는 칸을 조용히 버린다).
                //     걸러내기 판은 이름이 `ice-3` 처럼 기믹을 달고 있으니 그걸로 충분하다.
                var csv = new StringBuilder("판,표,걸음\n");
                foreach (var lv in _set.levels)
                    if (_picks.TryGetValue(lv.id, out int v))
                        csv.Append(lv.id).Append(',').Append(PickName[v]).Append(',')
                           .Append(lv.best).Append('\n');
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(Application.dataPath, "../../wiki/picks.csv"),
                    csv.ToString(), Encoding.UTF8);
            }
            catch (System.Exception e) { Debug.LogWarning("[표] 파일로 못 남김 — " + e.Message); }
#endif
        }

        /// <summary>
        /// 지금 판의 표와 누른 법을 아주 작게 보여준다.
        /// ⚠️ 판을 가리면 안 되므로 **화면 맨 아래 왼쪽** 구석에만 둔다 — 조작 안내 줄 위다.
        /// </summary>
        void PickHud(float w, float h)
        {
            if (!_dev || _menu || _title || _settings || _intro || Def == null) return;
            ReadPicks();

            var st = new GUIStyle(_sSmall)
            {
                fontSize = Px(Mathf.Clamp(h * 0.024f, 11f, 16f)),
                alignment = TextAnchor.MiddleLeft,
            };
            float y = h - Mathf.Clamp(h * 0.075f, 34f, 56f);

            _picks.TryGetValue(Def.id, out int got);
            st.normal.textColor = got > 0 ? PickCol[got] : new Color(1, 1, 1, 0.34f);
            string mark = got > 0 ? "● " + PickName[got] : "○ 아직";
            GUI.Label(new Rect(14f, y, w * 0.4f, 22f), mark + "    1 좋음 · 2 보통 · 3 버림", st);

            //  몇 판을 봤는지 — 끝이 안 보이면 지친다
            int done = 0;
            foreach (var kv in _picks) if (kv.Value > 0) done++;
            var st2 = new GUIStyle(st) { alignment = TextAnchor.MiddleRight };
            st2.normal.textColor = new Color(1, 1, 1, 0.30f);
            GUI.Label(new Rect(w * 0.55f, y, w - w * 0.55f - 14f, 22f),
                      done + " / " + _set.levels.Length + " 판 봄", st2);
        }
    }
}
