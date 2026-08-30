// 기술 하나만 시키는 **작은 판**을 만든다.
//
// 🔴 사장님 지적 (08-30): 15번 판의 11걸음짜리 되짚기는 처음 보면 절대 안 떠오른다.
//    어딘가에서 그걸 **작게 가르쳤어야** 한다. 판 이름만 "되짚기"라고 붙여놓고
//    실제로는 난이도 띠에서 뽑았을 뿐이었다.
//
// 가르치는 판의 조건 — 어려우면 안 된다. **그 기술이 아니면 안 풀려야** 한다.
//   · 짧다 (12~26걸음)
//   · 그 기술 값이 아주 높다
//   · 다른 기술은 안 섞인다
//
// MODE:
//   back  되짚기 — 목표에서 멀어지는 걸음이 많다
//   trap  먹으면 안 되는 조각 — 정답이 조각을 남긴다
//   step  두고 온 몸이 디딤돌 — 첫 홈을 채워야 위로 갈 수 있다
'use strict';
const E = require('./engine.js').SlimeEngine;

function rng(seed) {
  let s = (seed | 0) || 1;
  return () => { s ^= s << 13; s |= 0; s ^= s >>> 17; s ^= s << 5; s |= 0;
                 return ((s >>> 0) % 100000) / 100000; };
}

function build(r, W, H, lens, extra, carve) {
  const g = [];
  for (let y = 0; y < H; y++) g.push(new Array(W).fill('#'));
  const want = Math.floor((W - 2) * (H - 2) * carve);
  const open = new Set();
  for (let s = 0; s < 2; s++) {
    let cx = 1 + Math.floor(r() * (W - 2)), cy = H - 2;
    for (let k = 0; k < want * 12 && open.size < want; k++) {
      if (g[cy][cx] === '#') { g[cy][cx] = '.'; open.add(cy * W + cx); }
      const u = r();
      const nx = cx + (u < 0.24 ? -1 : u < 0.48 ? 1 : 0);
      const ny = cy + (u < 0.48 ? 0 : u < 0.84 ? -1 : 1);
      if (nx > 0 && nx < W - 1 && ny > 0 && ny < H - 1) { cx = nx; cy = ny; }
    }
  }
  if (open.size < want * 0.85) return null;
  let lo = H, hi = 0;
  for (const c of open) { const y = (c / W) | 0; if (y < lo) lo = y; if (y > hi) hi = y; }
  if (hi - lo < (H - 2) * 0.6) return null;

  const floors = () => {
    const out = [];
    for (const c of open) {
      const x = c % W, y = (c / W) | 0;
      if (g[y][x] === '.' && g[y + 1][x] === '#') out.push([x, y]);
    }
    return out;
  };
  const GLY = [['=', '*'], ['-', '%']];
  for (let di = 0; di < lens.length; di++) {
    const seeds = floors();
    if (!seeds.length) return null;
    let cells = null;
    for (let a = 0; a < 40 && !cells; a++) {
      const [x0, y0] = seeds[Math.floor(r() * seeds.length)];
      const got = [[x0, y0]], has = new Set([y0 * W + x0]);
      while (got.length < lens[di]) {
        const grew = [];
        for (const [x, y] of got)
          for (const [nx, ny] of [[x + 1, y], [x - 1, y], [x, y - 1]])
            if (nx > 0 && nx < W - 1 && ny > 0 && ny < H - 1
                && g[ny][nx] === '.' && !has.has(ny * W + nx)) grew.push([nx, ny]);
        if (!grew.length) break;
        const p = grew[Math.floor(r() * grew.length)];
        got.push(p); has.add(p[1] * W + p[0]);
      }
      if (got.length === lens[di]) cells = got;
    }
    if (!cells) return null;
    const core = Math.floor(r() * lens[di]);
    cells.forEach(([x, y], k) => { g[y][x] = GLY[di][k === core ? 1 : 0]; open.delete(y * W + x); });
  }
  // 🔴 시작점에서 못 닿는 주머니는 돌로 메운다. 있으나 마나 한 빈 칸이고,
  //    "방이 여럿"으로 잡혀서 카메라까지 튄다 (08-30).
  {
    const oc = [...open];
    if (oc.length) {
      const seen = new Set([oc[0]]), q = [oc[0]];
      while (q.length) {
        const c = q.pop(), x = c % W, y = (c / W) | 0;
        for (const [nx, ny] of [[x+1,y],[x-1,y],[x,y+1],[x,y-1]]) {
          const n = ny * W + nx;
          if (nx>0 && nx<W-1 && ny>0 && ny<H-1 && g[ny][nx] !== '#' && !seen.has(n)) { seen.add(n); q.push(n); }
        }
      }
      for (let y = 1; y < H - 1; y++)
        for (let x = 1; x < W - 1; x++)
          if (g[y][x] !== '#' && !seen.has(y * W + x)) { g[y][x] = '#'; open.delete(y * W + x); }
    }
  }

  const stand = floors();
  const need = lens.reduce((s, n) => s + n - 1, 0);
  if (stand.length < need + extra + 1) return null;
  for (let i = stand.length - 1; i > 0; i--) {
    const j = Math.floor(r() * (i + 1)); [stand[i], stand[j]] = [stand[j], stand[i]];
  }
  const [sx, sy] = stand.pop(); g[sy][sx] = 'S';
  for (let i = 0; i < need + extra; i++) {
    if (!stand.length) return null;
    const [fx, fy] = stand.pop(); g[fy][fx] = '+';
  }
  return g.map(row => row.join(''));
}

