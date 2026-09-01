using UnityEngine;

namespace SlimeEscape
{
    /// <summary>
    /// 되살린 GIF 시트를 잘라 쓰는 도구. 칸 크기는 art/frames.json에 적힌 실측값이다.
    /// (한 픽셀도 안 고친 원본이라 여기 숫자를 바꾸면 그림이 어긋난다)
    /// </summary>
    public static class PixelSprites
    {
        public const int PPU = 100;

        public struct Sheet
        {
            public Sprite[] Frames;
            public float UnitW, UnitH;   // 한 프레임의 월드 크기 (units)
        }

        public static Sheet Load(string resourcePath, int frameCount)
        {
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null) { Debug.LogError($"텍스처 없음: Resources/{resourcePath}"); return default; }
            tex.filterMode = FilterMode.Point;

            int cw = tex.width / frameCount, ch = tex.height;
            var frames = new Sprite[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                frames[i] = Sprite.Create(
                    tex, new Rect(i * cw, 0, cw, ch),
                    new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
                frames[i].name = $"{resourcePath}_{i}";
            }
            return new Sheet { Frames = frames, UnitW = cw / (float)PPU, UnitH = ch / (float)PPU };
        }

        static Sprite _solid, _disc;
        static Sprite _round;

        /// 벽·바닥용 1×1 흰 사각형 (색은 SpriteRenderer.color로 준다)
        public static Sprite Solid()
        {
            if (_solid != null) return _solid;
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            t.SetPixel(0, 0, Color.white); t.Apply();
            _solid = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1, 0, SpriteMeshType.FullRect);
            return _solid;
        }

        /// 먹이용 원. 가장자리를 부드럽게 깎아 픽셀 계단을 줄인다.
        public static Sprite Disc(int size = 32)
        {
            if (_disc != null) return _disc;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float r = size * 0.5f - 0.5f, c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(r - d);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            _disc = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            return _disc;
        }

        /// <summary>
        /// 🔴 몸통용 **둥근 네모**. 각진 네모는 아무리 색을 잘 써도 블록으로 보인다.
        /// 모서리만 깎아도 "말랑한 것"이 된다 — 슬라임에게는 그게 전부다.
        /// </summary>
        public static Sprite Round(int size = 48, float radius = 0.30f)
        {
            if (_round != null) return _round;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float r = size * radius;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // 모서리에서만 원으로 깎는다
                    float dx = Mathf.Max(0f, Mathf.Max(r - (x + 0.5f), (x + 0.5f) - (size - r)));
                    float dy = Mathf.Max(0f, Mathf.Max(r - (y + 0.5f), (y + 0.5f) - (size - r)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d + 0.5f)));
                }
            t.Apply();
            _round = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            return _round;
        }
    }
}
