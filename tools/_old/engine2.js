/* 🔬 실험 엔진 — 몸이 (가로, 세로) 두 값이다.
   지금 게임이 쓰는 tools/engine.js 는 안 건드린다. 이게 좋으면 그때 옮긴다.

   규칙 (2026-08-28)
   - 몸은 w×h 직사각형. (x, y) = 왼쪽 아래. 발밑을 딛고 위로 선다
   - 🔴 시작은 무조건 **1×1** · 사다리 바닥은 **1×0.5**(납작한 웅덩이)
   - 🔴 **이동은 공짜다.** 몸은 판 위의 것을 덮었을 때만 변한다
   - 판 위의 것 넷 (덮으면 한 번 발동하고 사라진다):
       '^' 세로 +1    '>' 가로 +1    'v' 세로 −1    '<' 가로 −1
     · 세로는 0.5 ↔ 1 ↔ 2 …  · 가로는 1이 바닥
     · 0.5도 칸 판정은 한 칸(올림)이다 — 반 칸짜리 틈은 없다
   - 중력: 발밑이 비면 떨어진다. 낙하는 공짜
   - 오르기: **세로 높이의 정수부**만큼 턱을 오른다 (1×0.5는 하나도 못 오른다)
   - 몸은 가는 쪽으로 흐른다: 가로가 변하면 앞面을 기준으로 다시 앉는다
   - 몸이 안 들어가는 걸음은 불가 (죽지 않는다. 못 갈 뿐)
   - 덩어리가 출구 칸을 덮으면 클리어

   ⚠️ shrinkOnMove / shrinkEvery / floorWalks 는 **옛 규칙(이동이 몸을 깎던 것)**의 잔재다.
      기본은 꺼져 있다. 비교 실험용으로만 남겨 뒀다.
*/
(function (root) {
  'use strict';

  /// 판에 놓이는 것 네 가지. 덮으면 한 번 발동하고 사라진다.
  const KINDS = { '^': 'up', '>': 'wide', 'v': 'down', '<': 'narrow' };

  function parse(def) {
    const g = def.grid.map(r => r.split(''));
    const h = g.length, w = g[0].length;
    let start = null, exit = null;
    const items = [], kinds = [], target = [];
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const c = g[y][x];
      if (c === 'S') { start = [x, y]; g[y][x] = '.'; }
      else if (c === 'E') { exit = [x, y]; g[y][x] = '.'; }
      else if (c === '=') { target.push(y * w + x); g[y][x] = '.'; }
      else if (KINDS[c]) { items.push(y * w + x); kinds.push(KINDS[c]); g[y][x] = '.'; }
    }

    // 🔴 목표가 '='이면 **그 칸을 정확히 채우는 것**이 클리어다.
    //    덮기만 하면 되는 출구와 달리 **끝날 때 몸 크기가 퍼즐의 일부**가 된다.
    let goal = null;
    if (target.length) {
      const xs = target.map(c => c % w), ys = target.map(c => (c / w) | 0);
      const x0 = Math.min(...xs), x1 = Math.max(...xs);
      const y0 = Math.min(...ys), y1 = Math.max(...ys);
      const tw = x1 - x0 + 1, th = y1 - y0 + 1;
      if (tw * th !== target.length) throw new Error('목표 칸이 직사각형이 아니다');
      goal = { x: x0, y: y1, w: tw, h: th };     // y는 아랫줄 (몸과 같은 기준)
    }

    return {
      ...def, g, w, h, start, exit, items, target, goal,
      kind: new Map(items.map((c, i) => [c, kinds[i]])),
      idx: new Map(items.map((c, i) => [c, i])),
    };
  }

  const isWall = (L, x, y) => x < 0 || y < 0 || x >= L.w || y >= L.h || L.g[y][x] === '#';

  /// 세로 h는 0.5를 가질 수 있다(가장 납작한 상태). 칸 판정은 올림 — 0.5도 한 칸은 필요하다.
  const cellsH = h => Math.ceil(h);

  function fits(L, x, y, w, h) {
    const ch = cellsH(h);
    for (let i = 0; i < w; i++) for (let j = 0; j < ch; j++) if (isWall(L, x + i, y - j)) return false;
    return true;
  }

  function covered(L, x, y, w, h) {
    const out = [], ch = cellsH(h);
    for (let i = 0; i < w; i++) for (let j = 0; j < ch; j++) out.push((y - j) * L.w + (x + i));
    return out;
  }

  /// 가로가 w -> w2로 바뀔 때 어디에 앉을지. 가는 쪽 앞面을 살린다.
  function reseatX(L, x, y, w, w2, h, dir) {
    const keepLeft = x, keepRight = (x + w - 1) - w2 + 1;
    const order = dir > 0
      ? (w2 > w ? [keepLeft, keepRight] : [keepRight, keepLeft])
      : (w2 > w ? [keepRight, keepLeft] : [keepLeft, keepRight]);
    for (const nx of order) if (fits(L, nx, y, w2, h)) return nx;
    return null;
  }

  /// 떨어지고 -> 먹고 -> 몸이 변하고 를 안정될 때까지
  function settle(L, st, dir) {
    let { x, y, w, h, fm } = st;
    let sc = st.sc || 0;
    dir = dir || 0;
    for (let guard = 0; guard < 32; guard++) {
      let fell = false;
      while (fits(L, x, y + 1, w, h)) { y++; fell = true; }

      let dw = 0, dh = 0, ate = false;
      for (const c of covered(L, x, y, w, h)) {
        const i = L.idx.get(c);
        if (i === undefined || (fm & (1 << i))) continue;
        fm |= 1 << i; ate = true;
        switch (L.kind.get(c)) {
          case 'up': dh++; break;
          case 'down': dh--; break;
          case 'wide': dw++; break;
          case 'narrow': dw--; break;
        }
      }
      if (!ate) { if (fell) continue; return { x, y, w, h, fm, sc }; }

      sc = 0;
      // 세로는 발밑을 딛고 오르내린다. 0.5 ↔ 1 ↔ 2 ...  0.5가 바닥이다.
      for (let k = 0; k < dh; k++) h = (h === 0.5 ? 1 : h + 1);
      for (let k = 0; k > dh; k--) h = (h > 1 ? h - 1 : 0.5);
      if (!fits(L, x, y, w, h)) return null;

      const w2 = Math.max(1, w + dw);            // 가로는 1이 바닥
      if (w2 !== w) {
        const nx = reseatX(L, x, y, w, w2, h, dir);
        if (nx === null) return null;
        x = nx; w = w2;
      }
    }
    return null;
  }

  const startState = L => settle(L, { x: L.start[0], y: L.start[1], w: 1, h: 1, fm: 0, sc: 0 }, 0);

  /// 한 걸음 뒤의 (w, h). 큰 쪽부터 줄고, 동점이면 세로부터.
  /// 🔴 사다리 맨 아래는 1×0.5 — 거기서 더 줄어야 하면 null(그 걸음이 막힌다).
  /// floorWalks=true 면 1×0.5에서 더 안 줄고 "걷기는 된다" (걸음이 안 막힌다).
  function shrink(w, h, floorWalks) {
    if (h >= w && h > 0.5) return [w, h === 1 ? 0.5 : h - 1];
    if (w > 1) return [w - 1, h];
    return floorWalks ? [w, h] : null;           // 1×0.5 — 사다리 바닥
  }

  function move(L, st, dx) {
    // 같은 몸으로 옆(또는 턱 위)에 설 자리 — 세로 높이만큼 오른다
    let hy = null;
    for (let up = 0; up <= Math.floor(st.h); up++) {
      if (fits(L, st.x + dx, st.y - up, st.w, st.h)) { hy = st.y - up; break; }
    }
    if (hy === null) return null;

    // 🔴 이동은 공짜다 — 몸은 판 위의 것을 덮었을 때만 변한다.
    //    (shrinkOnMove를 켜면 옛 규칙: shrinkEvery 걸음마다 한 겹씩 준다)
    let w2 = st.w, h2 = st.h, sc2 = st.sc || 0;
    if (L.shrinkOnMove) {
      const sc = sc2 + 1;
      const due = sc >= (L.shrinkEvery || 1);
      const shrunk = due ? shrink(st.w, st.h, L.floorWalks) : [st.w, st.h];
      if (!shrunk) return null;
      [w2, h2] = shrunk; sc2 = due ? 0 : sc;
    }
    const x2 = dx > 0 ? (st.x + dx + st.w - 1) - w2 + 1 : st.x + dx;
    if (!fits(L, x2, hy, w2, h2)) return null;

    return settle(L, { x: x2, y: hy, w: w2, h: h2, fm: st.fm, sc: sc2 }, dx);
  }

  /// 목표가 있으면 **정확히 채워야** 클리어. 없으면 출구를 덮으면 클리어.
  function isWin(L, st) {
    if (L.goal) {
      const g = L.goal;
      return st.x === g.x && st.y === g.y && st.w === g.w && cellsH(st.h) === g.h;
    }
    return covered(L, st.x, st.y, st.w, st.h).includes(L.exit[1] * L.w + L.exit[0]);
  }
  const keyOf = st => `${st.x},${st.y},${st.w},${st.h},${st.fm},${st.sc||0}`;

  function solve(def) {
    const L = parse(def);
    if (!L.start) return { ok: false, why: 'S가 없음' };
    if (!L.exit && !L.goal) return { ok: false, why: 'E도 = 도 없음' };
    if (L.items.length > 26) return { ok: false, why: '판 위의 것이 26개를 넘음' };

    const s0 = startState(L);
    if (!s0) return { ok: false, why: '시작하자마자 막힘' };
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
      for (const [dx, sym] of [[-1, '←'], [1, '→']]) {
        const ns = move(L, st, dx);
        if (!ns) continue;
        const nk = keyOf(ns), win = isWin(L, ns);
        if (dist.has(nk)) { if (win && dist.get(nk) === d + 1) wins.push(nk); continue; }
        dist.set(nk, d + 1); prev.set(nk, [sk, sym]);
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

  /// 정답을 밟으며 몸이 어떻게 변하는지 (판을 그려서 눈으로 본다)
  function trace(def) {
    const L = parse(def);
    let st = startState(L);
    const steps = [{ st, sym: '시작' }];
    for (const c of (solve(def).path || '')) {
      st = move(L, st, c === '→' ? 1 : -1);
      if (!st) break;
      steps.push({ st, sym: c });
    }
    return { L, steps };
  }

  function render(L, st) {
    const body = new Set(covered(L, st.x, st.y, st.w, st.h));
    const rows = [];
    for (let y = 0; y < L.h; y++) {
      let r = '';
      for (let x = 0; x < L.w; x++) {
        const c = y * L.w + x;
        const i = L.idx.get(c);
        if (body.has(c)) r += '@';
        else if (L.g[y][x] === '#') r += '#';
        else if (L.target.includes(c)) r += '=';
        else if (L.exit && L.exit[0] === x && L.exit[1] === y) r += 'E';
        else if (i !== undefined && !(st.fm & (1 << i))) r += { up:'^', wide:'>', down:'v', narrow:'<' }[L.kind.get(c)];
        else r += '·';
      }
      rows.push(r);
    }
    return rows;
  }

  root.SlimeEngine2 = { parse, fits, covered, settle, startState, move, shrink, isWin, keyOf, solve, trace, render };
})(typeof module !== 'undefined' && module.exports ? module.exports : window);