/// 정답이 그 기술을 얼마나 쓰나
function skill(grid, id) {
  const L = E.parse({ grid, gravity: true, clear: 'all', id });
  const a = E.solve({ grid, gravity: true, clear: 'all', id });
  if (!a.ok) return null;
  const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };
  const goal = L.doors[L.doors.length - 1].cells[0];
  const dist = c => Math.abs((c % L.w) - (goal % L.w)) + Math.abs(((c / L.w) | 0) - ((goal / L.w) | 0));

  // 🔴 "첫 홈을 채운 뒤 더 높이 올라갔나" — 두고 온 몸이 디딤돌이 됐다는 증거.
  //    y 는 아래로 갈수록 크다. 낮은 y = 높은 곳.
  let st = E.startState(L), away = 0, run = 0, maxRun = 0;
  let topBefore = (st.body[0] / L.w) | 0, topAfter = -1, opened = false;
  for (const ch of a.path) {
    const before = dist(st.body[0]), bd = st.dm || 0;
    const ns = E.step(L, st, SYM[ch]);
    if (!ns) break;
    const y = (ns.body[0] / L.w) | 0;
    if (!opened) topBefore = Math.min(topBefore, y);
    else topAfter = topAfter < 0 ? y : Math.min(topAfter, y);
    if ((ns.dm || 0) !== bd) opened = true;
    if (dist(ns.body[0]) > before) { away++; run++; maxRun = Math.max(maxRun, run); } else run = 0;
    st = ns;
  }
  const eaten = [];
  for (let i = 0; i < L.foods.length; i++) if (st.fm & (1 << i)) eaten.push(i);
  return {
    moves: a.moves, shortest: a.shortest,
    back: away / a.moves,          // 목표에서 멀어지는 걸음 비율
    maxRun,                        // 연달아 멀어지는 최대 걸음 — 되짚기의 크기
    skipped: L.foods.length - eaten.length,
    climb: topAfter < 0 ? 0 : topBefore - topAfter,   // 첫 홈을 채운 뒤 얼마나 더 높이 올랐나
  };
}

const MODE = process.env.MODE || 'back';
const W = +(process.env.W || 9), H = +(process.env.H || 8);
const LO = +(process.env.LO || 12), HI = +(process.env.HI || 26);
const N = +(process.env.N || 500), SEED = +(process.env.SEED || 1);
const SHOW = +(process.env.SHOW || 2);

const CFG = {
  back: { lens: [4], extra: 0, carve: 0.62, ok: m => m.maxRun >= 5 && m.back >= 0.40 },
  trap: { lens: [4], extra: 1, carve: 0.62, ok: m => m.skipped >= 1 },
  step: { lens: [3, 3], extra: 0, carve: 0.58, ok: m => m.climb >= +(process.env.CLIMB || 1) },
}[MODE];
if (!CFG) { console.log('MODE 는 back · trap · step 중 하나'); process.exit(1); }

const r = rng(SEED);
const out = [];
let made = 0;
for (let i = 0; i < N; i++) {
  const grid = build(r, W, H, CFG.lens, CFG.extra, CFG.carve);
  if (!grid) continue;
  made++;
  const m = skill(grid, 't' + i);
  if (!m || m.moves < LO || m.moves > HI || m.shortest !== 1) continue;
  if (!CFG.ok(m)) continue;
  out.push({ grid, m });
}
const score = MODE === 'back' ? (x => x.m.maxRun) : MODE === 'trap' ? (x => x.m.skipped) : (x => x.m.climb);
out.sort((a, b) => score(b) - score(a));
console.log(`[${MODE}] 지은 판 ${made} · 통과 ${out.length}`);
for (const f of out.slice(0, SHOW)) {
  const m = f.m;
  console.log('─'.repeat(40));
  console.log(`${m.moves}걸음 · 되짚기 ${(m.back * 100).toFixed(0)}% (연속 ${m.maxRun}) · 남긴조각 ${m.skipped} · 더오름 ${m.climb}`);
  console.log(f.grid.join('\n'));
  console.log(JSON.stringify(f.grid));
}
