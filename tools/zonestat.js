// 🔴 **무중력 구역이 압박을 올리나**를 잰다 (2026-09-05, rev.10).
//
// 문벽은 재보니 중립이었다 (14.5% → 14.7%). 무중력은 다를까?
// 무중력은 **길을 막는 게 아니라 늘리는** 것이라, 갈림길이 늘어 압박이 오를 수도
// 있고, 오르기 규칙을 비켜가게 해서 되레 쉬워질 수도 있다. 짐작하지 말고 잰다.
//
// ⚠️ 짝비교가 안 된다 — 무중력은 **지으면서** 넣는 거라 같은 판을 못 만든다.
//    그래서 같은 씨앗으로 두 벌을 만들어 **분포**를 견준다. 표본을 넉넉히 본다.
//
//   node tools/zonestat.js
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { grow, rng, SHAPES } = require('./grow.js');

const src = fs.readFileSync(path.join(__dirname, 'hard.js'), 'utf8');
const mod = { exports: {} };
new Function('module', 'exports', 'require', '__dirname',
             src.slice(0, src.indexOf('const fs = require')) +
             '\nmodule.exports = { measure };')(mod, mod.exports, require, __dirname);
const { measure } = mod.exports;

const N = +(process.env.N || 30);      // 예산마다 잴 판 수
const q = (a, p) => { const s = a.slice().sort((x, y) => x - y); return s[Math.min(s.length - 1, Math.floor(s.length * p))]; };

console.log('무중력  잰 판   압박 중앙  상위25%  상위10%   최대    되짚기  걸음  무중력칸');
console.log('-'.repeat(78));
for (const z of [0, 2]) {
  const ms = [];
  for (let seed = 1; seed <= 2000 && ms.length < N; seed++) {
    const r = rng(seed * 7919 + z * 131);
    const shape = SHAPES[Math.floor(r() * SHAPES.length)];
    const g = grow(r, 13, 10, [4, 3], 0.28, 6, 14, shape, z);
    if (!g) continue;
    const a = E.solve({ grid: g, gravity: true, clear: 'all', id: 'z' });
    if (!a.ok || a.shortest !== 1 || a.moves < 8) continue;
    if (z > 0 && !g.join('').includes(':')) continue;      // 무중력 없는 판은 견줄 게 아니다
    const m = measure({ grid: g, gravity: true, clear: 'all', id: 'z' });
    if (m) { m.nz = g.join('').split(':').length - 1; ms.push(m); }
  }
  const P = ms.map(m => m.press * 100), B = ms.map(m => m.back * 100), M = ms.map(m => m.moves);
  console.log(String(z).padEnd(7) + String(ms.length).padStart(5) +
    q(P, 0.5).toFixed(1).padStart(11) + q(P, 0.75).toFixed(1).padStart(9) +
    q(P, 0.90).toFixed(1).padStart(9) + q(P, 1).toFixed(1).padStart(8) +
    q(B, 0.5).toFixed(0).padStart(9) + q(M, 0.5).toFixed(0).padStart(6) +
    (ms.reduce((s, m) => s + m.nz, 0) / Math.max(1, ms.length)).toFixed(1).padStart(9));
}
