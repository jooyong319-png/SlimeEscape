using UnityEditor;
using UnityEngine;

namespace SlimeEscape.EditorTools
{
    /// <summary>
    /// 🔴 글꼴 들여오기 설정을 못박는다.
    ///
    /// 2026-09-12 에 도트 글꼴(갈무리11)을 **Pretendard** 로 바꿨다 — 도트를 전부 뺐다.
    /// 도트 글꼴일 때는 `HintedRaster`(픽셀 경계에 딱)로 뒀는데, 보통 글꼴은 그러면
    /// 곡선이 계단져 **삐뚤빼뚤**해진다. 이제는 `Smooth`(안티에일리어싱)가 맞다.
    /// 옛 설정이 meta 에 남아 있으니 여기서 한 번 되돌려 둔다.
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

            if (fi.fontRenderingMode != FontRenderingMode.Smooth)
            {
                fi.fontRenderingMode = FontRenderingMode.Smooth;
                fi.SaveAndReimport();
                Debug.Log("[글꼴] 도트 설정(HintedRaster)이 남아 있어 Smooth 로 고침");
            }
            else if (loud)
            {
                Debug.Log("[글꼴] Smooth — 곡선이 부드럽게 나온다");
            }
        }
    }
}
