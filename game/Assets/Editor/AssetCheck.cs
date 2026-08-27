using System.Text;
using UnityEditor;
using UnityEngine;

namespace SlimeEscape.EditorTools
{
    /// <summary>
    /// 런타임에 제일 잘 깨지는 것: 텍스처를 못 찾거나, 프레임 수가 안 맞아
    /// 칸 크기가 소수로 떨어지는 경우. 컴파일로는 안 잡힌다.
    ///
    ///   Unity.exe -batchmode -quit -projectPath game -executeMethod SlimeEscape.EditorTools.AssetCheck.Run
    /// </summary>
    public static class AssetCheck
    {
        // (경로, 프레임 수, art/frames.json에 실측된 칸 크기)
        static readonly (string path, int frames, int cw, int ch)[] Sheets =
        {
            ("Art/slime_idle_sheet", 4, 63, 52),
            ("Art/slime_move_sheet", 3, 67, 47),
            ("Art/fire_idle_sheet",  4, 84, 114),
        };

        [MenuItem("SlimeEscape/그림 검사")]
        public static bool Check()
        {
            var log = new StringBuilder();
            int fail = 0;

            foreach (var s in Sheets)
            {
                var tex = Resources.Load<Texture2D>(s.path);
                if (tex == null) { log.AppendLine($"  X {s.path} — 텍스처를 못 찾음"); fail++; continue; }

                int cw = tex.width / s.frames;
                bool exact = tex.width % s.frames == 0;
                bool matches = cw == s.cw && tex.height == s.ch;
                bool point = tex.filterMode == FilterMode.Point;

                if (!exact || !matches || !point)
                {
                    fail++;
                    log.AppendLine($"  X {s.path} — {tex.width}x{tex.height}, 칸 {cw}x{tex.height}" +
                                   (!exact ? " · 폭이 프레임 수로 안 나눠떨어진다" : "") +
                                   (!matches ? $" · 실측({s.cw}x{s.ch})과 다르다" : "") +
                                   (!point ? $" · 필터가 {tex.filterMode} (Point여야 안 흐려진다)" : ""));
                    continue;
                }

                var sheet = PixelSprites.Load(s.path, s.frames);
                if (sheet.Frames == null || sheet.Frames.Length != s.frames) { log.AppendLine($"  X {s.path} — 자르기 실패"); fail++; continue; }
                log.AppendLine($"  O {s.path} — {s.frames}프레임 · 칸 {cw}x{tex.height} · {sheet.UnitW:0.00}x{sheet.UnitH:0.00} units");
            }

            // 판 데이터가 실제로 읽히는지 + 시작 상태가 서는지
            try
            {
                var defs = LevelSet.LoadAll();
                foreach (var d in defs)
                {
                    var L = LevelSet.ToLevel(d);
                    if (!SlimeEngine.StartState(L, out var st)) { log.AppendLine($"  X {d.id} — 시작 상태가 안 선다"); fail++; }
                    else log.AppendLine($"  O {d.id} — {L.W}x{L.H} · 시작 크기 {st.N} · 먹이 {L.Foods.Count} · 불 {L.Fires.Count}");
                }
            }
            catch (System.Exception e) { log.AppendLine($"  X 판 데이터 — {e.Message}"); fail++; }

            Debug.Log($"[그림·판 검사]\n{log}\n{(fail == 0 ? "통과" : $"실패 {fail}개")}");
            return fail == 0;
        }

        public static void Run() => EditorApplication.Exit(Check() ? 0 : 1);
    }
}
