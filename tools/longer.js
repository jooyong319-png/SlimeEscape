// 🔴 긴 판만 따로 뽑아 못자리에 보탠다.
//
// 1차 40판은 뒤로 갈수록 **오히려 쉬워졌다** — 목표는 51·72걸음인데 있는 판이 26·37걸음이라
// 가까운 걸 고르다 보니 내리막이 됐다 (08-31).
// 원인은 하나다: **긴 판이 못자리에 없었다.**
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { grow, rng, SHAPES } = require('./grow.js');

const POOLF = path.join(__dirname, 'make40-pool.json');
const pool = fs.existsSync(POOLF) ? JSON.parse(fs.readFileSync(POOLF, 'utf8')) : [];
const seen = new Set(pool.map(p => p.grid.join('')));

function putStar(grid, base, r, tries, goodEnough) {
  const G = grid.map(row => row.split(''));
  const H = G.length, W = G[0].length;
  const cells = [];
  for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++)
    if (G[y][x] === '.') cells.push([x, y]);
  for (let i = cells.length - 1; i > 0; i--) {
    const j = Math.floor(r() * (i + 1)); [cells[i], cells[j]] = [cells[j], cells[i]];
  }
  let best = null;
  for (const [x, y] of cells.slice(0, tries)) {
    const g2 = grid.map(row => row.split(''));
    g2[y][x] = 'o';
    const gg = g2.map(row => row.join(''));
    const s = E.solve({ grid: gg, gravity: true, clear: 'all', id: 's' }, { needStar: true });
    if (!s.ok) continue;
    const det = s.moves - base;
    if (!best || det > best.det) best = { grid: gg, det, moves: s.moves };
    if (best.det >= goodEnough) break;
  }
  return best;
}

// 긴 판을 노리는 띠 — 많이 돌아다니게 하고, 짧게 나온 건 버린다
const HUNT = [
  { d: 2, W: 20, H: 11, ratio: 0.42, wmin: 40, wmax: 70, lens: [5, 3], min: 34, want: 10 },
  { d: 2, W: 22, H: 11, ratio: 0.41, wmin: 55, wmax: 90, lens: [5, 4], min: 42, want: 10 },
  { d: 3, W: 21, H: 11, ratio: 0.44, wmin: 45, wmax: 80, lens: [4, 3, 3], min: 50, want: 8 },
  { d: 3, W: 23, H: 12, ratio: 0.43, wmin: 60, wmax: 100, lens: [5, 3, 3], min: 58, want: 8 },
  { d: 3, W: 23, H: 12, ratio: 0.42, wmin: 80, wmax: 130, lens: [5, 4, 3], min: 65, want: 6 },
];

const t0 = Date.now();
const BUDGET = (+(process.env.MINUTES || 25)) * 60000;

for (const b of HUNT) {
  let got = 0;
  for (let seed = 1; seed <= 900 && got < b.want; seed++) {
    if (Date.now() - t0 > BUDGET) { console.log('시간 다 됨'); break; }
    const r = rng(seed * 104729 + b.W * 331 + b.wmin * 13 + b.d);
    for (let n = 0; n < 120 && got < b.want; n++) {
      const shape = SHAPES[Math.floor(r() * SHAPES.length)];
      const grid = grow(r, b.W, b.H, b.lens, b.ratio, b.wmin, b.wmax, shape);
      if (!grid) continue;
      const key = grid.join('');
      if (seen.has(key)) continue;
      const a = E.solve({ grid, gravity: true, clear: 'all', id: 'L' });
      if (!a.ok || a.shortest !== 1 || a.moves < b.min) continue;
      seen.add(key);
      const st = putStar(grid, a.moves, r, b.d >= 3 ? 6 : 10, b.d >= 3 ? 5 : 8);
      if (!st) continue;
      pool.push({
        doors: b.d, shape, grid: st.grid, best: a.moves, sol: a.path,
        bestStar: st.moves, det: st.det,
        cut: st.moves + Math.max(6, Math.ceil(st.moves * 0.4)),
      });
      got++;
      console.log(`[홈${b.d} ${b.lens.join('+')}] ${got}/${b.want}  ${a.moves}걸음  (${((Date.now() - t0) / 1000).toFixed(0)}초)`);
    }
  }
  if (got < b.want) console.log(`  🔴 [홈${b.d} ${b.lens.join('+')}] ${got}/${b.want} 밖에 못 뽑음`);
  if (Date.now() - t0 > BUDGET) break;
}

fs.writeFileSync(POOLF, JSON.stringify(pool, null, 1) + '\n');
const spread = k => {
  const v = pool.filter(p => p.doors === k).map(p => p.best).sort((a, b) => a - b);
  return v.length ? `${v.length}개 ${v[0]}~${v[v.length - 1]}걸음` : '없음';
};
console.log(`\n못자리 ${pool.length}개 · ${((Date.now() - t0) / 60000).toFixed(1)}분`);
console.log(`  홈1 ${spread(1)}\n  홈2 ${spread(2)}\n  홈3 ${spread(3)}`);
