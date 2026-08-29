using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SlimeEscape.EditorTools
{
    /// <summary>
    /// 🔴 한글 폰트가 빌드를 8MB 불렸다.
    ///
    /// 유니티는 **동적 글꼴을 쓰면 TTF를 통째로** 집어넣는다. 우리가 쓰는 한글은
    /// 백 자도 안 되는데 1만 자가 넘는 글꼴 전체가 따라온다.
    /// 웹으로 받는 게임이라 이건 그대로 사용자 대기 시간이다.
    ///
    /// 그래서 **실제로 쓰는 글자만** 골라 정적 아틀라스로 굽는다.
    /// 🔴 글자 목록을 손으로 관리하면 반드시 어긋난다 — 소스와 판 데이터에서 **긁어온다.**
    ///    글을 새로 쓰면 빌드 전에 이게 다시 돌아 자동으로 포함된다.
    ///
    ///   Unity.exe -batchmode -quit -projectPath game -executeMethod SlimeEscape.EditorTools.FontSubset.Run
    /// </summary>
    public static class FontSubset
    {
        const string FontPath = "Assets/Resources/Fonts/kr.ttf";

        [MenuItem("SlimeEscape/폰트 글자 다시 긁기")]
        public static void Run()
        {
            var used = new HashSet<char>();

            // 화면에 뜨는 글자는 전부 코드와 판 데이터 안에 있다
            foreach (var f in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
                foreach (char c in File.ReadAllText(f)) used.Add(c);
            var levels = "Assets/Resources/levels.json";
            if (File.Exists(levels))
                foreach (char c in File.ReadAllText(levels)) used.Add(c);

            // 눈에 보이는 것만 남긴다 (줄바꿈·탭 등은 글꼴에 필요 없다)
            var keep = new List<char>();
            foreach (char c in used)
                if (!char.IsControl(c) && !char.IsWhiteSpace(c)) keep.Add(c);
            keep.Sort();

            // 숫자와 기본 기호는 항상 넣는다 — 기록 표가 이걸 쓴다
            const string always = "0123456789.,:/%()-+*=#·×… ";
            var sb = new StringBuilder();
            foreach (char c in always) if (!keep.Contains(c)) sb.Append(c);
            foreach (char c in keep) sb.Append(c);
            string chars = sb.ToString();

            var imp = AssetImporter.GetAtPath(FontPath) as TrueTypeFontImporter;
            if (imp == null)
            {
                Debug.LogError($"[폰트] {FontPath} 를 못 찾았다");
                EditorApplication.Exit(1);
                return;
            }

            // 🔴 **동적**으로 둔다. 정적(CustomSet)으로 구우면 유니티가 fontSize를 무시해서
            //    글자가 하나도 안 그려진다 — 실제로 그렇게 만들어 휴대폰에서 빈 화면을 봤다.
            //    용량은 대신 **폰트 파일 자체를 잘라서** 줄인다: python tools/subset-font.py
            imp.fontTextureCase = FontTextureCase.Dynamic;
            imp.fontRenderingMode = FontRenderingMode.Smooth;
            imp.SaveAndReimport();

            Debug.Log($"[폰트] 동적으로 둔다. 화면에 쓰이는 글자 {chars.Length}자 — " +
                      "이만큼만 남기려면 python tools/subset-font.py 를 돌릴 것");
            EditorApplication.Exit(0);
        }
    }
}
