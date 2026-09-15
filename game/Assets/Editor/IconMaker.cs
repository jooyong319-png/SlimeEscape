using System.IO;
using UnityEditor;
using UnityEngine;

namespace SlimeEscape.EditorTools
{
    /// <summary>
    /// 🔴 **게임 아이콘을 코드로 굽는다** (09-16 다듬기 5단계 · 아이콘). 도트 아님 — 가장자리를 4×4 로 재서 부드럽게.
    ///
    /// 그림: 게임과 같은 어두운 둥근 네모 바탕 · 가운데 **슬라임**(게임의 몸 색, 밑이 살짝 퍼진 둥근 덩어리)
    ///       · 머리에 박힌 **흰 마름모 열쇠** — 게임 첫 화면에서 보는 그 캐릭터 그대로.
    /// 초록은 슬라임만 쓴다 (색 규칙). 아이콘에 홈·조각을 넣으면 작게 줄였을 때 뭉개져 안 넣는다.
    ///
    ///   메뉴 SlimeEscape > 아이콘 굽기   ·   또는 IconMaker.Bake()
    ///   → Assets/Art/Icon/icon.png (1024) · WebGLTemplates/SlimeEscape/TemplateData/icon.png (192)
    ///   → PlayerSettings 기본 아이콘으로 넣는다
    /// </summary>
    public static class IconMaker
    {
        const string IconPath = "Assets/Art/Icon/icon.png";
        const string WebIcon  = "Assets/WebGLTemplates/SlimeEscape/TemplateData/icon.png";

        static readonly Color Bg     = new Color32(0x12, 0x16, 0x1b, 0xff);   // 바탕 — 게임 빈 칸(Floor)
        static readonly Color BgTop  = new Color32(0x1a, 0x20, 0x28, 0xff);   // 위쪽이 조금 밝다 (게임과 같은 빛)
        static readonly Color Body   = new Color32(0x6f, 0xd0, 0x8c, 0xff);   // 슬라임 — BodyCol
        static readonly Color BodyHi = new Color32(0x9b, 0xe8, 0xad, 0xff);   // 젖은 윤
        static readonly Color Key    = Color.white;

        [MenuItem("SlimeEscape/아이콘 굽기")]
        public static void Bake()
        {
            var big = Draw(1024);
            Save(big, IconPath);
            Save(Draw(192), WebIcon);
            AssetDatabase.Refresh();

            var imp = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Default;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { tex });
            AssetDatabase.SaveAssets();
            Debug.Log("[아이콘] 구웠다 — " + IconPath + " · " + WebIcon);
        }

        static void Save(Texture2D t, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }

        static Texture2D Draw(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color[n * n];
            const int S = 4;                                   // 4×4 로 재서 가장자리를 부드럽게
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Color acc = Color.clear;
                    for (int sy = 0; sy < S; sy++)
                        for (int sx = 0; sx < S; sx++)
                        {
                            float u = (x + (sx + 0.5f) / S) / n, v = (y + (sy + 0.5f) / S) / n;   // v 는 아래가 0
                            acc += Shade(u, v);
                        }
                    px[y * n + x] = acc / (S * S);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// 한 점의 색 (앞에 있는 것이 이긴다). 투명 바깥은 알파 0.
        static Color Shade(float u, float v)
        {
            //  바탕 — 둥근 네모 (모서리 22%)
            if (!RoundRect(u, v, 0.5f, 0.5f, 0.5f, 0.5f, 0.22f)) return Color.clear;
            var c = Color.Lerp(Bg, BgTop, Mathf.SmoothStep(0f, 1f, v));

            //  슬라임 몸 — **말랑한 젤리 덩어리**. 가운데 · 조금 아래 (머리 위 여백)
            //  ⚠️ 처음엔 밑을 많이 퍼뜨렸더니(0.30 → 0.24) **사다리꼴** 이 되어 전등갓·지갑으로 보였다 (09-16 첫 굽기).
            //     밑은 아주 조금만 퍼지고, 모서리를 크게 깎는다 — 게임 속 슬라임이 이 모양이다.
            //  ⚠️ 16px 로 줄이면 몸이 **초록 점**이 됐다 (몸이 아이콘의 40% 뿐) — 크게 채운다.
            float cx = 0.5f, bottom = 0.17f, top = 0.75f;
            bool onBody = false;
            if (v >= bottom && v <= top)
            {
                float k = (v - bottom) / (top - bottom);                 // 0 밑 · 1 위
                float half = Mathf.Lerp(0.385f, 0.355f, Mathf.SmoothStep(0f, 1f, k));
                if (RoundRect(u, v, cx, (bottom + top) * 0.5f, half, (top - bottom) * 0.5f, 0.21f))
                {
                    onBody = true;
                    c = Body;
                    //  젖은 윤 — 왼쪽 위에 얇게
                    float dx = (u - (cx - 0.16f)) / 0.12f, dy = (v - (top - 0.09f)) / 0.032f;
                    if (dx * dx + dy * dy < 1f) c = Color.Lerp(Body, BodyHi, 0.85f);
                }
            }

            //  열쇠 — 흰 마름모 + 뒤에 **은은한 빛무리** (게임 속 머리가 이렇다).
            //  ⚠️ 빛무리 없이 납작한 흰 마름모만 두니 **카드의 ♦** 로 읽혔다 (09-16 첫 굽기).
            //  몸을 키운 만큼 열쇠·빛무리도 키운다 — 작게 줄여도 흰 마름모가 남아야 한다
            float ky0 = 0.47f;
            if (onBody)
            {
                float hx = (u - cx) / 0.19f, hy = (v - ky0) / 0.19f;
                float hd = hx * hx + hy * hy;
                if (hd < 1f) c = Color.Lerp(c, Color.white, 0.30f * (1f - hd) * (1f - hd));
            }
            float kx = (u - cx) / 0.085f, ky = (v - ky0) / 0.14f;
            if (Mathf.Abs(kx) + Mathf.Abs(ky) <= 1f) c = Key;
            return c;
        }

        /// 둥근 네모 안인가 — 가운데 (cx,cy) · 반폭 hw · 반높이 hh · 모서리 반지름 r
        static bool RoundRect(float u, float v, float cx, float cy, float hw, float hh, float r)
        {
            float dx = Mathf.Max(0f, Mathf.Abs(u - cx) - (hw - r));
            float dy = Mathf.Max(0f, Mathf.Abs(v - cy) - (hh - r));
            return dx * dx + dy * dy <= r * r;
        }
    }
}
