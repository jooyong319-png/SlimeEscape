using UnityEditor;
using UnityEngine;

namespace SlimeEscape.EditorTools
{
    /// <summary>
    /// 🔴 도트 글꼴은 **뭉개면 안 된다.**
    ///
    /// 유니티는 글꼴을 들여올 때 기본으로 `Smooth`(안티에일리어싱)로 굽는다.
    /// 보통 글꼴엔 맞지만 도트 글꼴에는 최악이다 — 획 하나가 두 픽셀에 걸쳐
    /// 흐릿하게 번져서, 애써 도트로 맞춘 화면에서 **글자만 딴 세상**이 된다.
    /// `HintedRaster` 로 두면 픽셀 경계에 딱 떨어진다.
    ///
    /// 🔴 들여오기 콜백(`OnPreprocessFont`) 대신 여기서 고친다.
    ///    텍스처에서 같은 걸 콜백으로 했다가 다른 후처리기와 부딪혀 하루를 먹었다 (09-02).
    ///    콜백 밖에서 `SaveAndReimport` 로 못박는 쪽이 확실하다.
    /// </summary>
    public static class FontCheck
    {
        const string Path = "Assets/Resources/Fonts/kr.ttf";

        [InitializeOnLoadMethod]
        static void OnLoad() => EditorApplication.delayCall += () => Run(false);

        [MenuItem("SlimeEscape/글꼴 확인")]
        public static void Menu() => Run(true);

        static void Run(bool loud)
        {
            if (!(AssetImporter.GetAtPath(Path) is TrueTypeFontImporter fi))
            {
                if (loud) Debug.LogWarning("[글꼴] " + Path + " 를 못 찾았다");
                return;
            }

            if (fi.fontRenderingMode != FontRenderingMode.HintedRaster)
            {
                fi.fontRenderingMode = FontRenderingMode.HintedRaster;
                fi.SaveAndReimport();
                Debug.Log("[글꼴] 도트가 뭉개지지 않게 HintedRaster 로 고침");
            }
            else if (loud)
            {
                Debug.Log("[글꼴] HintedRaster — 도트가 또렷하게 나온다");
            }
        }
    }
}
