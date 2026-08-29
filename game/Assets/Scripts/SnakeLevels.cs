using System;
using UnityEngine;

namespace SlimeEscape
{
    /// <summary>
    /// 판 데이터 로딩.
    /// 🔴 정본은 `game/Assets/Resources/levels.json` **하나뿐이다.**
    ///    tools/stamp.js 도 바로 그 파일을 읽고 쓴다 — 사본을 만들지 말 것.
    /// </summary>
    [Serializable]
    public class SnakeLevelJson
    {
        public string id;
        public string name;
        public int best;      // 최단 걸음 수 — stamp.js가 박는다
        public string sol;    // 정답 수순 ↑↓←→ — stamp.js가 박는다
        public float lost;    // "이미 진 상태" 비율(%) — 낮으면 실수해도 회복된다 = 쉽다
        public int wander;    // 🔴 진 뒤에도 더 돌아다닐 수 있는 걸음 수 — 채택 기준(14 이하)
        public int best1, best2;     // 문마다 최단 걸음
        public string sol1, sol2;    // 문마다 정답 수순
        /// 🔴 그 문으로 나가면 가는 방. 비어 있으면 거기서 끝난다.
        public string next1, next2;
        public string[] grid;
    }

    [Serializable]
    public class SnakeLevelSetJson
    {
        public bool gravity = true;
        public SnakeLevelJson[] levels;
    }

    public static class SnakeLevels
    {
        public const string ResourcePath = "levels";

        public static SnakeLevelSetJson Load()
        {
            var ta = Resources.Load<TextAsset>(ResourcePath);
            if (ta == null) throw new Exception($"Resources/{ResourcePath}.json 을 못 찾았다");
            var set = JsonUtility.FromJson<SnakeLevelSetJson>(ta.text);
            if (set?.levels == null || set.levels.Length == 0) throw new Exception("levels.json에 판이 없다");
            return set;
        }

        public static SnakeEngine.Level ToLevel(SnakeLevelJson j, bool gravity) =>
            SnakeEngine.Parse(j.grid, j.id, gravity);
    }
}
