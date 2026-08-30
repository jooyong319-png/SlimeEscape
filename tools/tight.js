// 좁고 빽빽한 판을 만든다.
//
// 🔴 넓은 판은 걸음의 82%가 그냥 걷기였다. 좁게 만들면 한 칸 한 칸이 결정이 된다.
//
// 걸러내는 기준 — 전부 **잴 수 있는 것**만 쓴다. 재미는 못 재니 사람이 봐야 한다.
//   · 풀린다
//   · 최단해가 **하나뿐**이다 (여러 개면 대충 해도 된다는 뜻)
//   · 걸음 수가 범위 안이다
//   · 🔴 **되돌릴 수 없는 상태가 많다** — 실수하면 망하는 판이라야 생각을 한다
//   · 별을 먹으려면 길이 확 달라진다
'use strict';
const E = require('./engine.js').SlimeEngine;

// ---- 난수: xorshift32. 예전 LCG 는 2^53 을 넘겨 seed 가 안 먹었다 ----
function rng(seed) {
  let s = (seed | 0) || 1;
  return () => {
    s ^= s << 13; s |= 0; s ^= s >>> 17; s ^= s << 5; s |= 0;
    return ((s >>> 0) % 100000) / 100000;
  };
}

/// 🔴 여기서부터는 절대 못 이기는 상태의 비율.
///    상태를 전부 펼친 뒤, 이긴 상태에서 **거꾸로** 닿을 수 있는 것을 뺀다.
function lostRatio(def, cap = 120000) {
  const L = E.parse(def);
  const s0 = E.startState(L);
  const key = E.keyOf(s0);
  const idx = new Map([[key, 0]]);
  const states = [s0];
  const back = [[]];               // back[i] = i 로 오는 상태들
  const win = [];
  for (let i = 0; i < states.length; i++) {
    if (states.length > cap) return null;      // 너무 크면 포기
    const st = states[i];
    if (E.isWin(L, st)) { win.push(i); continue; }
    for (let d = 0; d < 4; d++) {
      const ns = E.step(L, st, d);
      if (!ns) continue;
      const k = E.keyOf(ns);
      let j = idx.get(k);
      if (j === undefined) { j = states.length; idx.set(k, j); states.push(ns); back.push([]); }
      back[j].push(i);
    }
  }
  if (!win.length) return null;
  const safe = new Set(win);
  const q = [...win];
  for (let h = 0; h < q.length; h++)
    for (const p of back[q[h]]) if (!safe.has(p)) { safe.add(p); q.push(p); }
  return { lost: (states.length - safe.size) / states.length, states: states.length };
}

// ---- 판 한 장 짓기 ----
function build(r, W, H, doorLen, extra, twoDoors, padLen) {
  const g = [];
  for (let y = 0; y < H; y++) {
    let row = '';
    for (let x = 0; x < W; x++)
      row += (y === 0 || y === H - 1 || x === 0 || x === W - 1) ? '#' : '.';
    g.push(row.split(''));
  }
  // 선반 몇 개 — 오르내림이 생겨야 몸 길이가 쓸모를 갖는다
  const shelves = 2 + Math.floor(r() * 3);
  for (let i = 0; i < shelves; i++) {
    const y = 2 + Math.floor(r() * (H - 4));
    const x = 1 + Math.floor(r() * (W - 3));
    const len = 2 + Math.floor(r() * 3);
    for (let k = 0; k < len && x + k < W - 1; k++) g[y][x + k] = '#';
  }
  // 딛고 설 수 있는 칸들
  const stand = [];
  for (let y = 1; y < H - 1; y++)
    for (let x = 1; x < W - 1; x++)
      if (g[y][x] === '.' && g[y + 1][x] === '#') stand.push([x, y]);
  if (stand.length < doorLen + 6) return null;

  // 홈: 바닥에 붙은 가로 한 줄. 길이가 n칸이면 조각 n-1개가 든다.
  const runOf = (len) => {
    const out = [];
    for (const [x, y] of stand) {
      let ok = true;
      for (let k = 0; k < len; k++)
        if (!(x + k < W - 1 && g[y][x + k] === '.' && g[y + 1][x + k] === '#')) { ok = false; break; }
      if (ok) out.push([x, y]);
    }
    return out;
  };
  // 🔴 홈은 일자만이 아니다. **꺾인 홈**은 몸을 접어 넣어야 해서 훨씬 어렵다.
  //    바닥에 붙은 칸에서 출발해 이어 붙인다 — 한 칸이라도 땅을 딛고 있으면 몸이 버틴다.
  const seeds = runOf(1);
  if (!seeds.length) return null;
  let cells = null;
  for (let attempt = 0; attempt < 30 && !cells; attempt++) {
    const [sx0, sy0] = seeds[Math.floor(r() * seeds.length)];
    const got = [[sx0, sy0]];
    const has = new Set([sy0 * W + sx0]);
    while (got.length < doorLen) {
      const grew = [];
      for (const [cx, cy] of got)
        for (const [nx, ny] of [[cx + 1, cy], [cx - 1, cy], [cx, cy - 1]])
          if (nx > 0 && nx < W - 1 && ny > 0 && ny < H - 1
              && g[ny][nx] === '.' && !has.has(ny * W + nx)) grew.push([nx, ny]);
      if (!grew.length) break;
      const pick = grew[Math.floor(r() * grew.length)];
      got.push(pick); has.add(pick[1] * W + pick[0]);
    }
    if (got.length === doorLen) cells = got;
  }
  if (!cells) return null;
  const core = Math.floor(r() * doorLen);          // 머리가 끝나야 할 자리
  const dy = cells[0][1], dx = cells[0][0];
  cells.forEach(([cx, cy], k) => { g[cy][cx] = (k === core) ? '*' : '='; });
  const bent = cells.some(([, cy]) => cy !== dy);  // 꺾였나

  // 🔴 홈 두 개 — 첫 홈을 채우면 몸을 두고 핵만 남는다. 두고 온 몸은 **디딤돌**이 된다.
  let need = doorLen - 1;
  if (twoDoors) {
    const len2 = 3;
    const spots = runOf(len2).filter(([x, y]) => g[y][x] === '.' &&
      Math.abs(y - dy) > 0 || x + len2 <= dx - 2 || x >= dx + doorLen + 2);
    if (!spots.length) return null;
    const [ex, ey] = spots[Math.floor(r() * spots.length)];
    for (let k = 0; k < len2; k++) {
      if (g[ey][ex + k] !== '.') return null;
      g[ey][ex + k] = (k === len2 - 1) ? '%' : '-';
    }
    need += len2 - 1;
  }

  // 🔴 받침대 — 홈과 겹치지 않는 바닥 자리에. 여기 몸을 놓으면 계단이 된다.
  if (padLen > 0) {
    const spots = runOf(padLen).filter(([x, y]) => y !== dy || x + padLen <= dx || x >= dx + doorLen);
    if (!spots.length) return null;
    const [px, py] = spots[Math.floor(r() * spots.length)];
    for (let k = 0; k < padLen; k++) {
      if (g[py][px + k] !== '.') return null;
      g[py][px + k] = 'T';
    }
  }

  // 시작점과 조각 — 홈에서 떨어진 곳부터
  const free = stand.filter(([x, y]) => g[y][x] === '.');
  if (free.length < doorLen + 1) return null;
  for (let i = free.length - 1; i > 0; i--) {
    const j = Math.floor(r() * (i + 1));
    [free[i], free[j]] = [free[j], free[i]];
  }
  const [sxx, syy] = free.pop();
  g[syy][sxx] = 'S';
  // 🔴 딱 필요한 만큼 + 덤. 덤은 **먹으면 안 되는 조각**이다 — 먹으면 홈에 안 맞는다.
  for (let i = 0; i < need + extra; i++) {
    if (!free.length) return null;
    const [fx, fy] = free.pop();
    g[fy][fx] = '+';
  }
  // 별 — 아무 빈 칸. 좋은 자리는 나중에 골라 붙인다
  return g.map(row => row.join(''));
}

