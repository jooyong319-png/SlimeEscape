// 꽉 찬 판을 만든다 — **홈 여러 개 · 빈 공간 적게.**
//
// 🔴 지금까지는 빈 굴에 선반 몇 개를 얹었다. 그래서 걸음의 대부분이 그냥 걷기였고,
//    홈이 하나뿐이라 판마다 할 일이 한 가지였다 (08-30 사장님).
//
// 바뀐 것 — 순서를 거꾸로 한다:
//   1. 굴을 통째로 돌로 채우고 **파낸다** (자연히 꽉 찬다)
//   2. 파낸 바닥에 홈을 **두세 개** 놓는다
//   3. 남은 자리에 시작점과 조각
//
// 홈이 둘 이상이면 첫 홈을 채운 뒤 **두고 온 몸이 디딤돌**이 된다 — 이 게임의 제일 좋은 규칙이
// 1번 판부터 나온다.
'use strict';
const E = require('./engine.js').SlimeEngine;

function rng(seed) {
  let s = (seed | 0) || 1;
  return () => { s ^= s << 13; s |= 0; s ^= s >>> 17; s ^= s << 5; s |= 0;
                 return ((s >>> 0) % 100000) / 100000; };
}

function build(r, W, H, doorLens, extra, carve) {
  const g = [];
  for (let y = 0; y < H; y++) g.push(new Array(W).fill('#'));

  // ---- 1. 파낸다. 바닥 줄에서 시작해 위로 헤집는다 ----
  // 🔴 굴이 굴 전체에 퍼져야 한다. 한 구석만 파면 "꽉 찬" 게 아니라 **좁은** 거다.
  //    바닥 여러 곳에서 시작해 위로 치우쳐 헤집는다.
  // 🔴 파는 자리를 **가로세로로 흩는다.** 바닥에서만 위로 파올라가니
  //    아래쪽이 통돌로 남아 "넓다"가 아니라 "휑하다"가 됐다 (08-30 사장님 전체화면).
  const want = Math.floor((W - 2) * (H - 2) * carve);
  const open = new Set();
  const starts = 4 + Math.floor(r() * 3);
  for (let s = 0; s < starts; s++) {
    let cx = 1 + Math.floor(((s + 0.5) / starts + (r() - 0.5) * 0.2) * (W - 2));
    let cy = 1 + Math.floor(r() * (H - 2));            // 높이도 흩는다
    cx = Math.max(1, Math.min(W - 2, cx));
    for (let k = 0; k < want * 8 && open.size < want; k++) {
      if (g[cy][cx] === '#') { g[cy][cx] = '.'; open.add(cy * W + cx); }
      const u = r();
      const nx = cx + (u < 0.26 ? -1 : u < 0.52 ? 1 : 0);
      const ny = cy + (u < 0.52 ? 0 : u < 0.76 ? -1 : 1);   // 위아래 고르게
      if (nx > 0 && nx < W - 1 && ny > 0 && ny < H - 1) { cx = nx; cy = ny; }
    }
  }
  if (open.size < want * 0.85) return null;
  // 파낸 데가 위아래로 퍼졌나
  let lo = H, hi = 0;
  for (const c of open) { const y = (c / W) | 0; if (y < lo) lo = y; if (y > hi) hi = y; }
  if (hi - lo < (H - 2) * 0.7) return null;
  // 🔴 위아래가 고르게 열려 있어야 한다. 아래가 통돌이면 넓이를 못 쓴다.
  {
    const half = Math.floor(H / 2);
    let up = 0, dn = 0;
    for (const c of open) (((c / W) | 0) < half ? up++ : dn++);
    if (Math.min(up, dn) < Math.max(up, dn) * 0.55) return null;
  }

  // ---- 2. 홈 — 바닥에 붙은 칸에서 위로 자란다 ----
  const floors = () => {
    const out = [];
    for (const c of open) {
      const x = c % W, y = (c / W) | 0;
      if (g[y][x] === '.' && g[y + 1][x] === '#') out.push([x, y]);
    }
    return out;
  };
  const GLY = [['=', '*'], ['-', '%']];
  const doors = [];
  for (let di = 0; di < doorLens.length; di++) {
    const len = doorLens[di];
    const seeds = floors();
    if (!seeds.length) return null;
    let cells = null;
    for (let a = 0; a < 40 && !cells; a++) {
      const [sx0, sy0] = seeds[Math.floor(r() * seeds.length)];
      const got = [[sx0, sy0]], has = new Set([sy0 * W + sx0]);
      while (got.length < len) {
        const grew = [];
        for (const [x, y] of got)
          for (const [nx, ny] of [[x + 1, y], [x - 1, y], [x, y - 1]])
            if (nx > 0 && nx < W - 1 && ny > 0 && ny < H - 1
                && g[ny][nx] === '.' && !has.has(ny * W + nx)) grew.push([nx, ny]);
        if (!grew.length) break;
        const p = grew[Math.floor(r() * grew.length)];
        got.push(p); has.add(p[1] * W + p[0]);
      }
      if (got.length === len) cells = got;
    }
    if (!cells) return null;
    const core = Math.floor(r() * len);
    cells.forEach(([x, y], k) => { g[y][x] = GLY[di][k === core ? 1 : 0]; open.delete(y * W + x); });
    doors.push(cells);
  }

  // ---- 3. 시작점과 조각 ----
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
  const need = doorLens.reduce((s, n) => s + n - 1, 0);
  if (stand.length < need + extra + 1) return null;
  for (let i = stand.length - 1; i > 0; i--) {
    const j = Math.floor(r() * (i + 1));
    [stand[i], stand[j]] = [stand[j], stand[i]];
  }
  const [sx, sy] = stand.pop(); g[sy][sx] = 'S';
  for (let i = 0; i < need + extra; i++) {
    if (!stand.length) return null;
    const [fx, fy] = stand.pop(); g[fy][fx] = '+';
  }
  return g.map(row => row.join(''));
}

