// 🔴 **기믹을 다 담은 시험 판** 하나를 짓는다 (2026-09-05, rev.10).
//
// 사장님(09-05): *"일단 테스트 맵 하나 만들어서 거기에 모든 아이템, 기믹들 다 보여줘봐."*
//
// ⚠️ 손으로 그리지 않는다 — 브리프 원칙 2: **판은 기계가 검증한 것만 넣는다.**
//    (예전에 손으로 그린 판 아홉 중 다섯만 풀렸다)
//
// 담는 것: 조각 + · 홈 셋(= * / - % / ~ @) · 별 o · 덤 조각 · 문벽 1 2 · 무중력 : · 상자 B
//
//   node tools/showcase.js            지어서 보여만 준다
//   WRITE=1 node tools/showcase.js    levels.json 의 1-1 자리에 끼운다
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { grow, rng, SHAPES } = require('./grow.js');
const { addGates } = require('./gates.js');
const { shiftBoxes, loadBearing } = require('./boxes.js');
const { putStar, cutOf } = require('./star.js');

//  🔴 상한을 걸어 둔다 — 상자가 들어오면 상태가 터진다. 09-05 에 이걸로 node 가 죽었다.
const CAP = +(process.env.CAP || 400000);
const solve = (grid, o) => E.solve({ grid, gravity: true, clear: 'all', id: 's' },
                                   Object.assign({ cap: CAP }, o || {}));
const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };

/// 정답이 안 지나가는 칸에 **덤 조각**을 얹는다 (trapall.js 와 같은 규칙)
function addTraps(grid, want) {
  const base = solve(grid);
  if (!base.ok) return grid;
  const L = E.parse({ grid, gravity: true, clear: 'all', id: 't' });
  const walked = new Set();
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
  const G = grid.map(r => r.split(''));
  const H = G.length, W = G[0].length;
  let put = 0;
  for (let y = 1; y < H - 1 && put < want; y++) for (let x = 1; x < W - 1 && put < want; x++) {
    if (G[y][x] !== '.') continue;
    const c = y * W + x;
    if (walked.has(c)) continue;
    let near = false;
    for (const n of [c - 1, c + 1, c - W, c + W]) if (walked.has(n)) near = true;
    if (!near) continue;
    G[y][x] = '+';
    const a = solve(G.map(r => r.join('')));
    if (a.ok && a.moves === base.moves && a.shortest === 1) put++;
    else G[y][x] = '.';
  }
  return G.map(r => r.join(''));
}

const has = (g, ch) => g.join('').includes(ch);
//  판을 조금 줄였다 — 17x11 에 홈 셋 + 상자면 상태가 40만을 넘는다
const LENS = [[3, 3, 3], [3, 3], [4, 3]];

let best = null;
for (const lens of LENS) {
  for (let seed = 1; seed <= 6000 && !best; seed++) {
    const r = rng(seed * 7919 + lens.length * 131);
    const shape = SHAPES[Math.floor(r() * SHAPES.length)];
    let g = grow(r, 15, 10, lens, 0.30, 6, 14, shape, 2, 1);
    if (!g || !has(g, ':') || !has(g, 'B')) continue;
    if (!loadBearing(g)) continue;                 // 상자가 장식이면 보여줄 게 아니다
    let a = solve(g);
    if (!a.ok || a.shortest !== 1) continue;

    //  🔴 **밀어야 쓰는** 상자여야 한다. 그냥 놓여 있으면 발판이지 기믹이 아니다 —
    //     테스터가 원한 건 "박스 밀어가지고 높이 만들어서" 였다.
    //     (RELAX=1 이면 못 찾을 때 놓여 있는 것도 받는다)
    const sh = shiftBoxes(g, 3);
    if (!sh && process.env.RELAX !== '1') continue;
    if (sh) g = sh.grid;

    const gt = addGates(g, 2);                     // 문벽
    if (!gt) continue;
    g = gt.grid;

    g = addTraps(g, 3);                            // 덤 조각

    a = solve(g);
    if (!a.ok || a.shortest !== 1) continue;
    const st = putStar(g, a.moves, rng(seed), 20, 6);   // 별
    if (!st) continue;
    const a2 = solve(st.grid);
    if (!a2.ok || a2.shortest !== 1) continue;

    best = {
      grid: st.grid, best: a2.moves, sol: a2.path,
      bestStar: st.moves, cut: cutOf(st.moves),
      gates: gt.gates, pushed: sh ? sh.moved : 0,
      zones: (st.grid.join('').match(/:/g) || []).length,
      boxes: (st.grid.join('').match(/B/g) || []).length,
      doors: lens.length,
    };
  }
  if (best) break;
}

if (!best) { console.log('🔴 다 담은 판을 못 지었다 — 조건을 늘려야 한다'); process.exit(1); }

console.log(best.grid.join('\n'));
console.log('');
console.log('홈 ' + best.doors + ' · 문벽 ' + best.gates + ' · 무중력 ' + best.zones +
            ' · 상자 ' + best.boxes + (best.pushed ? ' (밀어야 쓴다)' : ' (놓여 있다)'));
console.log('걸음 ' + best.best + ' · 별 먹고 ' + best.bestStar + ' · 커트 ' + best.cut);
console.log('조각 ' + (best.grid.join('').match(/\+/g) || []).length + '개');

if (process.env.WRITE !== '1') { console.log('\n(보여주기만 했다. 쓰려면 WRITE=1)'); process.exit(0); }

const LP = path.join(__dirname, '../game/Assets/Resources/levels.json');
const d = JSON.parse(fs.readFileSync(LP, 'utf8'));
fs.writeFileSync(path.join(__dirname, 'levels.bak.json'), fs.readFileSync(LP));
//  🔴 1-1 자리에 끼운다. 있던 판은 뒤로 한 칸씩 민다 — 아무것도 안 버린다.
const rest = d.levels.filter(l => !l.tutorial);
const tut = d.levels.find(l => l.tutorial);
const show = {
  id: '1-1', grid: best.grid, clear: 'all',
  best: best.best, sol: best.sol, bestStar: best.bestStar, cut: best.cut,
  lost: 0, tight: 0, backtrack: 0, states: 0,
};
d.levels = [tut, show, ...rest.map((l, i) => Object.assign({}, l, { id: '1-' + (i + 2) }))];
fs.writeFileSync(LP, JSON.stringify(d, null, 2) + '\n');
console.log('\n1-1 자리에 끼웠다 · 판 ' + (d.levels.length - 1) + '개 (+튜토리얼)');
