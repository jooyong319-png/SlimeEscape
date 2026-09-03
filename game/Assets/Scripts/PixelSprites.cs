using UnityEngine;

namespace SlimeEscape
{
    /// <summary>
    /// 🔴 **코드가 그리는 도형 넷.** 납작한 그림은 전부 여기서 나온다 (09-02 확정).
    /// 네모 · 원 · 마름모 · 모서리 깎은 네모 — 색은 SpriteRenderer.color 가 준다.
    ///
    /// 한 번 만들고 계속 돌려 쓴다. 판마다 새로 만들면 텍스처가 쌓인다.
    /// </summary>
    public static class PixelSprites
    {
        static Sprite _solid, _disc;
        static Sprite _round;
        static Sprite _halo;
        static Sprite _diamond;

        /// 벽·바닥용 1×1 흰 사각형 (색은 SpriteRenderer.color로 준다)
        public static Sprite Solid()
        {
            if (_solid != null) return _solid;
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, Color.white); t.Apply();
            _solid = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1, 0, SpriteMeshType.FullRect);
            _solid.hideFlags = HideFlags.HideAndDontSave;
            return _solid;
        }

        /// 먹이용 원. 가장자리를 부드럽게 깎아 픽셀 계단을 줄인다.
        public static Sprite Disc(int size = 32)
        {
            if (_disc != null) return _disc;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
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
            _disc.hideFlags = HideFlags.HideAndDontSave;
            return _disc;
        }

        /// <summary>
        /// 🔴 마름모. 머리에 박힌 **열쇠**를 그린다 (09-02 사장님).
        /// 세로로 길게 쓰려고 만든 것이라 가로세로는 localScale 로 따로 준다 —
        /// 네모를 45도 돌려 쓰면 비스듬히 눌려서 마름모가 안 나온다.
        /// </summary>
        public static Sprite Diamond(int size = 64)
        {
            if (_diamond != null) return _diamond;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            float h = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    //  |x| + |y| <= 1 이 마름모다. 가장자리만 한 픽셀 부드럽게.
                    float d = (Mathf.Abs(x + 0.5f - h) + Mathf.Abs(y + 0.5f - h)) / h;
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1f - d) * h * 0.5f)));
                }
            t.Apply();
            _diamond = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            _diamond.hideFlags = HideFlags.HideAndDontSave;
            return _diamond;
        }

        /// <summary>
        /// 🔴 몸통용 **둥근 네모**. 각진 네모는 아무리 색을 잘 써도 블록으로 보인다.
        /// 모서리만 깎아도 "말랑한 것"이 된다 — 슬라임에게는 그게 전부다.
        /// </summary>
        static Sprite[] _drops;

        /// 밑이 퍼진 모양을 몇 단으로 구워 둘 것인가. 많을수록 부드럽지만 텍스처가 는다.
        public const int DropLevels = 7;

        /// <summary>
        /// 🔴 **밑이 퍼진 둥근 네모** (09-03 사장님: "밑에 사이드도 촥").
        /// 0 이면 <see cref="Round"/> 와 똑같이 좌우대칭이고, 단이 오를수록 아래만 넓어진다.
        ///
        /// 마디 밑에 조각을 따로 깔지 않으려고 **모양 자체**를 여러 단으로 굽는다 —
        /// 조각을 깔면 09-02 처럼 비석으로 보일 위험이 있고, 색·자리를 늘 맞춰줘야 한다.
        ///
        /// 넓히는 대신 **위를 좁힌다.** 칸을 넘을 수 없으니 넓힐 자리가 없다.
        /// </summary>
        public static Sprite Drop(int level, int size = 48, float radius = 0.30f)
        {
            if (_drops == null) _drops = new Sprite[DropLevels];
            level = Mathf.Clamp(level, 0, DropLevels - 1);
            if (_drops[level] != null) return _drops[level];

            float taper = 0.17f * level / (DropLevels - 1f);
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            float r = size * radius;
            for (int y = 0; y < size; y++)
            {
                //  y = 0 이 아래다. 아래 절반쯤에서만 부풀고 위로 갈수록 0 이 된다.
                float up = y / (size - 1f);
                float k  = Mathf.Max(0f, 1f - up / 0.55f);
                k = k * k * (3f - 2f * k);                 // 부드럽게 (매끈한 계단)
                float inset = size * 0.5f * taper * (1f - k);
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float dx = Mathf.Max(0f, Mathf.Max((inset + r) - px, px - (size - inset - r)));
                    float dy = Mathf.Max(0f, Mathf.Max(r - py, py - (size - r)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d + 0.5f)));
                }
            }
            t.Apply();
            _drops[level] = Sprite.Create(t, new Rect(0, 0, size, size),
                                          new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            _drops[level].hideFlags = HideFlags.HideAndDontSave;
            return _drops[level];
        }

        static Sprite _fade;

        /// <summary>
        /// 🔴 **한쪽으로 사라지는 네모** (09-03 사장님: "마지막 칸만 그라데이션으로").
        /// 왼쪽(-X)이 꽉 차고 오른쪽으로 갈수록 투명해진다.
        /// 다른 방향은 돌려서 쓴다 — 왼 0° · 아래 90° · 오른 180° · 위 270°.
        ///
        /// 칸 하나 안에서 서서히 변하는 것은 단색 도형으로는 못 한다.
        /// </summary>
        public static Sprite Fade(int size = 48)
        {
            if (_fade != null) return _fade;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
                    { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;              // 0 = 왼쪽(꽉 참)
                float a = 1f - u;
                a = a * a * (3f - 2f * a);                // 부드럽게 — 직선이면 끝이 뚝 끊긴다
                var c = new Color(1, 1, 1, a);
                for (int y = 0; y < size; y++) t.SetPixel(x, y, c);
            }
            t.Apply();
            _fade = Sprite.Create(t, new Rect(0, 0, size, size),
                                  new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            _fade.hideFlags = HideFlags.HideAndDontSave;
            return _fade;
        }

        static Sprite _soft;

        /// <summary>
        /// 🔴 **살짝만 둥근 네모** (09-04 사장님). <see cref="Round"/> 는 모서리가 30% 라
        /// 단추처럼 길게 늘이면 알약이 된다. 이건 6% 라 늘여도 네모로 남는다.
        ///
        /// 크게 구워야 한다 — 작게 구워 늘이면 모서리 계단이 그대로 늘어난다.
        /// </summary>
        /// <summary>
        /// 🔴 **가장자리로 갈수록 옅어지는 동그라미** (09-05 사장님).
        /// Soft() 는 둥근 **네모**라 빛무리로 쓰면 네모가 보인다 — 사장님 화면에서 그랬다.
        /// 가운데가 제일 짙고 밖으로 갈수록 사라진다. 딱 끊기는 테가 없어야 빛으로 읽힌다.
        /// 세제곱으로 떨어뜨린다 — 선형이면 가장자리에 옅은 테가 남아 원판처럼 보인다.
        /// </summary>
        public static Sprite Halo(int size = 96)
        {
            if (_halo != null) return _halo;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
                    { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            float c = (size - 1) * 0.5f, r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                    float a = Mathf.Clamp01(1f - d);
                    t.SetPixel(x, y, new Color(1, 1, 1, a * a * a));
                }
            t.Apply();
            _halo = Sprite.Create(t, new Rect(0, 0, size, size),
                                  new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            _halo.hideFlags = HideFlags.HideAndDontSave;
            return _halo;
        }

        public static Sprite Soft(int size = 96, float radius = 0.06f)
        {
            if (_soft != null) return _soft;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            float r = size * radius;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(r - (x + 0.5f), (x + 0.5f) - (size - r)));
                    float dy = Mathf.Max(0f, Mathf.Max(r - (y + 0.5f), (y + 0.5f) - (size - r)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d + 0.5f)));
                }
            t.Apply();
            _soft = Sprite.Create(t, new Rect(0, 0, size, size),
                                  new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            _soft.hideFlags = HideFlags.HideAndDontSave;
            return _soft;
        }

        static Sprite _speaker;

        /// <summary>
        /// 🔴 **확성기** (09-04 사장님). 몸통 + 벌어지는 원뿔 + 소리 결 둘.
        ///
        /// 이모지로 못 한다 — 잘라낸 글꼴에 없어서 **빌드에서만** 빈칸이 된다
        /// (rev.7-a 의 ※ 사건). 도형으로도 못 한다 — 사다리꼴도 호도 없다.
        /// 그래서 <see cref="Drop"/> · <see cref="Fade"/> 처럼 **모양을 직접 굽는다.**
        ///
        /// 가장자리는 3×3 으로 재서 부드럽게 한다. 안 그러면 원뿔의 빗변이 계단이 된다.
        /// </summary>
        public static Sprite Speaker(int size = 96)
        {
            if (_speaker != null) return _speaker;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
                    { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = 0f;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                            a += Inside((x + (sx + 0.5f) / 3f) / size,
                                        (y + (sy + 0.5f) / 3f) / size) ? 1f : 0f;
                    t.SetPixel(x, y, new Color(1, 1, 1, a / 9f));
                }
            t.Apply();
            _speaker = Sprite.Create(t, new Rect(0, 0, size, size),
                                     new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            _speaker.hideFlags = HideFlags.HideAndDontSave;
            return _speaker;
        }

        /// 확성기 안쪽인가. u·v 는 0~1 (v 는 아래가 0)
        static bool Inside(float u, float v)
        {
            float dy = Mathf.Abs(v - 0.5f);

            //  몸통 — 네모
            if (u >= 0.13f && u <= 0.34f && dy <= 0.14f) return true;

            //  원뿔 — 오른쪽으로 갈수록 벌어진다
            if (u > 0.34f && u <= 0.56f)
            {
                float k = (u - 0.34f) / 0.22f;
                if (dy <= Mathf.Lerp(0.14f, 0.38f, k)) return true;
            }

            //  소리 결 둘 — 원뿔 입 앞쪽으로 퍼지는 두 겹
            float ex = u - 0.56f;
            if (ex > 0.01f)
            {
                float d = Mathf.Sqrt(ex * ex + dy * dy);
                //  앞쪽 100도 안에서만 (뒤로 감기면 원이 된다)
                if (Mathf.Abs(Mathf.Atan2(v - 0.5f, ex)) < 0.90f)
                {
                    if (Mathf.Abs(d - 0.13f) < 0.030f) return true;
                    if (Mathf.Abs(d - 0.24f) < 0.030f) return true;
                }
            }
            return false;
        }

        public static Sprite Round(int size = 48, float radius = 0.30f)
        {
            if (_round != null) return _round;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
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
            _round.hideFlags = HideFlags.HideAndDontSave;
            return _round;
        }
    }
}
