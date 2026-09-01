// 🔴 **먹으면 안 되는 조각**을 얹는다.
//
// 새 생성기(grow.js)는 "필요할 때만 조각을 놓는" 방식이라 딱 맞는 개수만 놓는다.
// 그래서 47판 중 37판에 덤 조각이 아예 없었다 — 08-30에 못 이기는 상태를 28%→67%로
// 올려준 그 기믹이 통째로 빠져 있었다 (08-31 사장님 지적).
//
// 판 모양은 안 건드린다. **정답이 지나가지 않는 칸**에만 조각을 얹는다:
//   · 머리가 밟은 칸에 놓으면 억지로 먹혀서 판이 깨진다 → 그 칸은 뺀다
//   · 정답 길 **바로 옆**에 놓는다. 멀리 두면 눈에 안 들어와 함정이 아니다
//   · 얹은 뒤 최단해가 그대로인지, 최단해가 여전히 하나뿐인지 반드시 확인한다
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const LP = path.join(__dirname, '../game/Assets/Resources/levels.json');
const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };
const WANT = +(process.env.TRAPS || 2);          // 판마다 얹을 개수

/// 정답을 되짚으며 **머리가 밟은 칸**을 모은다
function headPath(L, sol) {
  const seen = new Set();
  let st = E.startState(L);
  seen.add(st.body[0]);
  for (const ch of sol) {
    const d = SYM[ch];
    if (d === undefined) continue;
    const ns = E.step(L, st, d);
    if (!ns) break;
    // 떨어지는 동안 지나간 칸도 머리가 밟은 것이다
    const from = st.body[0], to = ns.body[0];
    seen.add(to);
    if ((to - from) % L.w === 0) {                // 같은 세로줄 = 떨어졌다
      const step = to > from ? L.w : -L.w;
      for (let c = from; c !== to; c += step) seen.add(c);
    }
    st = ns;
  }
  return seen;
}

const d = JSON.parse(fs.readFileSync(LP, 'utf8'));
let added = 0, done = 0, skipped = 0;

for (const l of d.levels) {
  if (l.tutorial) continue;
  const base = E.solve({ grid: l.grid, gravity: true, clear: 'all', id: l.id });
  if (!base.ok) { console.log('🔴 ' + l.id + ' 원판이 안 풀린다'); continue; }

  const L0 = E.parse({ grid: l.grid, gravity: true, clear: 'all', id: l.id });
  const walked = headPath(L0, l.sol || base.path);

  // 이미 덤이 있는 판은 건너뛴다
  let st = E.startState(L0);
  for (const ch of (l.sol || base.path)) {
    const ns = E.step(L0, st, SYM[ch]); if (!ns) break; st = ns;
  }
  let eaten = 0;
  for (let i = 0; i < L0.foods.length; i++) if (st.fm & (1 << i)) eaten++;
  if (L0.foods.length - eaten > 0) { skipped++; continue; }

  // 후보: 빈 칸 · 머리가 안 밟은 곳 · 바닥에 붙은 곳 · **정답 길 바로 옆**
  const H = l.grid.length, W = l.grid[0].length;
  const g0 = l.grid.map(r => r.split(''));
  const near = c => {
    const x = c % W, y = (c / W) | 0;
    for (const [nx, ny] of [[x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]])
      if (walked.has(ny * W + nx)) return true;
    return false;
  };
  const cand = [];
  for (let y = 1; y < H - 1; y++)
    for (let x = 1; x < W - 1; x++) {
      const c = y * W + x;
      if (g0[y][x] !== '.' || walked.has(c)) continue;
      if (g0[y + 1][x] !== '#') continue;          // 바닥에 붙어 있어야 눈에 띈다
      if (!near(c)) continue;
      cand.push([x, y]);
    }

  let grid = l.grid.slice(), put = 0;
  for (const [x, y] of cand) {
    if (put >= WANT) break;
    const g = grid.map(r => r.split(''));
    g[y][x] = '+';
    const trial = g.map(r => r.join(''));
    const a = E.solve({ grid: trial, gravity: true, clear: 'all', id: l.id });
    // 🔴 최단해가 그대로여야 하고, 여전히 하나뿐이어야 한다
    if (!a.ok || a.moves !== base.moves || a.shortest !== 1) continue;
    grid = trial; put++;
  }
  if (!put) { console.log('  ' + l.id + ' — 놓을 자리가 없다'); continue; }

  // 별 자리는 그대로 두되 값은 다시 잰다
  const a = E.solve({ grid, gravity: true, clear: 'all', id: l.id });
  const s = E.solve({ grid, gravity: true, clear: 'all', id: l.id }, { needStar: true });
  l.grid = grid; l.best = a.moves; l.sol = a.path;
  if (s.ok) { l.bestStar = s.moves; l.cut = s.moves + Math.max(6, Math.ceil(s.moves * 0.4)); }
  added += put; done++;
  console.log(l.id.padEnd(6) + '덤 조각 ' + put + '개 · ' + a.moves + '걸음 (그대로)');
}

fs.writeFileSync(LP, JSON.stringify(d, null, 2) + '\n');
console.log(`\n${done}판에 덤 조각 ${added}개 · 원래 있던 판 ${skipped}개는 건너뜀`);
