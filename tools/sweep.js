// 🔴 **어느 손잡이가 압박을 올리는가**를 잰다 (2026-09-04).
//
// 못 284개를 재봤더니 압박 20% 이상이 14개뿐이었다. 다시 골라도 47판을 못 채운다.
// 고르기로 안 되니 **만드는 쪽**을 고쳐야 하는데 — 어디를 돌려야 하는지 모른다.
// 짐작으로 고치면 또 헛돈다. 그래서 흔들어 보고 잰다.
//
//   node tools/sweep.js
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { grow, rng, SHAPES } = require('./grow.js');

// hard.js 의 measure 만 꺼내 쓴다 (require 하면 판 전체를 재버린다)
const src = fs.readFileSync(path.join(__dirname, 'hard.js'), 'utf8');
const mod = { exports: {} };
new Function('module', 'exports', 'require', '__dirname',
             src.slice(0, src.indexOf('const fs = require')) +
             '\nmodule.exports = { measure };')(mod, mod.exports, require, __dirname);
const { measure } = mod.exports;

// 🔴 한 번에 하나씩만 흔든다. 두 개를 같이 흔들면 무엇이 효과인지 못 가린다.
const BASE = { W: 17, H: 10, ratio: 0.43, wmin: 10, wmax: 22, lens: [4, 3] };
const ONLY = process.env.ONLY ? process.env.ONLY.split(',') : null;
const KNOBS = [
  ['그대로',        {}],
  ['좁게 0.34',     { ratio: 0.34 }],
  ['좁게 0.28',     { ratio: 0.28 }],
  ['좁게 0.22',     { ratio: 0.22 }],
  ['홈 길게 [6,5]', { lens: [6, 5] }],
  ['홈 셋 [4,4,4]', { lens: [4, 4, 4] }],
  ['좁고 길게',     { ratio: 0.28, lens: [6, 5] }],
  ['좁고 높게',     { ratio: 0.28, H: 14 }],
  ['많이 헤매기',   { wmin: 30, wmax: 60 }],
  //  🔴 제일 센 두 손잡이를 겹쳐 본다. 첫 실험에 이 조합이 빠져 있었다.
  ['좁게+홈셋',     { ratio: 0.28, lens: [4, 4, 4] }],
  ['더좁게+홈셋',   { ratio: 0.22, lens: [4, 4, 4] }],
  ['좁게+홈셋+높게', { ratio: 0.26, lens: [4, 4, 4], H: 13 }],
  ['좁게+홈넷',     { ratio: 0.28, lens: [4, 3, 3, 3] }],
];

const N = +(process.env.N || 26);      // 손잡이마다 만들 판 수
const CAPN = +(process.env.CAPN || 8); // 재는 판 수 (재는 게 비싸다)

console.log('손잡이           만듦  잼   압박 중앙  압박 상위25  되짚기  걸음');
console.log('-'.repeat(72));

for (const [name, over] of KNOBS) {
  if (ONLY && !ONLY.includes(name)) continue;
  const b = Object.assign({}, BASE, over);
  const grids = [];
  for (let seed = 1; seed <= 500 && grids.length < N; seed++) {
    const r = rng(seed * 7919 + b.W * 131 + Math.round(b.ratio * 1000));
    const shape = SHAPES[Math.floor(r() * SHAPES.length)];
    const g = grow(r, b.W, b.H, b.lens, b.ratio, b.wmin, b.wmax, shape);
    if (!g) continue;
    const a = E.solve({ grid: g, gravity: true, clear: 'all', id: 'x' });
    if (!a.ok || a.shortest !== 1 || a.moves < 6) continue;
    grids.push(g);
  }
  const ms = [];
  for (const g of grids.slice(0, CAPN)) {
    const m = measure({ grid: g, gravity: true, clear: 'all', id: 'x' });
    if (m) ms.push(m);
  }
  const q = (arr, p) => {
    if (!arr.length) return 0;
    const s = arr.slice().sort((x, y) => x - y);
    return s[Math.min(s.length - 1, Math.floor(s.length * p))];
  };
  const P = ms.map(m => m.press * 100);
  const B = ms.map(m => m.back * 100);
  const M = ms.map(m => m.moves);
  console.log(name.padEnd(16) +
    String(grids.length).padStart(4) + String(ms.length).padStart(5) +
    q(P, 0.5).toFixed(1).padStart(10) + q(P, 0.75).toFixed(1).padStart(12) +
    q(B, 0.5).toFixed(0).padStart(8) + q(M, 0.5).toFixed(0).padStart(7));
}
