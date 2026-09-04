// 🔴 **한 판만** 골라 흉악하게 만든다 (2026-09-05 밤).
// 처음엔 900개를 새로 지으려다 판 하나에 5분씩 걸려 못 찾았다.
// 이미 만들어둔 못에서 **제일 긴 판**을 집어 덤 조각을 최대한 얹는 쪽으로 바꿨다.
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { putStar, cutOf } = require('./star.js');
const { rng } = require('./grow.js');

const src = fs.readFileSync(path.join(__dirname, 'hard.js'), 'utf8');
const mod = { exports: {} };
new Function('module', 'exports', 'require', '__dirname',
             src.slice(0, src.indexOf('const fs = require')) +
             '\nmodule.exports = { measure };')(mod, mod.exports, require, __dirname);
const { measure } = mod.exports;

const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };
const IDX = +(process.env.IDX || 121);
const WANT = +(process.env.WANT || 10);
const CAP = +(process.env.CAP2 || 1500000);
const solve = (g, o) => E.solve({ grid: g, gravity: true, clear: 'all', id: 'b' },
                                Object.assign({ cap: CAP }, o || {}));

const pool = JSON.parse(fs.readFileSync('tools/trap-pool.json', 'utf8'));
let grid = pool[IDX].grid;
let base = solve(grid);
console.log('바탕: ' + grid[0].length + 'x' + grid.length + ' · ' + base.moves + '걸음 · 상태 ' +
            base.states.toLocaleString() + ' · 지금 덤 ' + (pool[IDX].traps || 0));

//  정답이 밟는 칸을 모은다
const L = E.parse({ grid, gravity: true, clear: 'all', id: 't' });
const walked = new Set();
{
  let st = E.startState(L);
  for (const c of st.body) walked.add(c);
  for (const ch of base.path) {
    const d = SYM[ch]; if (d === undefined) continue;
    const ns = E.step(L, st, d); if (!ns) break;
    const from = st.body[0], to = ns.body[0];
    if ((to - from) % L.w === 0 && to !== from) {
      const s = to > from ? L.w : -L.w;
      for (let c = from; c !== to; c += s) walked.add(c);
    }
    for (const c of ns.body) walked.add(c);
    st = ns;
  }
}

//  🔴 **정답 길 바로 옆**에만 놓는다. 멀면 눈에 안 들어와 함정이 아니다.
const G = grid.map(r => r.split(''));
const H = G.length, W = G[0].length;
const cand = [];
for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) {
  if (G[y][x] !== '.') continue;
  const c = y * W + x;
  if (walked.has(c)) continue;
  let near = 0;
  for (const n of [c - 1, c + 1, c - W, c + W]) if (walked.has(n)) near++;
  if (near) cand.push([x, y, near]);
}
cand.sort((a, b) => b[2] - a[2]);          // 정답 길에 많이 닿는 칸부터
console.log('놓아볼 자리 ' + cand.length + '개');

let put = 0;
const capTrial = Math.min(CAP, base.states * 3 + 60000);
for (const [x, y] of cand) {
  if (put >= WANT) break;
  G[y][x] = '+';
  const a = solve(G.map(r => r.join('')), { cap: capTrial });
  //  🔴 **최단해가 그대로일 필요는 없다.** 그 조건은 *있던 판*을 안 망가뜨리려는 것이지,
  //     새로 만드는 판에는 필요 없다 — 풀리고 최단이 하나뿐이면 된다.
  //     이 조건 하나 때문에 스물다섯 자리 중 한 자리만 붙었다 (09-05 밤).
  //     KEEP=1 이면 옛 방식대로 최단해까지 지킨다.
  const okNow = process.env.KEEP === '1'
    ? (a.ok && a.moves === base.moves && a.shortest === 1)
    : (a.ok && a.moves >= base.moves && (process.env.UNIQ === '0' || a.shortest === 1));
  if (okNow) {
    put++;
    process.stderr.write('  덤 ' + put + '개째 (' + x + ',' + y + ')\n');
  } else G[y][x] = '.';
}
grid = G.map(r => r.join(''));
console.log('얹은 덤 조각 ' + put + '개');

const a2 = solve(grid);
const st2 = putStar(grid, a2.moves, rng(11), 30, 14, Math.min(CAP, base.states * 3 + 60000));
if (st2) grid = st2.grid;
const a3 = solve(grid);
const s3 = solve(grid, { needStar: true });
const m = measure({ grid, gravity: true, clear: 'all', id: 'b' });

console.log('');
console.log(grid.join('\n'));
console.log('');
const mins = b => b * (1.5 + b / 15) / 60;
console.log('걸음 ' + a3.moves + ' · 별 먹고 ' + (s3.ok ? s3.moves : '-') +
            ' · 커트 ' + (s3.ok ? cutOf(s3.moves) : '-'));
console.log('덤 조각 ' + put + '개 · 조각 전부 ' + (grid.join('').match(/\+/g) || []).length + '개');
if (m) console.log('압박 ' + (m.press * 100).toFixed(1) + '% · 외길 ' + (m.tight * 100).toFixed(1) +
                   '% · 되짚기 ' + (m.back * 100).toFixed(0) + '% · 경우의 수 ' + m.states.toLocaleString());
else console.log('압박: 너무 커서 못 쟀다 (상태 ' + a3.states.toLocaleString() + ')');
console.log('어림 ' + mins(a3.moves).toFixed(1) + '분 (별까지 ' + (s3.ok ? mins(s3.moves).toFixed(1) : '-') + '분)');

fs.writeFileSync('tools/brutal.json', JSON.stringify({
  grid, clear: 'all', best: a3.moves, sol: a3.path,
  bestStar: s3.ok ? s3.moves : a3.moves, cut: cutOf(s3.ok ? s3.moves : a3.moves),
  lost: 0, tight: m ? m.tight : 0, backtrack: m ? m.back : 0, states: a3.states,
  press: m ? +m.press.toFixed(4) : -1, traps: put, gim: 'none',
}, null, 1) + '\n');
console.log('→ tools/brutal.json');
