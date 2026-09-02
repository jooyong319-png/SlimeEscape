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
        /// 🔴 가르치는 판. 맵 안에서 키부터 하나씩 알려준다.
        public bool tutorial;
        public int best;      // 최단 걸음 수 — stamp.js가 박는다
        public string sol;    // 정답 수순 ↑↓←→ — stamp.js가 박는다
        /// 🔴 별을 먹고 깨는 최단 걸음. 별 셋의 커트라인은 여기서 나온다.
        public int bestStar;
        /// 🔴 별 셋 커트라인 — 이 걸음 안에 별을 먹고 깨야 한다. 널널하게 잡는다.
        public int cut;
        public float lost;    // "이미 진 상태" 비율(%) — 낮으면 실수해도 회복된다 = 쉽다
        //  🔴 hard.js --stamp 가 박는 값들. **선언을 빼먹으면 조용히 0 이 된다** —
        //     JsonUtility 는 모르는 칸을 그냥 버린다. 판 자료에는 있는데
        //     게임은 0 으로 읽고, 검사가 "쉽다"고 잘못 말하게 된다 (09-02).
        public int states;      // 뒤져야 하는 상태 수. 0 이면 **못 재본 것**이다
        public float tight;     // 외길 비율 — 정답 위에서 삭끗하면 지는 지점
        public float backtrack; // 되짚기 — 정답이 목표에서 멀어지는 걸음의 비율
        public int wander;    // 🔴 진 뒤에도 더 돌아다닐 수 있는 걸음 수 — 채택 기준(14 이하)
        // 🔴 판은 목록 순서대로 이어진다 — 이어짐을 따로 적지 않는다.
        //    (08-30에 세계 지도·문양 획·양방향 이동을 걷어냈다. RPG 부속이라 퍼즐을 안 늘렸다)
        /// 🔴 "any" = 아무 문이나 열면 끝 · "all" = 문을 다 열어야 끝(이동하는 판)
        public string clear;
        public string[] grid;
    }

    [Serializable]
    public class SnakeLevelSetJson
    {
        /// 🔴 몇 번째 굴인가. 1묶음 16판 · 묶음마다 한 시간이 목표다 (08-30).
        public int chapter = 1;
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
            SnakeEngine.Parse(j.grid, j.id, gravity, j.clear);
    }
}
