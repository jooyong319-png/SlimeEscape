using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SlimeEscape.EditorTools
{
    /// <summary>
    /// 🔴 `Resources/Art/` 의 그림을 **실제로 읽히는 상태로** 만들어 두고, 확인해서 알려준다.
    ///
    /// 왜 이게 따로 필요한가 (09-02에 겪은 것):
    /// 들여오기 콜백(`OnPreprocessTexture`) 안에서 `textureType` 을 Sprite 로 바꾸면
    /// **안 먹는다.** 픽셀당 단위 같은 건 들어가는데 종류만 조용히 안 바뀐다.
    /// 그러면 Sprite 가 안 구워지고, `Resources.LoadAll&lt;Sprite&gt;` 는 빈손으로 오고,
    /// 게임은 **아무 말 없이** 코드 그림으로 넘어간다 — 넣었는데 안 나오고, 이유도 안 뜬다.
    ///
    /// 그래서 콜백 **밖에서** 고친다. 여기서 `SaveAndReimport` 로 못박으면 확실히 먹는다.
    /// </summary>
    public static class ArtCheck
    {
        const string Dir = "Assets/Resources/Art";

        /// 게임이 쓰는 자리 이름. 여기 없는 이름으로 넣으면 안 쓰인다.
        static readonly string[] Slots =
        {
            //  지형
            "wall", "wall_top", "floor",
            //  슬라임
            "head", "body", "link", "key", "key_glow",
            //  물건
            "food", "star", "star_glow", "core", "pad", "pad_top", "spent",
            //  홈 틀
            "slot", "rail", "gem",
            //  판 고르기 지도
            "node", "node_lock",
        };

        /// 애니는 `이름_0` `이름_1` … 로 들어온다. 뒤에 붙은 수를 떼고 본다.
        static string Slot(string n)
        {
            int i = n.LastIndexOf('_');
            if (i <= 0 || i == n.Length - 1) return n;
            for (int k = i + 1; k < n.Length; k++)
                if (!char.IsDigit(n[k])) return n;
            return n.Substring(0, i);
        }

        [InitializeOnLoadMethod]
        static void OnLoad() => EditorApplication.delayCall += () => Run(false);

        [MenuItem("SlimeEscape/그림 확인")]
        public static void Menu() => Run(true);

        static void Run(bool loud)
        {
            if (!Directory.Exists(Dir)) return;
            int fixedUp = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                //  여러 칸짜리 옛 시트는 한 칸 규칙이 안 맞는다. 건드리지 않는다.
                if (name.EndsWith("_sheet")) continue;
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;

                int px = PngWidth(path);
                bool wrong = ti.textureType != TextureImporterType.Sprite
                             || ti.spriteImportMode != SpriteImportMode.Single
                             || !Mathf.Approximately(ti.spritePixelsPerUnit, px)
                             || ti.filterMode != FilterMode.Point
                             || ti.mipmapEnabled
                             || ti.textureCompression != TextureImporterCompression.Uncompressed;
                if (!wrong) continue;

                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                //  🔴 그림 한 장 = 판의 한 칸. 그래서 16×16으로 그리든 64×64로 그리든 코드가 그대로다.
                ti.spritePixelsPerUnit = px;
                ti.filterMode = FilterMode.Point;          // 도트는 뭉개지면 안 된다
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.alphaIsTransparency = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
                fixedUp++;
            }

            var names = Resources.LoadAll<Sprite>("Art").Select(s => s.name).OrderBy(n => n).ToArray();
            var used = names.Select(Slot).Distinct().Where(n => Slots.Contains(n)).OrderBy(n => n).ToArray();
            var strays = names.Where(n => !Slots.Contains(Slot(n)) && !n.EndsWith("_sheet")).ToArray();

            if (fixedUp > 0)
                Debug.Log("[Art] 들여오기 설정 " + fixedUp + "장 고침");
            if (loud || used.Length > 0 || strays.Length > 0)
                Debug.Log("[Art] 쓰이는 그림 " + used.Length + "장"
                          + (used.Length == 0 ? " (전부 코드가 그린다)" : ": " + string.Join(", ", used)));
            if (strays.Length > 0)
                Debug.LogWarning("[Art] 자리 이름과 안 맞아 **안 쓰이는** 파일: "
                                 + string.Join(", ", strays)
                                 + "\n     쓸 수 있는 이름: " + string.Join(", ", Slots)
                                 + "\n     (Resources/Art/README.md 참고)");
        }

        /// png 머리(IHDR)에서 가로 픽셀 수. 못 읽으면 32.
        internal static int PngWidth(string path)
        {
            try
            {
                using (var f = File.OpenRead(path))
                {
                    var b = new byte[24];
                    if (f.Read(b, 0, 24) < 24) return 32;
                    if (b[0] != 0x89 || b[1] != 'P' || b[2] != 'N' || b[3] != 'G') return 32;
                    int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                    return w > 0 && w <= 4096 ? w : 32;
                }
            }
            catch { return 32; }
        }
    }
}
