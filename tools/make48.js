// 🔴 마지막 판(1-48)을 만든다 — **안 쓰인 판 중에서 제일 빽빽한 것**으로.
//
// ⚠️ 09-05 밤 실수: 못에서 "제일 긴 판"을 집었는데 **그게 이미 1-47 로 쓰인 판**이었다.
//    사장님이 방금 깬 판을 마지막 판이라고 내놓았다. 한 줄이면 막을 수 있었다.
//    → 여기서는 **쓰인 판을 먼저 모아 빼고** 고른다. 그리고 다 만든 뒤 다시 한 번 본다.
//
// 🔴 그리고 고르는 자를 바꿨다. 길이가 아니라 **밀도**다:
//      걸음/빈칸   — 좁은 데서 오래 걷는가 (옛 어려운 판 0.90~1.04, 지금 판 중앙 0.37)
//      밟는 칸 비율 — 정답이 판을 얼마나 쓰는가 (옛 58~76%, 지금 20~30%)
//    넓은 방에서 오래 걷는 것과 좁은 데서 오래 고민하는 것은 다르다.
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { cutOf } = require('./star.js');

const LP = path.join(__dirname, '../game/Assets/Resources/levels.json');
const IDX = +(process.env.IDX || 225);
const CAP = 2500000;
const solve = (g, o) => E.solve({ grid: g, gravity: true, clear: 'all', id: 'x' },
                                Object.assign({ cap: CAP }, o || {}));
const key = g => g.map(r => r.replace(/o/g, '.')).join('|');

const d = JSON.parse(fs.readFileSync(LP, 'utf8'));
const pool = JSON.parse(fs.readFileSync('tools/trap-pool.json', 'utf8'));
const others = d.levels.filter(l => l.id !== '1-48');
const used = new Set(others.map(l => key(l.grid)));

const src = pool[IDX];
if (used.has(key(src.grid))) { console.log('🔴 #' + IDX + ' 은 이미 쓰인 판이다 — 다른 걸 골라라'); process.exit(1); }
console.log('#' + IDX + ' · ' + src.grid[0].length + 'x' + src.grid.length + ' · ' + src.best + '걸음 · 안 쓰인 판 🟢');

//  별을 **다 훑어** 제일 멀리 도는 자리에 놓는다
const bare = src.grid.map(r => r.replace(/o/g, '.'));
const base = solve(bare);
if (!base.ok) { console.log('🔴 안 풀린다'); process.exit(1); }
const G = bare.map(r => r.split(''));
const H = G.length, W = G[0].length;
let best = null, tried = 0;
for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) {
  if (G[y][x] !== '.') continue;
  const g2 = bare.map(r => r.split(''));
  g2[y][x] = 'o';
  const gg = g2.map(r => r.join(''));
  const s = solve(gg, { needStar: true });
  tried++;
  if (!s.ok) continue;
  const det = s.moves - base.moves;
  if (!best || det > best.det) { best = { grid: gg, det, moves: s.moves }; }
}
console.log('별 자리 ' + tried + '군데 다 시험 · 제일 멀리 도는 것 +' + (best ? best.det : 0) + '걸음');
if (!best) { console.log('🔴 별 자리가 없다'); process.exit(1); }

const a = solve(best.grid);
//  🔴 **한 번 더 본다** — 별까지 넣은 뒤에도 겹치지 않는지
if (used.has(key(best.grid))) { console.log('🔴 별을 넣고 보니 겹친다'); process.exit(1); }

console.log('');
console.log(best.grid.join('\n'));
console.log('');
const mins = b => b * (1.5 + b / 15) / 60;
console.log('걸음 ' + a.moves + ' · 별 먹고 ' + best.moves + ' · 커트 ' + cutOf(best.moves) +
            ' · 어림 ' + mins(a.moves).toFixed(1) + '분 (별까지 ' + mins(best.moves).toFixed(1) + '분)');

if (process.env.WRITE !== '1') { console.log('\n(보여주기만 했다. 쓰려면 WRITE=1)'); process.exit(0); }
fs.writeFileSync(path.join(__dirname, 'levels.bak.json'), fs.readFileSync(LP));
d.levels = others.concat([{
  id: '1-48', grid: best.grid, clear: 'all',
  best: a.moves, sol: a.path, bestStar: best.moves, cut: cutOf(best.moves),
  lost: 0, tight: 0, backtrack: 0, states: a.states,
  press: -1, traps: src.traps || 0, gim: src.gim || 'none', brutal: true,
}]);
//  🔴 마지막으로 **판 자료 전체**에 겹치는 게 없는지 본다
{
  const seen = new Map(); let dup = 0;
  for (const l of d.levels) {
    const k = key(l.grid);
    if (seen.has(k)) { console.log('🔴 겹침: ' + seen.get(k) + ' = ' + l.id); dup++; }
    else seen.set(k, l.id);
  }
  if (dup) { console.log('🔴 겹치는 판이 있어 안 쓴다'); process.exit(1); }
  console.log('겹침 검사 🟢 없음');
}
fs.writeFileSync(LP, JSON.stringify(d, null, 2) + '\n');
console.log('판 자료에 썼다 · 판 ' + d.levels.length);
