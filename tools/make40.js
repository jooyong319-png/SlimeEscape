// 1묶음 40판을 만든다.
//
// 🔴 사장님 지시 (08-31):
//   · 1-1 ~ 1-40 (튜토리얼은 1-0 으로 따로)
//   · **처음엔 쉽다가 갑자기 팍 어려워지면 안 된다** — 동생들이 당황했다
//   · 3홈 판은 **적게**. 대신 지금 1-16(47걸음)보다 어려워야 한다
//
// 1차 시도는 앞이 평평했다 — 여덟 판이 죄다 8걸음, 그 다음 여덟 판이 죄다 14걸음.
// 그래서 **목표 곡선을 먼저 그려놓고 거기 제일 가까운 판을 골라 끼우는** 방식으로 바꿨다.
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { grow, rng, SHAPES } = require('./grow.js');

const LP = path.join(__dirname, '../game/Assets/Resources/levels.json');
const POOLF = path.join(__dirname, 'make40-pool.json');
const mins = b => b * (1.5 + b / 15) / 60;      // 08-30 실측으로 맞춘 식

// ---- 목표 곡선 ----
// 40판에 걸쳐 8걸음에서 72걸음까지. 앞은 촘촘하고 뒤로 갈수록 벌어진다.
const N = 40;
const target = i => Math.round(8 + 64 * Math.pow((i - 1) / (N - 1), 1.35));
// 몇 번째부터 홈이 몇 개인가
const tierOf = i => (i <= 14 ? 1 : i <= 34 ? 2 : 3);

// ---- 띠 ----
// 같은 홈 개수라도 돌아다니는 양을 달리해 **걸음 수가 넓게 퍼지게** 한다.
const BANDS = [
  { d: 1, W: 11, H: 8,  ratio: 0.46, wmin: 2,  wmax: 6,  lens: [3] },
  { d: 1, W: 13, H: 8,  ratio: 0.45, wmin: 5,  wmax: 12, lens: [3] },
  { d: 1, W: 15, H: 9,  ratio: 0.44, wmin: 10, wmax: 20, lens: [4] },
  { d: 1, W: 17, H: 9,  ratio: 0.43, wmin: 16, wmax: 30, lens: [4] },
  { d: 1, W: 18, H: 10, ratio: 0.43, wmin: 24, wmax: 40, lens: [5] },
  { d: 2, W: 15, H: 9,  ratio: 0.44, wmin: 4,  wmax: 12, lens: [3, 3] },
  { d: 2, W: 17, H: 10, ratio: 0.43, wmin: 10, wmax: 22, lens: [4, 3] },
  { d: 2, W: 19, H: 10, ratio: 0.42, wmin: 18, wmax: 32, lens: [4, 3] },
  { d: 2, W: 20, H: 11, ratio: 0.42, wmin: 26, wmax: 44, lens: [5, 3] },
  { d: 2, W: 21, H: 11, ratio: 0.42, wmin: 34, wmax: 56, lens: [5, 4] },
  { d: 3, W: 20, H: 11, ratio: 0.44, wmin: 14, wmax: 28, lens: [4, 3, 3] },
  { d: 3, W: 22, H: 11, ratio: 0.43, wmin: 24, wmax: 44, lens: [4, 3, 3] },
  { d: 3, W: 22, H: 12, ratio: 0.43, wmin: 34, wmax: 58, lens: [5, 3, 3] },
];

/// 별 자리 — 칸마다 다시 푸는 건 너무 느리다. 몇 개만 뽑아 보고 충분하면 멈춘다.
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

// ---- 잔뜩 뽑는다 ----
// 🔴 SELECT=1 이면 **못자리를 새로 만들지 않고 있는 걸 쓴다.**
//    안 그러면 longer.js 로 어렵게 찾은 긴 판을 덮어써 버린다 (08-31에 한 번 그랬다).
const SELECT_ONLY = process.env.SELECT === '1';
const pool = SELECT_ONLY && fs.existsSync(POOLF)
  ? JSON.parse(fs.readFileSync(POOLF, 'utf8')) : [];
const seen = new Set(pool.map(p => p.grid.join('')));
const t0 = Date.now();
const PER = +(process.env.PER || 14);        // 띠마다 이만큼

