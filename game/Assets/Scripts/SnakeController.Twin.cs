using System.Collections.Generic;
using UnityEngine;

namespace SlimeEscape
{
    // 🔴 **슬라임 둘 — 안 조종하는 쪽 그리기** (09-23).
    //    조종하는 슬라임은 원래 그리던 대로(_segs) 그린다. 이쪽은 **쉬는 슬라임**이라
    //    같은 초록을 **흐리게** 둔다 — "초록은 슬라임만" 규칙은 그대로다.
    //    ⚠️ 지금은 기능 시험용: 떨어질 때 부드럽게 따라가기만 하고, 조종이 넘어갈 땐 딱 붙인다.
    //       넘어가는 연출·표정은 사장님이 해보고 난 뒤에.
    public partial class SnakeController
    {
        readonly List<SpriteRenderer> _twinSegs = new List<SpriteRenderer>();
        readonly List<Vector2> _twinPos = new List<Vector2>();
        bool _twinSnap = true;
        static readonly Color TwinCol = new Color32(0x4a, 0x8f, 0x62, 0xff);   // 쉬는 슬라임 — 나보다 어둡다

        void TickTwinView()
        {
            var ob = (_L != null && _L.Twin && _st != null) ? _st.Ob : null;
            //  판을 새로 지으면 _root 와 함께 스프라이트가 사라진다 — 그때 다시 만든다
            for (int i = _twinSegs.Count - 1; i >= 0; i--)
                if (_twinSegs[i] == null) { _twinSegs.Clear(); _twinPos.Clear(); _twinSnap = true; break; }
            int n = ob != null ? ob.Count : 0;
            while (_twinSegs.Count < n)
            {
                var sr = NewSprite("Twin", 3);
                sr.sprite = PixelSprites.Drop(0);
                sr.color = TwinCol;
                _twinSegs.Add(sr);
                _twinPos.Add(n > 0 ? CellPos(ob[Mathf.Min(_twinPos.Count, n - 1)]) : Vector2.zero);
            }
            for (int i = 0; i < _twinSegs.Count; i++)
            {
                bool on = i < n;
                _twinSegs[i].enabled = on;
                if (!on) continue;
                Vector2 want = CellPos(ob[i]);
                _twinPos[i] = _twinSnap ? want : Vector2.MoveTowards(_twinPos[i], want, Time.deltaTime * 9f);
                _twinSegs[i].transform.position = _twinPos[i];
                float s = i == 0 ? 0.92f : 0.86f;
                _twinSegs[i].transform.localScale = new Vector3(s, s, 1);
            }
            _twinSnap = false;
        }
    }
}
