using System;
using UnityEngine;

namespace SlimeEscape
{
    /// <summary>levels.json 한 판. 표기값(best/sol/back)은 tools/stamp.js가 박은 것이다.</summary>
    [Serializable]
    public class LevelJson
    {
        public string id;
        public string name;
        public int startSize = 3;
        public int fireCost;      // 0이면 기본값 3 (JS의 `L.fireCost || 3`과 같다)
        public int best;
        public string sol;        // 정답 수순. → ←
        public int back;          // 되돌아가기 횟수. 0이면 "오른쪽만 누르면 풀린다"
        public string[] grid;
    }

    [Serializable]
    public class LevelSetJson
    {
        public LevelJson[] levels;
    }

    public static class LevelSet
    {
        public const string ResourcePath = "levels";

        /// 🔴 판 데이터는 사본이 없다. tools/stamp.js도 바로 이 파일을 읽고 쓴다.
        public static LevelJson[] LoadAll()
        {
            var ta = Resources.Load<TextAsset>(ResourcePath);
            if (ta == null) throw new Exception($"Resources/{ResourcePath}.json 을 못 찾았다");
            var set = JsonUtility.FromJson<LevelSetJson>(ta.text);
            if (set?.levels == null || set.levels.Length == 0) throw new Exception("levels.json에 판이 없다");
            return set.levels;
        }

        public static SlimeEngine.Level ToLevel(LevelJson j) =>
            SlimeEngine.Parse(j.grid, j.id, j.name, j.startSize, j.fireCost <= 0 ? 3 : j.fireCost);
    }
}
