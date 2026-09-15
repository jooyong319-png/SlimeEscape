using UnityEngine;

namespace SlimeEscape
{
    /// <summary>
    /// 🔴 **효과음** (09-13 밤 — 사장님이 고르심: "효과음 만들기").
    /// 전엔 별 딸 때 「딩」 하나뿐이었다 (브리프: 효과음은 마감 단계에 · todo 「나중」: 걸음·먹기·빨려들어감).
    ///
    /// 파일을 두지 않는다 — <see cref="Ding"/> 처럼 **그때 굽는다.** 빌드에 자산이 안 붙는다.
    /// 소리는 전부 **말랑한 쪽**이다: 사인이 짧게 휘는 "뽁" 이 뼈대고, 무거운 착지만 잡음을 섞는다.
    /// 딱딱한 클릭·삑 소리는 안 쓴다 — 화면을 말랑하게 한 것(09-13)과 같은 방향이다.
    ///
    /// ⚠️ 사장님이 **아직 안 들어보셨다.** 크기·높이는 BakeSfx() 의 숫자만 만지면 된다.
    /// </summary>
    public partial class SnakeController
    {
        enum Sfx { Step, Eat, Land, Door, Undo, Tap, Locked }
        const int SfxCount = 7;
        //  ⚠️ **직렬화하지 않는다** (09-15). 에디터가 스크립트를 다시 읽을 때 private 배열을
        //     null 이 아니라 **빈 배열**로 되살려서, `_sfx == null` 검사를 지나 _sfxSrc[0] 에서
        //     IndexOutOfRange 가 걸음마다 났다 (41판 정답 재생 중에 잡혔다).
        [System.NonSerialized] AudioClip[] _sfx;
        [System.NonSerialized] AudioSource[] _sfxSrc;
        int _sfxNext;
        readonly float[] _sfxAt = new float[SfxCount];

        void Play(Sfx s, float vol = 1f, float pitch = 1f)
        {
            if (_demo || _mute || _volume <= 0.001f) return;
            int k = (int)s;
            //  같은 소리가 한꺼번에 겹치면 커지기만 한다 — 40ms 안엔 한 번
            if (Time.unscaledTime - _sfxAt[k] < 0.04f) return;
            _sfxAt[k] = Time.unscaledTime;
            //  길이까지 본다 — 빈 배열이면 null 검사로는 못 잡는다
            if (_sfx == null || _sfx.Length != SfxCount || _sfxSrc == null || _sfxSrc.Length == 0) BakeSfx();
            //  🔴 높이를 조금씩 흔든다 — 같은 걸음이 매번 똑같으면 기계 소리가 된다.
            //     높이는 AudioSource 에 걸리므로 넷을 돌려 쓴다 (앞에서 울리는 소리의 높이를 안 바꾸게).
            var src = _sfxSrc[_sfxNext];
            _sfxNext = (_sfxNext + 1) % _sfxSrc.Length;
            src.pitch = pitch;
            src.PlayOneShot(_sfx[k], _volume * vol);
        }

        void BakeSfx()
        {
            _sfxSrc = new AudioSource[4];
            for (int i = 0; i < _sfxSrc.Length; i++)
            {
                _sfxSrc[i] = gameObject.AddComponent<AudioSource>();
                _sfxSrc[i].playOnAwake = false;
            }
            _sfx = new AudioClip[SfxCount];

            //  걸음 — 짧은 "뽁". 제일 자주 나니 제일 작고 짧다
            _sfx[(int)Sfx.Step] = Bake("step", 0.09f, t => Blip(t, 520f, 300f, 0.07f, 45f) * 0.16f);
            //  먹기 — 올라가는 "꿀꺽" 두 번. 몸이 는다
            _sfx[(int)Sfx.Eat] = Bake("eat", 0.16f, t =>
                (Blip(t, 280f, 640f, 0.09f, 26f) + 0.6f * Blip(t - 0.05f, 420f, 880f, 0.08f, 30f)) * 0.20f);
            //  착지 — 낮은 "철퍽". 무거운 소리라 잡음을 조금 섞는다
            var rng = new System.Random(7);
            float lp = 0f;
            _sfx[(int)Sfx.Land] = Bake("land", 0.20f, t =>
            {
                float n = (float)rng.NextDouble() * 2f - 1f;
                lp += 0.12f * (n - lp);                          // 거친 잡음을 뭉근하게
                return Blip(t, 190f, 80f, 0.14f, 22f) * 0.30f + lp * Mathf.Exp(-t * 45f) * 0.35f;
            });
            //  홈이 다 차서 빨려든다 — 길게 올라가는 "슈우욱". 떨림을 얹어 물기가 돈다
            _sfx[(int)Sfx.Door] = Bake("door", 0.60f, t =>
            {
                float env = Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-t * 4.2f);
                float ph = Phase(t, 220f, 880f, 0.50f) + 0.6f * Mathf.Sin(2f * Mathf.PI * 11f * t);
                return env * 0.22f * (Mathf.Sin(ph) + 0.3f * Mathf.Sin(2f * ph));
            });
            //  되돌리기 — 내려가는 "뿅". 걸음보다 가볍다
            _sfx[(int)Sfx.Undo] = Bake("undo", 0.10f, t => Blip(t, 640f, 360f, 0.08f, 40f) * 0.13f);
            //  단추 — 아주 작은 "톡"
            _sfx[(int)Sfx.Tap] = Bake("tap", 0.05f, t => Blip(t, 900f, 700f, 0.04f, 80f) * 0.10f);
            //  잠긴 판 — 둔한 "툭"
            _sfx[(int)Sfx.Locked] = Bake("lock", 0.10f, t => Blip(t, 170f, 120f, 0.08f, 50f) * 0.22f);
        }

        /// 휘는 사인 한 번 — f0 에서 f1 로 T 동안 (지수로) 가고, 빨리 치고 k 의 빠르기로 잦아든다
        static float Blip(float t, float f0, float f1, float T, float k)
        {
            if (t < 0f) return 0f;
            float env = Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t * k);
            float ph = Phase(t, f0, f1, T);
            return env * (Mathf.Sin(ph) + 0.25f * Mathf.Sin(2f * ph));
        }

        /// 주파수가 f0→f1 로 지수로 휘는 사인의 위상. T 가 지나면 f1 으로 곧게 간다.
        /// 주파수를 그냥 sin(2πf(t)t) 로 바꾸면 소리가 찢어진다 — 위상을 쌓아야 한다.
        static float Phase(float t, float f0, float f1, float T)
        {
            float r = Mathf.Log(f1 / f0);
            float u = Mathf.Min(t, T);
            float p = Mathf.Abs(r) < 1e-4f ? f0 * u : f0 * T * (Mathf.Exp(r * u / T) - 1f) / r;
            if (t > T) p += f1 * (t - T);
            return 2f * Mathf.PI * p;
        }

        static AudioClip Bake(string name, float len, System.Func<float, float> f)
        {
            const int rate = 44100;
            int n = Mathf.Max(1, (int)(rate * len));
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)rate), -1f, 1f);
            //  끝을 5ms 동안 줄인다 — 뚝 끊기면 "딸깍" 이 난다
            int fade = Mathf.Min(n, rate / 200);
            for (int i = 0; i < fade; i++) d[n - 1 - i] *= i / (float)fade;
            var c = AudioClip.Create(name, n, 1, rate, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
