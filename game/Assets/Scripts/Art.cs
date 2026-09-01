using System.Collections.Generic;
using UnityEngine;

namespace SlimeEscape
{
    /// <summary>
    /// 🔴 **그림 갈아끼우는 자리.** (09-02 사장님 — "내가 이미지 다 새로 줄게")
    ///
    /// `Assets/Resources/Art/` 에 png 를 떨어뜨리면 그 자리만 그림으로 바뀐다.
    /// 없으면 지금까지처럼 코드가 그린다. **한 장씩 갈아끼울 수 있다** —
    /// 돌 하나만 주셔도 돌만 바뀌고 나머지는 그대로 돈다.
    ///
    /// 🔴 파일 이름이 곧 자리 이름이다. 목록은 `Assets/Resources/Art/README.md`.
    ///    이름이 틀리면 **조용히 무시된다** — 안 바뀌면 철자부터 본다.
    ///
    /// 한 칸을 꽉 채우게 들어간다. 여백은 그림 안에서 잡으면 된다 —
    /// 코드가 크기를 줄이지 않는다 (`ArtImport` 가 픽셀당 단위를 맞춰준다).
    /// </summary>
    public static class Art
    {
        static Dictionary<string, Sprite> _map;

        static void Load()
        {
            if (_map != null) return;
            _map = new Dictionary<string, Sprite>();
            foreach (var s in Resources.LoadAll<Sprite>("Art"))
                if (s != null) _map[s.name] = s;
        }

        /// 그림이 있으면 준다. 없으면 null — 부르는 쪽이 코드 그림으로 넘어간다.
        public static Sprite Get(string name)
        {
            Load();
            return name != null && _map.TryGetValue(name, out var s) ? s : null;
        }

        /// 한 장짜리든 애니든, 이 자리에 그림이 있는가.
        public static bool Has(string name) => Get(name) != null || Get(name + "_0") != null;

        /// <summary>
        /// 그림이 있으면 칸을 꽉 채우고(1f), 없으면 코드가 쓰던 크기를 그대로 쓴다.
        /// 🔴 그림에는 그리는 사람이 여백을 넣는다. 코드가 또 줄이면 두 번 줄어든다.
        /// </summary>
        public static float Scale(string name, float codeScale) => Has(name) ? 1f : codeScale;

        /// <summary>
        /// 🔴 그림이 있으면 **색을 안 입힌다.** 그린 대로 나와야 한다 —
        /// 코드가 쓰던 어두운 색을 덮칠하면 그림이 그대로 죽는다.
        /// 다만 **상태를 색으로 말하는 것**(덤은 칸 · 굳은 몸)은 그대로 물들인다.
        /// </summary>
        public static Color Tint(string name, Color codeColor)
            => Has(name) ? Color.white : codeColor;

        static Dictionary<string, Sprite[]> _anim;

        /// <summary>
        /// 🔴 **애니.** `이름_0.png` `이름_1.png` ... 를 순서대로 모아준다.
        /// 여러 장이 없으면 한 장짜리 `이름.png` 를, 그것도 없으면 빈 배열을 준다.
        ///
        /// 움직임은 작게 둔다 — 퍼즐에서 배경이 크게 흔들리면 판을 읽는 눈이 흩어진다.
        /// "살아 있다"만 알려주면 된다.
        /// </summary>
        static readonly Sprite[] None = new Sprite[0];

        public static Sprite[] Frames(string name)
        {
            //  🔴 이름이 없으면 빈손으로 돌려보낸다.
            //     사전 열쇠에 null 을 넣으면 ArgumentNullException 으로 죽는다 —
            //     안내판을 그림으로 바꾸면서 그림 이름 없이 부르는 자리가 생겼다 (09-02).
            if (string.IsNullOrEmpty(name)) return None;
            Load();
            if (_anim == null) _anim = new Dictionary<string, Sprite[]>();
            if (_anim.TryGetValue(name, out var got)) return got;

            var list = new List<Sprite>();
            for (int i = 0; ; i++)
            {
                var s = Get(name + "_" + i);
                if (s == null) break;
                list.Add(s);
            }
            if (list.Count == 0)
            {
                var one = Get(name);
                if (one != null) list.Add(one);
            }
            got = list.ToArray();
            _anim[name] = got;
            return got;
        }

        /// 지금 보여줄 장. 그림이 아예 없으면 null — 부르는 쪽이 코드 그림으로 간다.
        public static Sprite Frame(string name, float fps = 8f)
        {
            var f = Frames(name);
            if (f.Length == 0) return null;
            if (f.Length == 1) return f[0];
            return f[(int)(Time.time * fps) % f.Length];
        }

        /// 판을 다시 읽을 때 쓸 일이 생기면. 지금은 안 부른다.
        public static void Forget() { _map = null; _anim = null; }
    }
}

