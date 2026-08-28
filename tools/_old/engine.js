/* 슬라임 퍼즐 규칙 엔진 — 정본은 이 파일 하나다.
   게임(브라우저)과 솔버(node)가 같은 코드를 쓴다. 사본을 만들지 말 것.

   규칙
   - 슬라임은 크기 N인 N×N 덩어리. (xL, y) = 왼쪽 아래 칸. 몸은 발밑을 딛고 위로 선다
   - 중력: 발밑이 비면 떨어진다. 낙하는 공짜 — 크기가 안 변한다
   - 좌우 이동: 크기 −1. 앞이 막혔으면 N−1칸까지 턱을 오른다 (오르는 것도 이동 한 번)
   - 🔴 몸은 가는 쪽으로 흐른다: 먹고 자라면 앞으로 불어나고, 줄면 뒤가 딸려온다
   - 덮은 칸의 먹이를 전부 먹는다: 하나당 +1. 불은 끄면서 −fireCost (기본 3)
   - 크기가 0 이하가 되거나 몸이 안 들어가는 이동은 불가 (죽지 않는다. 못 갈 뿐)
   - 덩어리가 출구 칸을 덮으면 클리어
*/
(function (root) {
  'use strict';

  function parse(def) {
    const g = def.grid.map(r => r.split(''));
    const h = g.length, w = g[0].length;
    let start = null, exit = null;
    const foods = [], fires = [];
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const c = g[y][x];
      if (c === 'S') { start = [x, y]; g[y][x] = '.'; }
      else if (c === 'E') { exit = [x, y]; g[y][x] = '.'; }
      else if (c === 'o') { foods.push(y * w + x); g[y][x] = '.'; }
      else if (c === 'f') { fires.push(y * w + x); g[y][x] = '.'; }
    }
    return {
      ...def, g, w, h, start, exit, foods, fires,
      foodIdx: new Map(foods.map((c, i) => [c, i])),
      fireIdx: new Map(fires.map((c, i) => [c, i])),
    };
  }

  const isWall = (L, x, y) => x < 0 || y < 0 || x >= L.w || y >= L.h || L.g[y][x] === '#';

  function fits(L, xL, y, n) {
    for (let i = 0; i < n; i++) for (let j = 0; j < n; j++) if (isWall(L, xL + i, y - j)) return false;
    return true;
  }

  function covered(L, xL, y, n) {
    const out = [];
    for (let i = 0; i < n; i++) for (let j = 0; j < n; j++) out.push((y - j) * L.w + (xL + i));
    return out;
  }

  // 크기가 n -> n2로 바뀔 때 몸을 어디에 놓을지.
  // 🔴 몸은 가는 쪽으로 흐른다 — 자라면 앞으로 불어나고, 줄면 뒤가 딸려온다.
  function reseat(L, xL, y, n, n2, dir) {
    const keepLeft = xL;                        // 뒤쪽(왼쪽) 고정
    const keepRight = (xL + n - 1) - n2 + 1;    // 앞쪽(오른쪽) 고정
    let order;
    if (dir > 0) order = n2 > n ? [keepLeft, keepRight] : [keepRight, keepLeft];
    else if (dir < 0) order = n2 > n ? [keepRight, keepLeft] : [keepLeft, keepRight];
    else order = [keepLeft, keepRight];
    for (const x of order) if (fits(L, x, y, n2)) return x;
    return null;
  }

  // 떨어지고 -> 먹고 -> 크기 변하고 를 안정될 때까지 반복. 실패하면 null.
  // sc = 마지막으로 줄어든 뒤 걸은 수 (shrinkEvery 실험용. 기본 1이면 안 쓰인다)
  function settle(L, st, dir) {
    let { x, y, n, fm, gm } = st;
    let sc = st.sc || 0;
    dir = dir || 0;
    for (let guard = 0; guard < 32; guard++) {
      let fell = false;
      while (fits(L, x, y + 1, n)) { y++; fell = true; }

      let delta = 0, ate = false;
      for (const c of covered(L, x, y, n)) {
        const fi = L.foodIdx.get(c);
        if (fi !== undefined && !(fm & (1 << fi))) { fm |= 1 << fi; delta += 1; ate = true; continue; }
        const gi = L.fireIdx.get(c);
        if (gi !== undefined && !(gm & (1 << gi))) { gm |= 1 << gi; delta -= (L.fireCost || 3); ate = true; }
      }
      if (!ate) { if (fell) continue; return { x, y, n, fm, gm, sc }; }

      sc = 0;                       // 배가 차면 다시 센다
      const n2 = n + delta;
      if (n2 < 1) return null;
      if (n2 !== n) {
        const nx = reseat(L, x, y, n, n2, dir);
        if (nx === null) return null;
        x = nx; n = n2;
      }
    }
    return null;
  }

  const startState = L => settle(L, { x: L.start[0], y: L.start[1], n: L.startSize, fm: 0, gm: 0, sc: 0 });

  // dx = -1 | 1
  function move(L, st, dx) {
    // 같은 크기로 옆(또는 턱 위)에 설 자리가 있는가
    let hy = null;
    for (let h = 0; h <= st.n - 1; h++) {
      if (fits(L, st.x + dx, st.y - h, st.n)) { hy = st.y - h; break; }
    }
    if (hy === null) return null;

    // 🔬 실험: shrinkEvery 걸음마다 한 겹 줄어든다. 기본 1이면 매 걸음 = 지금 규칙 그대로.
    const every = L.shrinkEvery || 1;
    const sc = (st.sc || 0) + 1;
    const shrink = sc >= every;
    const n2 = shrink ? st.n - 1 : st.n;
    const sc2 = shrink ? 0 : sc;
    if (n2 < 1) return null;

    // 앞面이 한 칸 나아가고, 줄어든 몫은 뒤에서 빠진다
    const shifted = st.x + dx;
    const x2 = dx > 0 ? (shifted + st.n - 1) - n2 + 1 : shifted;
    if (!fits(L, x2, hy, n2)) return null;

    return settle(L, { x: x2, y: hy, n: n2, fm: st.fm, gm: st.gm, sc: sc2 }, dx);
  }

  const isWin = (L, st) => covered(L, st.x, st.y, st.n).includes(L.exit[1] * L.w + L.exit[0]);
  const keyOf = st => `${st.x},${st.y},${st.n},${st.fm},${st.gm},${st.sc||0}`;

  function solve(def) {
    const L = parse(def);
    if (!L.start || !L.exit) return { ok: false, why: 'S 또는 E가 없음' };
    if (L.foods.length > 30 || L.fires.length > 30) return { ok: false, why: '먹이/불이 30개를 넘음' };
    if (!fits(L, L.start[0], L.start[1], L.startSize)) return { ok: false, why: '시작 위치에 몸이 안 들어감' };

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

  root.SlimeEngine = { parse, fits, covered, reseat, settle, startState, move, isWin, keyOf, solve, isWall };
})(typeof module !== 'undefined' && module.exports ? module.exports : window);
