// 큰 판을 만든다 — **홈을 먼저 그리고 지형을 그 주위에 깐다.**
//
// 🔴 지금까지는 거꾸로였다. 지형을 먼저 깔고 홈이 들어갈 자리를 찾으니
//    홈 6~7칸은 자리가 안 나와서 200판 지어도 0판이었다.
//    홈부터 그리면 큰 몸이 늘 들어간다 = 최단해가 길어진다 = 어려워진다.
'use strict';
const E = require('./engine.js').SlimeEngine;

function rng(seed) {
  let s = (seed | 0) || 1;
  return () => { s ^= s << 13; s |= 0; s ^= s >>> 17; s ^= s << 5; s |= 0;
                 return ((s >>> 0) % 100000) / 100000; };
}

function build(r, W, H, doorLen, extra) {
  const g = [];
  for (let y = 0; y < H; y++) {
    let row = '';
    for (let x = 0; x < W; x++)
      row += (y === 0 || y === H - 1 || x === 0 || x === W - 1) ? '#' : '.';
    g.push(row.split(''));
  }

  // ---- 1. 홈부터. 아래에서 위로 자라는 이어진 덩어리 ----
  const by = H - 2;                                  // 바닥 바로 위
  const bx = 2 + Math.floor(r() * (W - 4 - 1));
  const cells = [[bx, by]];
  const has = new Set([by * W + bx]);
  while (cells.length < doorLen) {
    const grew = [];
    for (const [cx, cy] of cells)
      for (const [nx, ny] of [[cx + 1, cy], [cx - 1, cy], [cx, cy - 1]])
        if (nx > 0 && nx < W - 1 && ny > 1 && !has.has(ny * W + nx)) grew.push([nx, ny]);
    if (!grew.length) return null;
    const p = grew[Math.floor(r() * grew.length)];
    cells.push(p); has.add(p[1] * W + p[0]);
  }
  // 홈 밑은 단단해야 몸이 버틴다 (홈 칸이 아닌 곳만)
  for (const [cx, cy] of cells)
    if (!has.has((cy + 1) * W + cx) && cy + 1 < H - 1) g[cy + 1][cx] = '#';

  // ---- 2. 지형 — 홈은 건드리지 않는다 ----
  const shelves = 3 + Math.floor(r() * 4);
  for (let i = 0; i < shelves; i++) {
    const y = 2 + Math.floor(r() * (H - 4));
    const x = 1 + Math.floor(r() * (W - 3));
    const len = 2 + Math.floor(r() * 4);
    for (let k = 0; k < len && x + k < W - 1; k++)
      if (!has.has(y * W + x + k)) g[y][x + k] = '#';
  }
  const core = Math.floor(r() * doorLen);
  cells.forEach(([cx, cy], k) => { g[cy][cx] = (k === core) ? '*' : '='; });

  // ---- 3. 시작점과 조각 ----
  const stand = [];
  for (let y = 1; y < H - 1; y++)
    for (let x = 1; x < W - 1; x++)
      if (g[y][x] === '.' && g[y + 1][x] === '#') stand.push([x, y]);
  const need = doorLen - 1;
  if (stand.length < need + extra + 2) return null;
  for (let i = stand.length - 1; i > 0; i--) {
    const j = Math.floor(r() * (i + 1));
    [stand[i], stand[j]] = [stand[j], stand[i]];
  }
  const [sx, sy] = stand.pop();
  g[sy][sx] = 'S';
  for (let i = 0; i < need + extra; i++) {
    if (!stand.length) return null;
    const [fx, fy] = stand.pop();
    g[fy][fx] = '+';
  }
  return g.map(row => row.join(''));
}

const W = +(process.env.W || 12), H = +(process.env.H || 10);
const DOOR = +(process.env.DOOR || 6), EXTRA = +(process.env.EXTRA || 1);
const LO = +(process.env.LO || 35), HI = +(process.env.HI || 140);
const N = +(process.env.N || 300), SEED = +(process.env.SEED || 1);
const SHOW = +(process.env.SHOW || 2);

const r = rng(SEED);
const out = [];
let made = 0, solved = 0;
for (let i = 0; i < N; i++) {
  const grid = build(r, W, H, DOOR, EXTRA);
  if (!grid) continue;
  made++;
  const a = E.solve({ grid, gravity: true, clear: 'all', id: 'b' + i });
  if (!a.ok || a.moves < LO || a.moves > HI) continue;
  solved++;
  if (a.shortest !== 1) continue;
  out.push({ grid, moves: a.moves, states: a.states });
}
out.sort((a, b) => b.moves - a.moves);
console.log(`지은 판 ${made} · 범위 안 ${solved} · 최단해 유일 ${out.length}`);
for (const f of out.slice(0, SHOW)) {
  console.log('─'.repeat(40));
  console.log(`${f.moves}걸음 · 상태 ${f.states}`);
  console.log(f.grid.join('\n'));
  console.log(JSON.stringify(f.grid));
}
