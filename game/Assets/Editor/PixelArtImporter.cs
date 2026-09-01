using System.IO;
using UnityEditor;
using UnityEngine;

namespace SlimeEscape.EditorTools
{
    /// <summary>
    /// `Resources/Art/` 에 들어오는 도트의 들여오기 설정을 **한 곳에서** 못박는다.
    ///
    /// 🔴 후처리기를 둘로 두지 말 것 (09-02에 하루를 먹은 것).
    /// 이 파일은 rev.2 때 만들어져 `textureType = Default` 로 두고 있었는데,
    /// 그림 자리를 만들며 Sprite 로 바꾸는 후처리기를 **하나 더** 붙였다.
    /// 둘이 같은 폴더를 잡고 서로 덮어써서, 설정을 아무리 고쳐도 Default 로 돌아갔다.
    /// `Resources.LoadAll&lt;Sprite&gt;` 는 계속 빈손이었고 게임은 **말없이** 코드 그림을 썼다.
    /// 컴파일도 통과하고 meta 도 절반은 맞아 보여서, 로그를 찍기 전엔 안 보였다.
    ///
    /// 규칙:
    ///   · `*_sheet.png` — rev.2 시절 여러 칸짜리 옛 그림. 예전 그대로 둔다
    ///   · 그 밖 — **Sprite 한 장**. 픽셀당 단위 = 그림의 가로 픽셀 수
    ///     → 그림 한 장이 정확히 판의 **한 칸**이 된다. 16×16이든 64×64든 코드가 그대로다
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.Contains("/Resources/Art/")) return;

            var t = (TextureImporter)assetImporter;
            //  도트는 뭉개지면 안 된다. 기본값(Bilinear + 압축)으로 들어오면 흐려진다.
            t.filterMode = FilterMode.Point;
            t.mipmapEnabled = false;
            t.alphaIsTransparency = true;
            t.npotScale = TextureImporterNPOTScale.None;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.wrapMode = TextureWrapMode.Clamp;
            t.maxTextureSize = 1024;

            if (Path.GetFileNameWithoutExtension(path).EndsWith("_sheet"))
            {
                t.textureType = TextureImporterType.Default;   // 옛 시트는 건드리지 않는다
                return;
            }

            t.textureType = TextureImporterType.Sprite;
            t.spriteImportMode = SpriteImportMode.Single;
            t.spritePixelsPerUnit = ArtCheck.PngWidth(assetPath);
        }
    }
}