// ---- 별 자리 고르기: 먹으려면 제일 많이 돌아가는 곳 ----
function bestStarSpot(grid, id, base) {
  const G = grid.map(r => r.split(''));
  const H = G.length, W = G[0].length;
  let best = null;
  for (let y = 1; y < H - 1; y++)
    for (let x = 1; x < W - 1; x++) {
      if (G[y][x] !== '.') continue;
      const g2 = grid.map(r => r.split(''));
      g2[y][x] = 'o';
      const gg = g2.map(r => r.join(''));
      const s = E.solve({ grid: gg, gravity: true, clear: 'all', id }, { needStar: true });
      if (!s.ok) continue;
      const det = s.moves - base;
      if (!best || det > best.det) best = { grid: gg, det, moves: s.moves, x, y };
    }
  return best;
}

// ---- 돌리기 ----
const W = +(process.env.W || 9), H = +(process.env.H || 8);
const DOOR = +(process.env.DOOR || 4);
const LO = +(process.env.LO || 16), HI = +(process.env.HI || 44);
const MINLOST = +(process.env.MINLOST || 0.18);
const MINDET = +(process.env.MINDET || 5);
const EXTRA = +(process.env.EXTRA || 0);      // 먹으면 안 되는 조각 개수
const TWO = process.env.TWO === '1';          // 홈 두 개
const PAD = +(process.env.PAD || 0);          // 받침대 칸 수
const NEEDPAD = process.env.NEEDPAD === '1';  // 받침대를 꼭 써야만 통과
const N = +(process.env.N || 400);
const SEED = +(process.env.SEED || 1);

const r = rng(SEED);
const found = [];
let tried = 0, solved = 0;
for (let i = 0; i < N; i++) {
  const grid = build(r, W, H, DOOR, EXTRA, TWO, PAD);
  if (!grid) continue;
  tried++;
  const id = 't' + i;
  const a = E.solve({ grid, gravity: true, clear: 'all', id });
  if (!a.ok || a.moves < LO || a.moves > HI) continue;
  if (a.shortest !== 1) continue;            // 최단해가 여러 개면 대충 해도 된다
  // 🔴 받침대가 장식이면 뜻이 없다 — 최단해가 실제로 쓰는지 본다
  if (NEEDPAD && !a.path.includes('↧')) continue;
  solved++;
  const lr = lostRatio({ grid, gravity: true, clear: 'all', id });
  if (!lr || lr.lost < MINLOST) continue;
  const st = bestStarSpot(grid, id, a.moves);
  if (!st || st.det < MINDET) continue;
  found.push({ grid: st.grid, path: a.path, best: a.moves, bestStar: st.moves, det: st.det,
               lost: lr.lost, states: lr.states, star: [st.x, st.y] });
}

found.sort((a, b) => (b.lost * 100 + b.det) - (a.lost * 100 + a.det));
console.log(`지은 판 ${tried} · 조건 맞는 최단해 ${solved} · 통과 ${found.length}`);
for (const f of found.slice(0, +(process.env.SHOW || 3))) {
  console.log('─'.repeat(40));
  console.log(`${f.best}걸음 · 별까지 ${f.bestStar}(+${f.det}) · 못 이기는 상태 ${(f.lost * 100).toFixed(0)}% · 상태 ${f.states}` +
              (f.path.includes('↧') ? '  · 받침대 씀' : ''));
  console.log(f.grid.join('\n'));
  console.log(JSON.stringify(f.grid));
}