for (const b of (SELECT_ONLY ? [] : BANDS)) {
  let got = 0;
  for (let seed = 1; seed <= 400 && got < PER; seed++) {
    const r = rng(seed * 7919 + b.W * 131 + b.wmin * 29 + b.d * 7);
    for (let n = 0; n < 150 && got < PER; n++) {
      // 🔴 모양을 골고루 — 다 같은 방식으로 파면 마흔 판이 다 비슷하게 생긴다
      const shape = SHAPES[Math.floor(r() * SHAPES.length)];
      const grid = grow(r, b.W, b.H, b.lens, b.ratio, b.wmin, b.wmax, shape);
      if (!grid) continue;
      const key = grid.join('');
      if (seen.has(key)) continue;
      const a = E.solve({ grid, gravity: true, clear: 'all', id: 'g' });
      if (!a.ok || a.shortest !== 1 || a.moves < 6) continue;
      seen.add(key);
      const st = putStar(grid, a.moves, r, b.d >= 3 ? 8 : 14, b.d >= 3 ? 5 : 8);
      if (!st) continue;
      pool.push({
        doors: b.d, shape, grid: st.grid, best: a.moves, sol: a.path,
        bestStar: st.moves, det: st.det,
        cut: st.moves + Math.max(6, Math.ceil(st.moves * 0.4)),
      });
      got++;
    }
  }
  console.log(`[홈${b.d} ${b.lens.join('+')} ${b.W}x${b.H} 걸음${b.wmin}~${b.wmax}] ${got}/${PER}` +
              `  (${((Date.now() - t0) / 1000).toFixed(0)}초)`);
}
if (!SELECT_ONLY) fs.writeFileSync(POOLF, JSON.stringify(pool, null, 1) + '\n');

const spread = k => {
  const v = pool.filter(p => p.doors === k).map(p => p.best).sort((a, b) => a - b);
  return v.length ? `${v.length}개 ${v[0]}~${v[v.length - 1]}걸음` : '없음';
};
console.log(`\n모은 판 ${pool.length}개 · ${((Date.now() - t0) / 60000).toFixed(1)}분`);
console.log(`  홈1 ${spread(1)}\n  홈2 ${spread(2)}\n  홈3 ${spread(3)}`);

// ---- 곡선에 맞춰 고른다 ----
// 🔴 자리마다 "이 정도 걸음이었으면" 하는 값을 먼저 정하고, 남은 판 중 제일 가까운 걸 끼운다.
//    이러면 같은 걸음 수가 여덟 개씩 늘어서는 일이 안 생긴다.
const left = { 1: pool.filter(p => p.doors === 1), 2: pool.filter(p => p.doors === 2), 3: pool.filter(p => p.doors === 3) };
const chain = [];
for (let i = 1; i <= N; i++) {
  const want = target(i);
  let tier = tierOf(i);
  while (tier > 1 && !left[tier].length) tier--;          // 모자라면 아래 띠에서 빌린다
  while (tier < 3 && !left[tier].length) tier++;
  const bag = left[tier];
  if (!bag.length) { console.log(`🔴 ${i}번 자리에 넣을 판이 없다`); break; }
  let bi = 0;
  for (let k = 1; k < bag.length; k++)
    if (Math.abs(bag[k].best - want) < Math.abs(bag[bi].best - want)) bi = k;
  chain.push(bag.splice(bi, 1)[0]);
}
// 🔴 **줄어들면 안 된다.** 긴 판이 모자라면 가까운 걸 고르다 내리막이 된다 (1차 시도).
//    띠 안에서 걸음 수 오름차순으로 다시 세운다 — 홈 개수 차례는 그대로 둔다.
{
  const by = { 1: [], 2: [], 3: [] };
  for (const p of chain) by[p.doors].push(p);
  for (const k of [1, 2, 3]) by[k].sort((a, b) => a.best - b.best);
  chain.length = 0;
  for (const k of [1, 2, 3]) chain.push(...by[k]);
}

// ---- 판 자료에 쓰기 ----
const d = JSON.parse(fs.readFileSync(LP, 'utf8'));
const tut = d.levels.find(l => l.tutorial);
if (!tut) { console.log('🔴 튜토리얼 판이 없다 — 그만둔다'); process.exit(1); }
tut.id = '1-0';

d.levels = [tut, ...chain.map((p, i) => ({
  id: '1-' + (i + 1),
  name: p.doors === 1 ? '홈 하나' : p.doors === 2 ? '홈 둘 — 몸을 두고 간다' : '홈 셋 — 세 번 나눠 쓴다',
  grid: p.grid, clear: 'all',
  best: p.best, sol: p.sol, bestStar: p.bestStar, cut: p.cut,
  lost: 0, tight: 0, backtrack: 0, states: 0,
}))];
d.chapter = 1;
fs.writeFileSync(LP, JSON.stringify(d, null, 2) + '\n');

let sum = 0, prev = 0, jumps = 0;
console.log('\n번호   크기    홈  목표  걸음  어림');
for (let i = 0; i < d.levels.length; i++) {
  const l = d.levels[i];
  const s = l.grid.join('');
  const n = [['=', '*'], ['-', '%'], ['~', '@']].filter(p => s.includes(p[0]) || s.includes(p[1])).length;
  sum += mins(l.best);
  const want = i === 0 ? 0 : target(i);
  if (i > 1 && l.best > prev * 1.6) jumps++;
  prev = l.best;
  console.log(l.id.padEnd(6) + (l.grid[0].length + 'x' + l.grid.length).padEnd(8) + n +
              String(want || '-').padStart(6) + String(l.best).padStart(6) + '걸음  ' + mins(l.best).toFixed(1) + '분');
}
console.log(`\n판 ${d.levels.length - 1}개 · 합계 어림 ${sum.toFixed(0)}분 · 난이도 급등 ${jumps}번`);
