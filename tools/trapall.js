// 🔴 못 전체에 **덤 조각**을 얹는다 (2026-09-04).
//
// 짝비교에서 압박이 6.5% → 24.5% 로 올랐다. 손잡이로는 3~5% 를 못 벗어났는데
// 덤 조각 셋이 네 배를 만들었다 — **처음으로 진짜 틀린 수를 만들기 때문**이다.
//
//   node tools/trapall.js
//   TRAPS=4 node tools/trapall.js
//
// 판 모양은 안 건드린다. 정답이 **안 지나가는 칸**에만 얹고,
// 얹은 뒤 최단해가 그대로인지 · 여전히 하나뿐인지 확인한다.
'use strict';
const fs = require('fs');
const E = require('./engine.js').SlimeEngine;

const IN  = process.argv[2] || 'tools/hard-pool.json';
const OUT = process.argv[3] || 'tools/trap-pool.json';
const WANT = +(process.env.TRAPS || 3);
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

function addTraps(grid, want) {
  const def = { grid, gravity: true, clear: 'all', id: 't' };
  const base = E.solve(def);
  if (!base.ok) return null;
  const walked = headPath(E.parse(def), base.path);
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
    if (a.ok && a.moves === base.moves && a.shortest === 1) put++;
    else G[y][x] = '.';
  }
  return put ? { grid: G.map(r => r.join('')), traps: put } : null;
}

const pool = JSON.parse(fs.readFileSync(IN, 'utf8'));
console.log(IN, '—', pool.length, '개 ·', WANT, '개씩 얹는다');
const out = [];
let none = 0;
for (let i = 0; i < pool.length; i++) {
  const p = pool[i];
  const r = addTraps(p.grid, WANT);
  if (!r) { none++; out.push(Object.assign({}, p, { traps: 0 })); continue; }
  //  🔴 정답 수순은 그대로다 (최단해가 안 바뀌는 것만 받았다). 그래도 다시 풀어 못박는다.
  const a = E.solve({ grid: r.grid, gravity: true, clear: 'all', id: 'g' });
  out.push(Object.assign({}, p, { grid: r.grid, traps: r.traps, best: a.moves, sol: a.path }));
  if ((i + 1) % 40 === 0) process.stderr.write('  ' + (i + 1) + '/' + pool.length + '\n');
}
fs.writeFileSync(OUT, JSON.stringify(out, null, 1) + '\n');
const avg = out.reduce((s, p) => s + p.traps, 0) / out.length;
console.log('덤 조각 평균 ' + avg.toFixed(1) + '개 · 하나도 못 얹은 판 ' + none + ' → ' + OUT);
