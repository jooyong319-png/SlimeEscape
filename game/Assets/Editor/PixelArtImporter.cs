using UnityEditor;
using UnityEngine;

namespace SlimeEscape.EditorTools
{
    /// <summary>
    /// 되살린 도트가 뭉개지지 않게 임포트 설정을 강제한다.
    /// 기본값(Bilinear + 압축)으로 들어오면 63×52짜리 그림이 흐려진다.
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Art/")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.filterMode = FilterMode.Point;
            t.mipmapEnabled = false;
            t.alphaIsTransparency = true;
            t.npotScale = TextureImporterNPOTScale.None;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.wrapMode = TextureWrapMode.Clamp;
            t.maxTextureSize = 1024;
        }
    }
}
