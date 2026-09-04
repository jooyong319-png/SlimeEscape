/* 🔬 실험 엔진 3 — 뱀 + 공간 채우기.
   engine.js(지금 게임) / engine2.js(직사각형)는 안 건드린다.

   규칙 (2026-08-28)
   - 몸은 **칸의 사슬**. 머리가 앞. 시작 길이 1
   - 🔴 **중력 없다.** 머리를 상하좌우로 한 칸씩 움직이면 몸이 따라온다 (꼬리가 비켜난다)
   - '+' 를 먹으면 **길이 +1** (그 걸음엔 꼬리가 안 비켜난다). 줄어드는 건 없다
   - 벽·자기 몸에 부딪히는 걸음은 **막힌다** (죽지 않는다. 못 갈 뿐)
   - 🔴 클리어 = '=' 로 표시된 칸을 **몸이 정확히** 덮는다
        → 길이가 목표 칸 수와 같아야 하고, 접힌 모양도 맞아야 한다

   기호:  # 벽 · . 빈칸 · S 시작(머리) · + 먹이 · = 채워야 할 칸
*/
(function (root) {
  'use strict';

  const DIRS = [[0, -1, '↑'], [0, 1, '↓'], [-1, 0, '←'], [1, 0, '→'], [0, 0, '↧']];
  // ↧ = 받침대에 몸을 놓는다

  function parse(def) {
    const g = def.grid.map(r => r.split(''));
    const h = g.length, w = g[0].length;
    let start = null, core = -1;
    const foods = [], zone = [];
    const gates = [[], [], []];        // 🔴 문벽: gates[i] = i번 문이 열리면 사라지는 칸들
    // 🔴 별 'o' — 먹어도 **몸이 안 자란다**. 문 계산을 건드리면 안 되기 때문이다.
    //    안 먹어도 판은 깨진다. 별 둘/셋을 받으려면 먹어야 한다.
    let star = -1;
    // 🔴 받침대 'T' — 몸을 여기 두고 핵만 갈 수 있다. 두고 온 몸은 **딛고 설 지형**이 된다.
    //    "이 몸을 홈에 낼 것인가, 계단으로 쓸 것인가" 가 이 게임의 결정이다.
    //    아무 데나 놓게 하면 '어디에 놓았나'가 상태에 들어가 솔버가 터진다.
    //    자리를 정해두면 **받침대 칸 수만큼의 비트**로 끝난다.
    const pads = [];
    const boxes = [];      // 🔴 미는 상자의 처음 자리
    //  포탈 짝 — port[i] = [한쪽, 다른쪽]. i 번 홈을 채우면 켜진다
    const port = [[-1, -1], [-1, -1]];
    // 🔴 화살표 표지판 '^ v < >' — **순수 장식.** 퍼즐엔 아무 영향이 없다.
    //    가르치는 판에서 "여기서 이 키를 누르세요"를 맵 안에 박아두는 용도다.
    const signs = [];
    // 🔴 홈이 여러 개다. 기호 쌍마다 하나씩 —
    //    첫 홈 '=' '*' · 둘째 홈 '-' '%' · 셋째 홈 '~' '@'
    //    ('~' 은 접었던 무중력 구역 기호를 다시 쓴다)
    //    doors[i] = { cells: [...], core: n }
    const doors = [{ cells: [], core: -1 }, { cells: [], core: -1 }, { cells: [], core: -1 }];
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const c = g[y][x];
      if (c === 'S') { start = y * w + x; g[y][x] = '.'; }
      else if (c === '+') { foods.push(y * w + x); g[y][x] = '.'; }
      else if (c === '=') { doors[0].cells.push(y * w + x); g[y][x] = '.'; }
      else if (c === '*') { doors[0].cells.push(y * w + x); doors[0].core = y * w + x; g[y][x] = '.'; }
      else if (c === '-') { doors[1].cells.push(y * w + x); g[y][x] = '.'; }
      else if (c === '%') { doors[1].cells.push(y * w + x); doors[1].core = y * w + x; g[y][x] = '.'; }
      else if (c === '~') { doors[2].cells.push(y * w + x); g[y][x] = '.'; }
      else if (c === '@') { doors[2].cells.push(y * w + x); doors[2].core = y * w + x; g[y][x] = '.'; }
      // 🔴 문벽 — 그 문을 열기 전엔 벽, 열면 사라진다
      else if (c >= '1' && c <= '3') { gates[c.charCodeAt(0) - 49].push(y * w + x); g[y][x] = '.'; }
      // 🔴 출구 — 밟을 수 있는 바닥이다. 퍼즐엔 아무 영향이 없고, 게임이 여기서 맵을 넘긴다
      else if (c === 'o') { star = y * w + x; g[y][x] = '.'; }
      else if (c === 'T') { pads.push(y * w + x); g[y][x] = '.'; }
      //  🔴 무중력 구역 ':' — 벽이 아니라 **뒷벽**이다 (rev.10, 2026-09-05).
      //     지나갈 수 있고, 몸의 한 칸이라도 여기 걸쳐 있으면 안 떨어진다.
      //     중력이 이 게임의 축이라 그걸 국소적으로 끄면 오르기 규칙(길이 ≥ k+1)이 통째로 비켜간다.
      else if (c === ':') { zone.push(y * w + x); g[y][x] = '.'; }
      //  🔴 미는 상자 'B' — **가로로만** 민다. 떨어지고, 딛고 설 수 있다 (rev.10).
      //     자리가 상태에 들어가므로 솔버가 소코반이 된다 — 판을 작게 써야 한다.
      else if (c === 'B') { boxes.push(y * w + x); g[y][x] = '.'; }
      //  🔴 포탈 (rev.11) — 짝끼리 오간다. **그 짝의 홈을 채워야 켜진다.**
      //     P↔Q 는 홈 1 · p↔q 는 홈 2. 홈 셋짜리는 안 만든다 (채우면 판이 끝나서 쓸 데가 없다).
      else if (c === 'P') { port[0][0] = y * w + x; g[y][x] = '.'; }
      else if (c === 'Q') { port[0][1] = y * w + x; g[y][x] = '.'; }
      else if (c === 'p') { port[1][0] = y * w + x; g[y][x] = '.'; }
      else if (c === 'q') { port[1][1] = y * w + x; g[y][x] = '.'; }
      else if ('^v<>'.includes(c)) { signs.push({ cell: y * w + x, dir: '^v<>'.indexOf(c) }); g[y][x] = '.'; }
    }
    const open = doors.filter(d => d.cells.length);
    // 🔴 문마다 칸 수 제약은 clear에 달렸다.
    //    "any" — 아무 문이나 하나 채우고 끝난다. 길이는 한 번뿐이니 문마다 칸 수가 같아야 한다.
    //    "all" — 문을 차례로 채운다. 채울 때마다 몸을 두고 다시 먹으므로 **달라도 된다.**
    const clear = def.clear || 'any';
    if (clear === 'any') {
      for (const d of open)
        if (d.cells.length !== open[0].cells.length)
          throw new Error((def.id || '판') + ': 문마다 칸 수가 다르다 (' +
            open.map(x => x.cells.length).join(' vs ') + ') — 아무 문이나 여는 판은 길이가 하나뿐이다');
    }
    for (const d of open) d.set = new Set(d.cells);

    return {
      ...def, g, w, h, start, foods, doors: open, clear,
      boxes,
      //  칸 -> [나갈 칸, 몇 번 홈이 켜는가]
      portal: (() => {
        const m = new Map();
        for (let i = 0; i < port.length; i++) {
          const [a, b] = port[i];
          if (a < 0 || b < 0) continue;      // 짝이 하나만 있으면 안 쓴다
          m.set(a, [b, i]); m.set(b, [a, i]);
        }
        return m;
      })(),
      star,
      pads,
      signs,
      padIdx: new Map(pads.map((c, i) => [c, i])),
      gateSet: gates.map(cs => new Set(cs)),
      hasGates: gates.some(cs => cs.length),
      allDoors: (1 << open.length) - 1,
      // 아래 셋은 예전 이름 — 문이 하나뿐인 판을 위해 남겨둔다
      target: open.length ? open[0].cells : [],
      core: open.length ? open[0].core : -1,
      foodIdx: new Map(foods.map((c, i) => [c, i])),
      targetSet: open.length ? open[0].set : new Set(),
      zoneSet: new Set(zone),
    };
  }

  const isWall = (L, cell) => L.g[(cell / L.w) | 0][cell % L.w] === '#';

  /// 🔴 상자는 **딛고 설 수 있다** — 그게 이 기믹의 전부다 (발판을 스스로 만든다).
  ///    상자를 받쳐주는 것: 판 밖 · 벽 · 닫힌 문벽 · 굳은 몸 · 다른 상자 · 슬라임 몸.
  ///    ⚠️ 슬라임은 자기 몸을 못 딛지만 **상자는 슬라임을 딛는다.**
  ///       안 그러면 몸 위로 상자가 통과해 내려가 그림이 무너진다.
  function boxHeld(L, c, dm, bxs, bodySet) {
    const below = c + L.w;
    if (below >= L.w * L.h) return true;
    if (isBlocked(L, below, dm)) return true;
    if (spent(L, below, dm)) return true;
    if (bxs.has(below)) return true;
    if (bodySet.has(below)) return true;
    return false;
  }

  /// 🔴 지금 이 칸이 막혀 있나 — 문벽은 짝이 되는 문을 열면 사라진다.
  /// 🔴 이미 연 문의 칸인가 — 굳은 몸이 박혀 있다
  function spent(L, cell, dm) {
    for (let i = 0; i < L.doors.length; i++)
      if ((dm & (1 << i)) && L.doors[i].set.has(cell)) return true;
    return false;
  }

  function isBlocked(L, cell, dm) {
    if (isWall(L, cell)) return true;
    if (!L.hasGates) return false;
    for (let i = 0; i < L.gateSet.length; i++)
      if (L.gateSet[i].has(cell) && !(dm & (1 << i))) return true;
    return false;
  }

  /// 🔬 중력 실험 (L.gravity 가 켜져 있을 때만)
  /// 몸의 어느 칸이든 바로 아래가 벽이면 버틴다. 자기 몸은 지지대가 못 된다 —
  /// 뱀은 통째로 떨어진다(Snakebird와 같다).
  function supported(L, body, dm) {
    // 🔴 **포탈에 걸쳐 있는 동안은 안 떨어진다** (rev.11, 09-05 사장님 신고).
    //    몸이 포탈을 지나는 동안 두 덩이로 갈리는데, 중력은 몸을 **한 덩어리로** 떨어뜨린다.
    //    그래서 이쪽이 발판을 잃으면 **저쪽 머리까지 포탈 밖으로 흘러내렸다.**
    //    (1-2 에서: 머리가 Q 로 나왔는데 다음 걸음에 한 칸 내려앉았다)
    //    갈려 있는 동안은 포탈이 붙들고 있는 것으로 본다 — 다 나오면 다시 중력을 받는다.
    if (L.portal && L.portal.size) {
      for (let i = 1; i < body.length; i++) {
        const a = body[i - 1], b = body[i];
        const dx = Math.abs((a % L.w) - (b % L.w));
        const dy = Math.abs(((a / L.w) | 0) - ((b / L.w) | 0));
        if (dx + dy !== 1) return true;                 // 사슬이 끊겨 있다 = 걸쳐 있다
      }
    }
    for (const c of body) {
      // 🔴 무중력 구역(':') — 벽이 아니라 **뒷벽**이다. 지나갈 수 있고, 그 안에선 안 떨어진다.
      //    '지지대'의 뜻을 넓힌 것이다. rev.10 에서 되살렸다 ('~' 는 홈 셋이 가져갔다).
      if (L.zoneSet.has(c)) return true;
      const below = c + L.w;
      if (below >= L.w * L.h || isBlocked(L, below, dm)) return true;
      // 🔴 두고 온 몸은 **디딤돌**이다 (2026-08-30).
      //    이미 연 문의 칸에는 굳은 몸이 박혀 있다. 지나갈 수는 있지만 **딛고 설 수도 있다.**
      //    이게 없으면 문을 연 뒤 길이 1이 된 핵은 한 칸도 못 올라 갇힌다.
      if (spent(L, below, dm)) return true;
      // 받침대에 놓고 온 몸도 디딤돌이다
      if (L.padIdx.has(below) && ((body.pm || 0) & (1 << L.padIdx.get(below)))) return true;
      // 🔴 상자 위에는 설 수 있다 (rev.10)
      if (body.bxs && body.bxs.has(below)) return true;
    }
    return false;
  }

  /// 지지될 때까지 떨어뜨린다. 떨어질 데가 없으면(판 밖) null = 그 걸음은 불가.
  ///
  /// 🔴 떨어지는 동안에도 **머리가 지나가는 조각을 먹는다** (2026-08-29).
  ///    전에는 걸음에만 구현돼 있어서 조각 위로 떨어지면 안 먹혔다. 사람이 바로 부딪혔다.
  ///    다만 낙하 중엔 꼬리가 물러날 자리가 없다 — 거기에 마디를 붙이면 몸이 겹친다.
  ///    그래서 **다음 걸음에 꼬리가 안 물러나는 것**으로 갚는다 (고전 스네이크와 같다).
  ///    { body, fm, pg } 를 돌려준다. pg = 아직 안 갚은 성장 횟수.
  function settle(L, body, fm, pg, dm, sc, pm, bx) {
    bx = (bx || []).slice();
    body.pm = pm || 0;                 // supported() 가 받침대를 보게 들려 보낸다
    body.bxs = new Set(bx);            // 상자도 같이 들려 보낸다
    sc = sc || 0;
    if (L.star >= 0 && body[0] === L.star) sc = 1;
    const done = () => ({ body: body, fm: fm, pg: pg, sc: sc, bx: bx });
    if (!L.gravity) return done();
    for (let guard = 0; guard < 128; guard++) {
      body.pm = pm || 0;
      body.bxs = new Set(bx);
      if (!supported(L, body, dm)) {
        const next = body.map(c => c + L.w);
        for (const c of next) if (c >= L.w * L.h) return null;
        next.pm = pm || 0; next.bxs = body.bxs;
        body = next;
        const fi = L.foodIdx.get(body[0]);        // 머리가 새로 들어간 칸
        if (fi !== undefined && !(fm & (1 << fi))) { fm |= (1 << fi); pg++; }
        if (L.star >= 0 && body[0] === L.star) sc = 1;   // 떨어지며 지나가도 줍는다
        continue;
      }
      //  🔴 몸이 멈춘 뒤에 상자를 떨어뜨린다. **아래 것부터** — 아래가 비켜야 위가 내려온다.
      if (!bx.length) return done();
      const bxs = body.bxs, bodySet = new Set(body);
      let moved = false;
      const order = bx.slice().sort((a, b) => b - a);
      for (const c of order) {
        if (boxHeld(L, c, dm, bxs, bodySet)) continue;
        const below = c + L.w;
        if (below >= L.w * L.h) return null;
        bx = bx.map(v => (v === c ? below : v));
        moved = true;
        break;
      }
      if (!moved) return done();
    }
    return null;
  }

  /// 상태: { body: [머리, ..., 꼬리], fm }
  const startState = L => {
    const r = settle(L, [L.start], 0, 0, 0, 0, 0, L.boxes);
    const st = r || { body: [L.start], fm: 0, pg: 0, sc: 0, bx: (L.boxes || []).slice() };
    st.pm = 0;
    st.dm = 0;                       // 🔴 열린 문 비트마스크
    st.bx = st.bx || (L.boxes || []).slice();
    return st;
  };

  /// 🔴 받침대에 몸을 놓는다. **몸 전체가 받침대 안에** 있어야 하고, 통째로 놓는다.
  ///    일부만 떼게 하면 덤 조각을 먹고도 살아나서 난이도가 통째로 사라진다.
  ///    놓고 나면 핵(머리)만 남고, 그 자리에서 다시 떨어질 수 있다.
  function drop(L, st) {
    if (!L.pads.length || st.body.length < 2) return null;
    let mask = 0;
    for (const c of st.body) {
      const i = L.padIdx.get(c);
      if (i === undefined) return null;              // 하나라도 밖이면 못 놓는다
      if ((st.pm || 0) & (1 << i)) return null;      // 이미 찬 자리엔 못 놓는다
      mask |= 1 << i;
    }
    const pm = (st.pm || 0) | mask;
    const r = settle(L, [st.body[0]], st.fm, 0, st.dm || 0, st.sc || 0, pm, st.bx);
    if (!r) return null;
    return { body: r.body, fm: r.fm, pg: r.pg, sc: r.sc, dm: st.dm || 0, pm, bx: r.bx };
  }

  function step(L, st, di) {
    if (di === 4) return drop(L, st);
    const [dx, dy] = DIRS[di];
    const hx = st.body[0] % L.w, hy = (st.body[0] / L.w) | 0;
    const nx = hx + dx, ny = hy + dy;
    if (nx < 0 || ny < 0 || nx >= L.w || ny >= L.h) return null;
    if (isBlocked(L, ny * L.w + nx, st.dm || 0)) return null;

    // 🔴 포탈 (rev.11) — **걸어 들어갈 때만** 통한다.
    //    ⚠️ 떨어져 들어가도 통하게 하면 *떨어짐 → 나감 → 또 떨어짐* 의 무한 고리가 된다.
    //       그래서 settle() 은 포탈을 안 본다. 포탈 위에 내려앉는 건 그냥 그 칸에 서는 것이다.
    //    ⚠️ 한 번만 통한다 — 나간 자리가 또 포탈이어도 다시 안 보낸다.
    let nh = ny * L.w + nx;
    let warped = false;
    if (L.portal.size) {
      const hit = L.portal.get(nh);
      //  그 짝의 홈을 채웠을 때만 켜진다. 안 켜졌으면 그냥 바닥이다.
      if (hit && ((st.dm || 0) & (1 << hit[1]))) {
        nh = hit[0];
        warped = true;
        if (isBlocked(L, nh, st.dm || 0)) return null;
      }
    }

    // 🔴 상자를 민다 — **가로로만.** 위아래로 밀면 중력과 뜻이 겹쳐 읽히지 않는다.
    let bx = st.bx || [];
    if (bx.length) {
      const bxs0 = new Set(bx);
      if (bxs0.has(nh)) {
        if (di !== 2 && di !== 3) return null;          // 위·아래로는 못 민다
        //  🔴 포탈로 나온 자리에서는 못 민다 — 어느 쪽으로 미는지가 뜻을 잃는다
        if (nh !== ny * L.w + nx) return null;
        const tx = nx + dx;
        if (tx < 0 || tx >= L.w) return null;
        const tc = ny * L.w + tx;
        if (isBlocked(L, tc, st.dm || 0)) return null;
        if (spent(L, tc, st.dm || 0)) return null;      // 굳은 몸이 박힌 칸엔 못 넣는다
        if (bxs0.has(tc)) return null;                  // 상자 둘은 한 번에 못 민다
        for (const c of st.body) if (c === tc) return null;
        bx = bx.map(v => (v === nh ? tc : v));
      }
    }

    const fi = L.foodIdx.get(nh);
    const grows = fi !== undefined && !(st.fm & (1 << fi));

    // 자기 몸: 꼬리는 비켜나니 밟아도 된다 — 단 이번에 자라지 않을 때만
    const blocked = grows ? st.body.length : st.body.length - 1;
    for (let i = 0; i < blocked; i++) if (st.body[i] === nh) return null;

    let body = [nh, ...st.body];
    let pg = st.pg || 0;
    // 자라는 걸음이면 꼬리를 그대로 둔다. 아니면 낙하 중에 진 빚(pg)부터 갚는다.
    if (!grows) { if (pg > 0) pg--; else body.pop(); }

    let sc = st.sc || 0;
    if (L.star >= 0 && nh === L.star) sc = 1;

    const r = settle(L, body, grows ? (st.fm | (1 << fi)) : st.fm, pg, st.dm || 0, sc, st.pm || 0, bx);
    if (!r) return null;
    const ns = { body: r.body, fm: r.fm, pg: r.pg, sc: r.sc, dm: st.dm || 0, pm: st.pm || 0, bx: r.bx };

    // 🔴 **포탈로 나온 자리에서 도로 떨어져 오면 그 걸음은 막힌다** (rev.11, 09-05 사장님:
    //    "포탈을 밑에서 위로 가면 이상해"). 아래에서 위로 들어가면 나온 자리가 허공이라
    //    그대로 떨어져 **출발 자리로 돌아온다** — 판 반대편으로 번쩍했다 끌려오는 걸로 보인다.
    //    ⚠️ 그리고 몸이 길면 떨어진 자리가 **제 몸과 겹친다.** 자기 몸 검사는 포탈 나온 칸에서
    //       했지 떨어진 뒤 자리에서는 안 했기 때문이다. 둘 다 여기서 막는다.
    if (warped) {
      if (ns.body[0] === st.body[0]) return null;              // 아무것도 안 바뀐 걸음
      for (let i = 1; i < ns.body.length; i++)                 // 몸이 겹쳤다
        for (let j = 0; j < i; j++) if (ns.body[i] === ns.body[j]) return null;
      // 🔴 **사슬은 한 군데만 끊길 수 있다** (09-05 사장님).
      //    몸이 포탈에 걸쳐 있는데 머리가 **또** 포탈로 들어가면 사슬이 두 군데 끊긴다:
      //      ㅁㅁ(포탈)ㅁㅇ  ← 왼쪽 →  ㅁㅇ(포탈)ㅁㅁ
      //    한 몸이 판 여기저기에 흩어지는 셈이라 무슨 일이 난 건지 안 읽힌다.
      //    한 번 걸친 동안은 **다 나올 때까지** 다시 못 들어간다.
      let breaks = 0;
      for (let i = 1; i < ns.body.length; i++) {
        const a = ns.body[i - 1], b = ns.body[i];
        const dx = Math.abs((a % L.w) - (b % L.w));
        const dy = Math.abs(((a / L.w) | 0) - ((b / L.w) | 0));
        if (dx + dy !== 1) breaks++;
      }
      if (breaks > 1) return null;
    }

    // 🔴 문이 채워졌나 — 채워졌으면 **몸을 자물쇠에 두고 핵만 남는다.**
    //    (몸이 문 칸을 정확히 덮고 있으므로, 남는 몸의 모양은 곧 그 문이다)
    const opened = matchDoor(L, ns);
    if (opened >= 0 && !(ns.dm & (1 << opened))) {
      ns.dm |= (1 << opened);
      if (L.clear === 'all') {
        ns.body = [L.doors[opened].core >= 0 ? L.doors[opened].core : ns.body[0]];
        ns.pg = 0;                   // 두고 온 몸과 함께 미룬 성장도 사라진다
      }
      // 🔴 문벽이 사라지면 발밑이 없어질 수 있다 — 그러면 떨어진다
      const after = settle(L, ns.body, ns.fm, ns.pg, ns.dm, ns.sc, ns.pm, ns.bx);
      if (!after) return null;
      ns.body = after.body; ns.fm = after.fm; ns.pg = after.pg; ns.sc = after.sc; ns.bx = after.bx;
    }
    return ns;
  }

  /// 지금 몸이 어느 문에 딱 맞나. 아니면 -1.
  /// 몸이 그 문의 칸을 '정확히' 덮고(남아도 모자라도 안 된다) 핵이 심에 있어야 한다.
  function matchDoor(L, st) {
    for (let i = 0; i < L.doors.length; i++) {
      const d = L.doors[i];
      if (st.body.length !== d.cells.length) continue;
      let all = true;
      for (const c of st.body) if (!d.set.has(c)) { all = false; break; }
      if (!all) continue;
      if (d.core >= 0 && st.body[0] !== d.core) continue;
      return i;
    }
    return -1;
  }
  /// 이 판이 끝났나.
  ///   any — 아무 문이나 하나 열면 끝 (지금까지의 판)
  ///   all — 🔴 문을 다 열어야 끝. 열 때마다 몸을 두고 핵만 간다
  function isWin(L, st, needStar) {
    const dm = st.dm || 0;
    const doors = L.clear === 'all'
      ? (dm === L.allDoors && L.allDoors !== 0)
      : (dm !== 0 || matchDoor(L, st) >= 0);
    if (!doors) return false;
    // 🔴 별 둘/셋을 노리는 계산에서만 별을 요구한다. 깨는 것 자체엔 필요 없다.
    if (needStar && L.star >= 0 && !(st.sc || 0)) return false;
    // 🔴 홈을 채우면 **거기서 끝난다.** 출구까지 걸어가는 건 없앴다 (08-30 사장님).
    //    출구 그림은 남아 있지만 연출일 뿐이다 — 밟으러 갈 필요가 없다.
    return true;
  }
  /// 예전 이름 — 어느 문이 열렸는지 물을 때
  const wonDoor = (L, st) => matchDoor(L, st);

  //  🔴 상자 자리도 상태다 — 이것 때문에 판이 커지면 솔버가 소코반처럼 터진다.
  const keyOf = st => st.body.join(',') + '|' + st.fm + '|' + (st.pg || 0) + '|' +
                      (st.dm || 0) + '|' + (st.sc || 0) + '|' + (st.pm || 0) +
                      ((st.bx && st.bx.length) ? '|' + st.bx.join('.') : '');

  function solve(def, opts) {
    const needStar = !!(opts && opts.needStar);
    const L = parse(def);
    if (L.start === null) return { ok: false, why: 'S가 없음' };
    if (!L.target.length) return { ok: false, why: '목표(=)가 없음' };
    if (L.foods.length > 26) return { ok: false, why: '먹이가 26개를 넘음' };
    // 🔴 조각이 맞아야 애초에 풀 수 있다.
    //    any — 문 하나만 채운다: 조각 = 칸 수 - 1
    //    all — 문마다 채우고 몸을 두고 간다: 조각 = 문마다 (칸 수 - 1)의 합
    const need = L.clear === 'all'
      ? L.doors.reduce((s, d) => s + d.cells.length - 1, 0)
      : L.target.length - 1;
    // 🔴 모자라면 못 푼다. 하지만 **남는 건 괜찮다** — 그게 곧 "먹으면 안 되는 조각"이다.
    //    (여기를 '정확히 같아야 한다'로 묶어둔 게 난이도의 족쇄였다. 08-30)
    //    더 먹으면 몸이 길어져 홈에 안 맞고, 먹은 건 못 뱉는다 = 그 자리에서 진다.
    if (L.foods.length < need)
      return { ok: false, why: `조각이 모자란다 — ${need}개가 필요한데 ${L.foods.length}개다` };

    const s0 = startState(L);
    if (isWin(L, s0, needStar)) return { ok: true, moves: 0, path: '', shortest: 1, states: 1 };

    //  🔴 **상한을 둔다** (rev.10, 2026-09-05). 상자가 들어오면서 상태가 터질 수 있게 됐다 —
    //     상한이 없으면 node 가 메모리를 다 먹고 죽어 **돌던 일 전체가 날아간다.**
    //     넘으면 "못 풀었다" 가 아니라 **"너무 크다"** 로 돌려준다. 둘은 다른 말이다.
    const cap = (opts && opts.cap) || +(typeof process !== 'undefined' && process.env && process.env.SOLVE_CAP) || 3000000;
    const q = [s0];
    const dist = new Map([[keyOf(s0), 0]]);
    const prev = new Map([[keyOf(s0), null]]);
    let goal = null, gd = Infinity, head = 0;
    const wins = [];

    while (head < q.length) {
      if (dist.size > cap) return { ok: false, why: '너무 큼', states: dist.size, capped: true };
      const st = q[head++];
      const sk = keyOf(st), d = dist.get(sk);
      if (d >= gd) continue;
      const acts = L.pads.length ? 5 : 4;
      for (let di = 0; di < acts; di++) {
        const ns = step(L, st, di);
        if (!ns) continue;
        const nk = keyOf(ns), win = isWin(L, ns, needStar);
        if (dist.has(nk)) { if (win && dist.get(nk) === d + 1) wins.push(nk); continue; }
        dist.set(nk, d + 1); prev.set(nk, [sk, DIRS[di][2]]);
        if (win) { if (d + 1 < gd) { gd = d + 1; goal = nk; } wins.push(nk); continue; }
        q.push(ns);
      }
    }
    if (!goal) return { ok: false, why: '해가 없음', states: dist.size };

    const path = [];
    let cur = goal;
    while (prev.get(cur)) { const [p, sym] = prev.get(cur); path.push(sym); cur = p; }
    path.reverse();
    return {
      ok: true, moves: gd, path: path.join(''), states: dist.size,
      shortest: wins.filter(k => dist.get(k) === gd).length,
    };
  }

  function trace(def) {
    const L = parse(def);
    const r = solve(def);
    let st = startState(L);
    const steps = [{ st, sym: '시작' }];
    const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };
    for (const c of (r.path || '')) {
      st = step(L, st, SYM[c]);
      if (!st) break;
      steps.push({ st, sym: c });
    }
    return { L, steps, r };
  }

  function render(L, st) {
    const head = st.body[0], body = new Set(st.body.slice(1));
    const rows = [];
    for (let y = 0; y < L.h; y++) {
      let r = '';
      for (let x = 0; x < L.w; x++) {
        const c = y * L.w + x;
        const fi = L.foodIdx.get(c);
        if (c === head) r += 'O';
        else if (body.has(c)) r += 'o';
        else if (L.g[y][x] === '#') r += '#';
        else if (L.hasGates && L.gateSet.some((s, i) => s.has(y * L.w + x) && !((st.dm || 0) & (1 << i))))
          r += L.gateSet.findIndex(s => s.has(y * L.w + x)) + 1;   // 열린 문벽은 안 그린다
        else if (y * L.w + x === L.star) r += 'o';
        else if (L.padIdx.has(y * L.w + x))
          r += ((st.pm || 0) & (1 << L.padIdx.get(y * L.w + x))) ? 'Ⅰ' : 'T';
        else if (st && st.bx && st.bx.indexOf(y * L.w + x) >= 0) r += 'B';
        else if (L.portal.has(y * L.w + x)) {
          const [, gi] = L.portal.get(y * L.w + x);
          //  켜졌으면 큰 P, 아니면 작은 p (보여주기용이라 짝 구분은 안 한다)
          r += ((st && (st.dm || 0)) & (1 << gi)) ? 'P' : 'p';
        }
        else if (L.zoneSet.has(y * L.w + x)) r += ':';
        else if (L.doors[1] && L.doors[1].set.has(y * L.w + x))
          r += (y * L.w + x) === L.doors[1].core ? '%' : '-';
        else if (L.doors[2] && L.doors[2].set.has(y * L.w + x))
          r += (y * L.w + x) === L.doors[2].core ? '@' : '~';
        else if (fi !== undefined && !(st.fm & (1 << fi))) r += '+';
        else if (c === L.core) r += '*';
        else if (L.targetSet.has(c)) r += '=';
        else r += '·';
      }
      rows.push(r);
    }
    return rows;
  }

  root.SlimeEngine = { parse, startState, step, isWin, wonDoor, matchDoor, isBlocked, keyOf, solve, trace, render, settle, supported, DIRS };
})(typeof module !== 'undefined' && module.exports ? module.exports : window);
