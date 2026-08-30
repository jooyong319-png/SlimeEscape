// 1묶음 40판을 만든다. 오래 걸리니 사장님 주무시는 동안 돌린다.
//
// 🔴 사장님 지시 (08-31):
//   · 1-1 ~ 1-40 (튜토리얼은 1-0 으로 따로)
//   · **처음엔 쉽다가 갑자기 팍 어려워지면 안 된다** — 동생들이 당황했다
//   · 3홈 판은 **적게**. 대신 지금 1-16(47걸음)보다 어려워야 한다
//
// 만드는 법은 tools/grow.js — 풀리는 과정을 먼저 만들고 그걸 판으로 굳힌다.
// 여기서는 띠별로 잔뜩 뽑아서 **난이도 순으로 고르게 늘어놓는 일**을 한다.
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { grow, rng } = require('./grow.js');

const LP = path.join(__dirname, '../game/Assets/Resources/levels.json');
const OUT = path.join(__dirname, 'make40-out.json');
const LOG = (...a) => { console.log(...a); };

// 걸린 시간 어림 (08-30 실측으로 맞춘 식)
const mins = b => b * (1.5 + b / 15) / 60;

// ---- 띠 ----
// 앞은 작고 홈 하나, 뒤로 갈수록 커지고 홈이 늘어난다.
const BANDS = [
  { tag: '1홈', lens: [3],       W: 13, H: 8,  ratio: 0.44, wmin: 3,  wmax: 10, lo: 8,  hi: 22, want: 8 },
  { tag: '1홈', lens: [4],       W: 15, H: 9,  ratio: 0.44, wmin: 5,  wmax: 16, lo: 14, hi: 32, want: 8 },
  { tag: '2홈', lens: [3, 3],    W: 16, H: 9,  ratio: 0.43, wmin: 6,  wmax: 18, lo: 20, hi: 40, want: 8 },
  { tag: '2홈', lens: [4, 3],    W: 18, H: 10, ratio: 0.42, wmin: 10, wmax: 24, lo: 28, hi: 55, want: 8 },
  { tag: '2홈', lens: [5, 3],    W: 20, H: 11, ratio: 0.42, wmin: 14, wmax: 30, lo: 38, hi: 70, want: 4 },
  { tag: '3홈', lens: [4, 3, 3], W: 21, H: 11, ratio: 0.44, wmin: 18, wmax: 34, lo: 50, hi: 120, want: 4 },
];

/// 🔴 별 자리 — **칸마다 다시 푸는 건 너무 느리다** (3홈 판은 상태가 10만이다).
///    빈 칸에서 몇 개만 뽑아 보고, 충분히 돌아가는 자리가 나오면 거기서 멈춘다.
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

// ---- 뽑기 ----
const pool = [];
const seen = new Set();
const t0 = Date.now();

for (const b of BANDS) {
  let got = 0;
  for (let seed = 1; seed <= 300 && got < b.want; seed++) {
    const r = rng(seed * 7919 + b.W * 131 + b.lens.length * 17);
    for (let n = 0; n < 200 && got < b.want; n++) {
      const grid = grow(r, b.W, b.H, b.lens, b.ratio, b.wmin, b.wmax);
      if (!grid) continue;
      const key = grid.join('');
      if (seen.has(key)) continue;
      const a = E.solve({ grid, gravity: true, clear: 'all', id: 'g' });
      if (!a.ok || a.moves < b.lo || a.moves > b.hi || a.shortest !== 1) continue;
      seen.add(key);
      const st = putStar(grid, a.moves, r, b.lens.length >= 3 ? 8 : 16, b.lens.length >= 3 ? 5 : 8);
      if (!st) continue;
      pool.push({
        tag: b.tag, doors: b.lens.length, grid: st.grid,
        best: a.moves, sol: a.path, bestStar: st.moves, det: st.det,
        cut: st.moves + Math.max(6, Math.ceil(st.moves * 0.4)),
        w: grid[0].length, h: grid.length,
      });
      got++;
      LOG(`[${b.tag} ${b.lens.join('+')}] ${got}/${b.want}  ${grid[0].length}x${grid.length} ${a.moves}걸음 별+${st.det}  (${((Date.now() - t0) / 1000).toFixed(0)}초)`);
    }
  }
  if (got < b.want) LOG(`  🔴 [${b.tag} ${b.lens.join('+')}] ${got}/${b.want} 밖에 못 뽑음`);
}

fs.writeFileSync(OUT, JSON.stringify(pool, null, 1) + '\n');
LOG(`\n모은 판 ${pool.length}개 · ${((Date.now() - t0) / 1000 / 60).toFixed(1)}분`);

// ---- 늘어놓기 ----
// 🔴 띠 안에서는 쉬운 것부터. 띠가 바뀔 때 한 번 뚝 떨어진다 —
//    그게 "새 규칙을 가르치는 자리"라 오히려 있어야 하는 계단이다.
//    갑자기 팍 어려워지는 건 띠 안에서 널뛸 때 생긴다. 그래서 안에서는 반드시 오름차순.
const byTier = { 1: [], 2: [], 3: [] };
for (const p of pool) byTier[p.doors].push(p);
for (const k of [1, 2, 3]) byTier[k].sort((a, b) => a.best - b.best);

const WANT = { 1: 16, 2: 18, 3: 6 };
const chain = [];
for (const k of [1, 2, 3]) chain.push(...byTier[k].slice(0, WANT[k]));

LOG(`\n골라낸 판 ${chain.length}개 (1홈 ${Math.min(byTier[1].length, WANT[1])} · 2홈 ${Math.min(byTier[2].length, WANT[2])} · 3홈 ${Math.min(byTier[3].length, WANT[3])})`);

// ---- 판 자료에 쓰기 ----
const d = JSON.parse(fs.readFileSync(LP, 'utf8'));
const tut = d.levels.find(l => l.tutorial);
if (!tut) { LOG('🔴 튜토리얼 판이 없다 — 그만둔다'); process.exit(1); }
tut.id = '1-0';

const levels = [tut];
chain.forEach((p, i) => {
  levels.push({
    id: '1-' + (i + 1),
    name: p.doors === 1 ? '홈 하나' : p.doors === 2 ? '홈 둘 — 몸을 두고 간다' : '홈 셋',
    grid: p.grid, clear: 'all',
    best: p.best, sol: p.sol, bestStar: p.bestStar, cut: p.cut,
    lost: 0, tight: 0, backtrack: 0, states: 0,
  });
});
d.levels = levels;
d.chapter = 1;
fs.writeFileSync(LP, JSON.stringify(d, null, 2) + '\n');

let sum = 0;
LOG('\n번호   크기    홈  걸음  어림');
for (const l of d.levels) {
  const s = l.grid.join('');
  const n = [['=', '*'], ['-', '%'], ['~', '@']].filter(p => s.includes(p[0]) || s.includes(p[1])).length;
  sum += mins(l.best);
  LOG(l.id.padEnd(6) + (l.grid[0].length + 'x' + l.grid.length).padEnd(8) + n + '  ' +
      String(l.best).padStart(3) + '걸음  ' + mins(l.best).toFixed(1) + '분');
}
LOG(`\n합계 어림 ${sum.toFixed(0)}분`);
