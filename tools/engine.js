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

  const DIRS = [[0, -1, '↑'], [0, 1, '↓'], [-1, 0, '←'], [1, 0, '→']];

  function parse(def) {
    const g = def.grid.map(r => r.split(''));
    const h = g.length, w = g[0].length;
    let start = null, core = -1;
    const foods = [], target = [], zone = [];
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const c = g[y][x];
      if (c === 'S') { start = y * w + x; g[y][x] = '.'; }
      else if (c === '+') { foods.push(y * w + x); g[y][x] = '.'; }
      else if (c === '=') { target.push(y * w + x); g[y][x] = '.'; }
      else if (c === '*') { target.push(y * w + x); core = y * w + x; g[y][x] = '.'; }
      else if (c === '~') { zone.push(y * w + x); g[y][x] = '.'; }   // 🔬 무중력 구역
    }
    return {
      ...def, g, w, h, start, foods, target, core,
      foodIdx: new Map(foods.map((c, i) => [c, i])),
      targetSet: new Set(target),
      zoneSet: new Set(zone),
    };
  }

  const isWall = (L, cell) => L.g[(cell / L.w) | 0][cell % L.w] === '#';

  /// 🔬 중력 실험 (L.gravity 가 켜져 있을 때만)
  /// 몸의 어느 칸이든 바로 아래가 벽이면 버틴다. 자기 몸은 지지대가 못 된다 —
  /// 뱀은 통째로 떨어진다(Snakebird와 같다).
  function supported(L, body) {
    for (const c of body) {
      // 🔬 무중력 구역('~') — 벽이 아니라 **뒷벽**이다. 지나갈 수 있고, 그 안에선 안 떨어진다.
      //    규칙을 늘린 게 아니라 '지지대'의 뜻을 넓힌 것뿐이다.
      if (L.zoneSet.has(c)) return true;
      const below = c + L.w;
      if (below >= L.w * L.h || isWall(L, below)) return true;
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
  function settle(L, body, fm, pg) {
    if (!L.gravity) return { body: body, fm: fm, pg: pg };
    for (let guard = 0; guard < 64; guard++) {
      if (supported(L, body)) return { body: body, fm: fm, pg: pg };
      const next = body.map(c => c + L.w);
      for (const c of next) if (c >= L.w * L.h) return null;
      body = next;
      const fi = L.foodIdx.get(body[0]);        // 머리가 새로 들어간 칸
      if (fi !== undefined && !(fm & (1 << fi))) { fm |= (1 << fi); pg++; }
    }
    return null;
  }

  /// 상태: { body: [머리, ..., 꼬리], fm }
  const startState = L => {
    const r = settle(L, [L.start], 0, 0);
    return r || { body: [L.start], fm: 0, pg: 0 };
  };

  function step(L, st, di) {
    const [dx, dy] = DIRS[di];
    const hx = st.body[0] % L.w, hy = (st.body[0] / L.w) | 0;
    const nx = hx + dx, ny = hy + dy;
    if (nx < 0 || ny < 0 || nx >= L.w || ny >= L.h) return null;
    const nh = ny * L.w + nx;
    if (isWall(L, nh)) return null;

    const fi = L.foodIdx.get(nh);
    const grows = fi !== undefined && !(st.fm & (1 << fi));

    // 자기 몸: 꼬리는 비켜나니 밟아도 된다 — 단 이번에 자라지 않을 때만
    const blocked = grows ? st.body.length : st.body.length - 1;
    for (let i = 0; i < blocked; i++) if (st.body[i] === nh) return null;

    let body = [nh, ...st.body];
    let pg = st.pg || 0;
    // 자라는 걸음이면 꼬리를 그대로 둔다. 아니면 낙하 중에 진 빚(pg)부터 갚는다.
    if (!grows) { if (pg > 0) pg--; else body.pop(); }

    const r = settle(L, body, grows ? (st.fm | (1 << fi)) : st.fm, pg);
    if (!r) return null;
    return { body: r.body, fm: r.fm, pg: r.pg };
  }

  /// 몸이 목표 칸을 '정확히' 덮었는가 (남아도 모자라도 안 된다)
  function isWin(L, st) {
    if (st.body.length !== L.target.length) return false;
    for (const c of st.body) if (!L.targetSet.has(c)) return false;
    if (L.core >= 0 && st.body[0] !== L.core) return false;   // 머리가 심에 있어야 한다
    return true;
  }

  const keyOf = st => st.body.join(',') + '|' + st.fm + '|' + (st.pg || 0);

  function solve(def) {
    const L = parse(def);
    if (L.start === null) return { ok: false, why: 'S가 없음' };
    if (!L.target.length) return { ok: false, why: '목표(=)가 없음' };
    if (L.foods.length > 26) return { ok: false, why: '먹이가 26개를 넘음' };
    if (L.target.length !== L.foods.length + 1)
      return { ok: false, why: `길이가 안 맞는다 — 먹이 ${L.foods.length}개면 최대 길이 ${L.foods.length + 1}, 목표는 ${L.target.length}칸` };

    const s0 = startState(L);
    if (isWin(L, s0)) return { ok: true, moves: 0, path: '', shortest: 1, states: 1 };

    const q = [s0];
    const dist = new Map([[keyOf(s0), 0]]);
    const prev = new Map([[keyOf(s0), null]]);
    let goal = null, gd = Infinity, head = 0;
    const wins = [];

    while (head < q.length) {
      const st = q[head++];
      const sk = keyOf(st), d = dist.get(sk);
      if (d >= gd) continue;
      for (let di = 0; di < 4; di++) {
        const ns = step(L, st, di);
        if (!ns) continue;
        const nk = keyOf(ns), win = isWin(L, ns);
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
    const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3 };
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
        else if (L.zoneSet.has(y * L.w + x)) r += '~';
        else if (fi !== undefined && !(st.fm & (1 << fi))) r += '+';
        else if (c === L.core) r += '*';
        else if (L.targetSet.has(c)) r += '=';
        else r += '·';
      }
      rows.push(r);
    }
    return rows;
  }

  root.SlimeEngine = { parse, startState, step, isWin, keyOf, solve, trace, render, settle, supported, DIRS };
})(typeof module !== 'undefined' && module.exports ? module.exports : window);
