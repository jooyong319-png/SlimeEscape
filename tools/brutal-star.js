// 별을 **제일 멀리 돌게** 놓는다. brutal.json 의 판 하나에만 쓴다.
// 🔴 putStar 는 "충분하면 멈춘다"라 공짜 자리에서 그칠 수 있다 — 여기서는 다 훑어 제일 먼 것을 고른다.
'use strict';
const fs = require('fs');
const E = require('./engine.js').SlimeEngine;
const { cutOf } = require('./star.js');
const CAP = 2000000;
const solve = (g, o) => E.solve({ grid: g, gravity: true, clear: 'all', id: 'b' },
                                Object.assign({ cap: CAP }, o || {}));

const j = JSON.parse(fs.readFileSync('tools/brutal.json', 'utf8'));
const bare = j.grid.map(r => r.replace(/o/g, '.'));
const base = solve(bare);
console.log('별 없는 최단 ' + base.moves + '걸음');

const G = bare.map(r => r.split(''));
const H = G.length, W = G[0].length;
const cells = [];
for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) if (G[y][x] === '.') cells.push([x, y]);
console.log('별을 놓아볼 자리 ' + cells.length + '개 — 다 해본다');

let best = null, n = 0;
for (const [x, y] of cells) {
  const g2 = bare.map(r => r.split(''));
  g2[y][x] = 'o';
  const gg = g2.map(r => r.join(''));
  const s = solve(gg, { needStar: true });
  n++;
  if (!s.ok) continue;
  const det = s.moves - base.moves;
  if (!best || det > best.det) {
    best = { grid: gg, det, moves: s.moves, x, y };
    process.stderr.write('  (' + x + ',' + y + ') 돌아가기 +' + det + '걸음\n');
  }
}
console.log('시험한 자리 ' + n);
if (!best) { console.log('🔴 쓸 만한 별 자리가 없다'); process.exit(1); }

const a = solve(best.grid);
console.log('');
console.log(best.grid.join('\n'));
console.log('');
const mins = b => b * (1.5 + b / 15) / 60;
console.log('걸음 ' + a.moves + ' · 별 먹고 ' + best.moves + ' (+' + best.det + ') · 커트 ' + cutOf(best.moves));
console.log('어림 ' + mins(a.moves).toFixed(1) + '분 · 별까지 ' + mins(best.moves).toFixed(1) + '분');
fs.writeFileSync('tools/brutal.json', JSON.stringify(Object.assign({}, j, {
  grid: best.grid, best: a.moves, sol: a.path,
  bestStar: best.moves, cut: cutOf(best.moves),
}), null, 1) + '\n');
console.log('→ tools/brutal.json');
