// 🔴 **덤 조각이 압박을 올리는가**를 잰다 (2026-09-04).
//
// 손잡이(굴 모양·홈 개수·크기)로는 압박이 3~5% 를 못 벗어났다.
// 압박이 생기려면 *"이 길로 가면 나중에 못 채운다"* 가 있어야 하는데,
// **덤 조각**(먹으면 길이가 어긋나는 조각)이 정확히 그것이다.
//
// 같은 판을 **덤 조각 얹기 전과 후로 둘 다 재서** 짝지어 견준다.
// ⚠️ 09-04 에 표본 4개로 손잡이를 고르는 실수를 했다. 여기서는 짝비교라
//    같은 판끼리 견주므로 훨씬 적은 표본으로도 방향이 보인다. 그래도 30판은 본다.
//
//   node tools/trapstat.js
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const src = fs.readFileSync(path.join(__dirname, 'hard.js'), 'utf8');
const mod = { exports: {} };
new Function('module', 'exports', 'require', '__dirname',
             src.slice(0, src.indexOf('const fs = require')) +
             '\nmodule.exports = { measure };')(mod, mod.exports, require, __dirname);
const { measure } = mod.exports;

//  🔴 traps.js 에서 잘라 오려 했더니 headPath 가 LP 선언보다 **아래**에 있어
//     잘린 구간에 안 들어왔다. 짧으니 여기 그대로 둔다.
const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };
function headPath(L, sol) {
  const seen = new Set();
  let st = E.startState(L);
  seen.add(st.body[0]);
  for (const ch of sol) {
    const d = SYM[ch];
    if (d === undefined) continue;
    const ns = E.step(L, st, d);
    if (!ns) break;
    const from = st.body[0], to = ns.body[0];
    seen.add(to);
    if ((to - from) % L.w === 0) {              // 같은 세로줄 = 떨어졌다
      const step = to > from ? L.w : -L.w;
      for (let c = from; c !== to; c += step) seen.add(c);
    }
    st = ns;
  }
  return seen;
}

const WANT = +(process.env.TRAPS || 3);
const N = +(process.env.N || 30);

/// 정답이 안 지나가는 빈 칸에 덤 조각을 얹는다. 최단해가 그대로여야 한다.
function addTraps(grid, want) {
  const def = { grid, gravity: true, clear: 'all', id: 't' };
  const L = E.parse(def);
  const base = E.solve(def);
  if (!base.ok) return null;
  const walked = headPath(L, base.path);
  const G = grid.map(r => r.split(''));
  const H = G.length, W = G[0].length;

  const cand = [];
  for (let y = 1; y < H - 1; y++)
    for (let x = 1; x < W - 1; x++) {
      const c = y * W + x;
      if (G[y][x] !== '.' || walked.has(c)) continue;
      //  정답 길 **바로 옆**만. 멀면 눈에 안 들어와 함정이 아니다
      let near = false;
      for (const n of [c - 1, c + 1, c - W, c + W]) if (walked.has(n)) near = true;
      if (near) cand.push([x, y]);
    }

  let put = 0;
  for (const [x, y] of cand) {
    if (put >= want) break;
    G[y][x] = '+';
    const g2 = G.map(r => r.join(''));
    const a = E.solve({ grid: g2, gravity: true, clear: 'all', id: 't' });
    //  최단해가 그대로이고 여전히 하나뿐이어야 한다
    if (a.ok && a.moves === base.moves && a.shortest === 1) put++;
    else G[y][x] = '.';
  }
  return put ? G.map(r => r.join('')) : null;
}

const pool = JSON.parse(fs.readFileSync('tools/hard-pool.json', 'utf8'));
const stat = JSON.parse(fs.readFileSync('tools/poolstat.json', 'utf8'));
//  잰 판 중 상태가 너무 크지 않은 것부터 (재는 값이 비싸다)
const rows = stat.filter(r => r.states < 120000).sort((a, b) => a.states - b.states).slice(0, N);
console.log('짝비교', rows.length, '판 · 덤 조각', WANT, '개씩');
console.log('판    걸음   압박 전 → 후      되짚기 전 → 후');
console.log('-'.repeat(52));

let up = 0, same = 0, down = 0, sBefore = 0, sAfter = 0, n = 0;
for (const r of rows) {
  const g2 = addTraps(pool[r.i].grid, WANT);
  if (!g2) continue;
  const m = measure({ grid: g2, gravity: true, clear: 'all', id: 't' });
  if (!m) continue;
  n++; sBefore += r.press; sAfter += m.press;
  if (m.press > r.press + 0.005) up++; else if (m.press < r.press - 0.005) down++; else same++;
  if (n <= 14) console.log(String(r.i).padStart(4) + String(r.moves).padStart(6) +
    (r.press * 100).toFixed(1).padStart(9) + ' →' + (m.press * 100).toFixed(1).padStart(6) +
    (r.back * 100).toFixed(0).padStart(12) + ' →' + (m.back * 100).toFixed(0).padStart(5));
}
console.log('-'.repeat(52));
console.log('잰 판 ' + n + ' · 올랐다 ' + up + ' · 그대로 ' + same + ' · 내렸다 ' + down);
console.log('평균 압박  ' + (sBefore / n * 100).toFixed(1) + '%  →  ' + (sAfter / n * 100).toFixed(1) + '%');
