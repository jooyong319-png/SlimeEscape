using UnityEngine;

namespace SlimeEscape
{
    /// <summary>
    /// 움직임 수치. 게임 안에서 K를 눌러 돌려보고 저장한다.
    /// 🔴 여기 값을 코드 여기저기에 흩지 말 것 — 손맛을 잡을 땐 한 곳에서 돌려야 한다.
    /// </summary>
    [System.Serializable]
    public class Tuning
    {
        [Header("걸음")]
        public float stepTime = 0.13f;     // 옆으로 한 칸 가는 시간 (초)
        public float stepEase = 2.2f;      // 클수록 처음에 확 나가고 끝에 붙는다

        [Header("낙하")]
        public float gravity = 46f;        // 낙하 가속 (칸/초²) — 클수록 무겁다
        public float maxFall = 26f;        // 최고 낙하 속도 (칸/초)

        [Header("착지")]
        public float landSquash = 0.30f;   // 얼마나 눌리는가 (0이면 안 눌림)
        public float squashRecover = 13f;  // 되돌아오는 속도 — 작을수록 오래 출렁인다

        [Header("몸이 변할 때")]
        public float resizeTime = 0.08f;   // 몸이 옆으로 밀려나는 데 걸리는 시간
        public float sizeChase = 16f;      // 몸 크기가 목표를 쫓는 속도 — 마디와 상관없이 계속 따라간다
        public float growPop = 0.22f;      // 커질 때 한 번 부푸는 정도
        public float stepSquash = 0.10f;   // 옆으로 갈 때 앞으로 늘어나는 정도

        [Header("막혔을 때")]
        public float bumpDistance = 0.16f; // 벽 쪽으로 살짝 밀렸다 돌아오는 거리
        public float bumpTime = 0.13f;

        [Header("보기")]
        /// 몸이 차지하는 칸(N×N) 대비 그림을 얼마나 작게 그리는가.
        /// 🔴 규칙상 차지하는 칸은 그대로다 — 그림만 줄어든다.
        ///    2026-08-28 사장님 기준: **크기 2 @ 0.55**가 화면에서 한 칸으로 읽힌다.
        public float spriteInset = 0.55f;

        /// 몸이 실제로 차지하는 N×N 칸을 얼마나 진하게 깔아 보여줄까 (0이면 끔).
        /// 🔴 그림을 작게 그리면 차지하는 칸이 안 보여서, 왜 못 지나가는지 알 수가 없다.
        public float footprintAlpha = 0.13f;

        const string Key = "tuning";

        public static Tuning Load()
        {
            var json = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return new Tuning();
            try { return JsonUtility.FromJson<Tuning>(json) ?? new Tuning(); }
            catch { return new Tuning(); }
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        /// 조절 패널이 그릴 항목들. (이름, 최소, 최대, 읽기, 쓰기)
        public (string label, float min, float max, System.Func<float> get, System.Action<float> set)[] Knobs() =>
            new (string, float, float, System.Func<float>, System.Action<float>)[]
            {
                ("걸음 시간",    0.04f, 0.35f, () => stepTime,      v => stepTime = v),
                ("걸음 가속감",  1.0f,  4.0f,  () => stepEase,      v => stepEase = v),
                ("중력",         12f,   120f,  () => gravity,       v => gravity = v),
                ("최고 낙하속도", 8f,    60f,   () => maxFall,       v => maxFall = v),
                ("착지 눌림",    0f,    0.6f,  () => landSquash,    v => landSquash = v),
                ("눌림 회복",    4f,    30f,   () => squashRecover, v => squashRecover = v),
                ("몸 밀림 시간", 0.03f, 0.35f, () => resizeTime,    v => resizeTime = v),
                ("몸 변화 속도", 5f,    40f,   () => sizeChase,     v => sizeChase = v),
                ("커질 때 부풂", 0f,    0.5f,  () => growPop,       v => growPop = v),
                ("걸을 때 늘어남", 0f,  0.3f,  () => stepSquash,    v => stepSquash = v),
                ("막힘 밀림",    0f,    0.4f,  () => bumpDistance,  v => bumpDistance = v),
                ("막힘 시간",    0.05f, 0.3f,  () => bumpTime,      v => bumpTime = v),
                ("그림 크기",    0.30f, 1.0f,  () => spriteInset,   v => spriteInset = v),
                ("차지한 칸",    0f,    0.45f, () => footprintAlpha, v => footprintAlpha = v),
            };
    }
}