/// 🔴 꽉참 = **놀 수 있는 칸 중 할 일이 있는 칸의 비율.**
///    돌로 꽉 찬 건 꽉 찬 게 아니라 좁은 거다. 홈·조각·별이 촘촘해야 꽉 찬 것이다.
function density(grid) {
  const H = grid.length, W = grid[0].length;
  let play = 0, stuff = 0;
  for (let y = 1; y < H - 1; y++)
    for (let x = 1; x < W - 1; x++) {
      const c = grid[y][x];
      if (c === '#') continue;
      play++;
      if (c !== '.') stuff++;          // 홈 · 조각 · 시작점 · 별
    }
  return play ? stuff / play : 0;
}

const W = +(process.env.W || 11), H = +(process.env.H || 9);
const D1 = +(process.env.D1 || 4), D2 = +(process.env.D2 || 3);
const EXTRA = +(process.env.EXTRA || 1);
const CARVE = +(process.env.CARVE || 0.55);
const MINDEN = +(process.env.MINDEN || 0.45);
const LO = +(process.env.LO || 25), HI = +(process.env.HI || 200);
const N = +(process.env.N || 300), SEED = +(process.env.SEED || 1);
const SHOW = +(process.env.SHOW || 2);

const r = rng(SEED);
const out = [];
let made = 0, ok = 0, nDen = 0, nUnsolved = 0, nRange = 0; const lens = [];
for (let i = 0; i < N; i++) {
  const grid = build(r, W, H, D2 > 0 ? [D1, D2] : [D1], EXTRA, CARVE);
  if (!grid) continue;
  made++;
  const den = density(grid);
  if (den < MINDEN) { nDen++; continue; }
  const a = E.solve({ grid, gravity: true, clear: 'all', id: 'd' + i });
  if (!a.ok) { nUnsolved++; continue; }
  if (a.moves < LO || a.moves > HI) { nRange++; lens.push(a.moves); continue; }
  ok++;
  if (a.shortest !== 1) continue;
  out.push({ grid, moves: a.moves, states: a.states, den });
}
out.sort((a, b) => b.moves - a.moves);
console.log(`지은 판 ${made} · 밀도탈락 ${nDen} · 못품 ${nUnsolved} · 범위밖 ${nRange} · 범위안 ${ok} · 유일 ${out.length}` + (lens.length ? '  걸음분포 ' + lens.slice(0,10).join(',') : ''));
for (const f of out.slice(0, SHOW)) {
  console.log('─'.repeat(40));
  console.log(`${f.moves}걸음 · 상태 ${f.states} · 꽉참 ${(f.den * 100).toFixed(0)}%`);
  console.log(f.grid.join('\n'));
  console.log(JSON.stringify(f.grid));
}
