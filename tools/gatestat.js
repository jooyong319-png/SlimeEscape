// 🔴 **문벽이 압박을 올리나 내리나**를 짝비교로 잰다 (2026-09-05).
//
// 덤 조각은 "틀린 수를 새로 만드는" 거라 압박이 올랐다.
// 문벽은 **길을 막는** 거라 오히려 내릴 수도 있다 — 갈 데가 없으면 고민도 없다.
// 짐작하지 말고 잰다.
//
//   node tools/gatestat.js
'use strict';
const fs = require('fs');
const path = require('path');
const { addGates } = require('./gates.js');

const src = fs.readFileSync(path.join(__dirname, 'hard.js'), 'utf8');
const mod = { exports: {} };
new Function('module', 'exports', 'require', '__dirname',
             src.slice(0, src.indexOf('const fs = require')) +
             '\nmodule.exports = { measure };')(mod, mod.exports, require, __dirname);
const { measure } = mod.exports;

const N = +(process.env.N || 25);
const pool = JSON.parse(fs.readFileSync('tools/trap-pool.json', 'utf8'));
const stat = JSON.parse(fs.readFileSync('tools/trapstat-after.json', 'utf8'));

//  홈 둘 이상 + 이미 잰 판 + 상태가 작은 것부터 (재는 게 비싸다)
const rows = stat.filter(r => pool[r.i] && pool[r.i].doors >= 2 && r.states < 150000)
                 .sort((a, b) => a.states - b.states).slice(0, N);
console.log('짝비교', rows.length, '판');
console.log('판    홈  걸음    압박 전 → 후      되짚기 전 → 후   문벽');
console.log('-'.repeat(60));

let up = 0, same = 0, down = 0, sB = 0, sA = 0, n = 0, gsum = 0;
for (const r of rows) {
  const g = addGates(pool[r.i].grid, 3);
  if (!g) continue;
  const m = measure({ grid: g.grid, gravity: true, clear: 'all', id: 'g' });
  if (!m) continue;
  n++; sB += r.press; sA += m.press; gsum += g.gates;
  if (m.press > r.press + 0.005) up++; else if (m.press < r.press - 0.005) down++; else same++;
  if (n <= 16) console.log(String(r.i).padStart(4) + String(pool[r.i].doors).padStart(4) +
    String(r.moves).padStart(6) + (r.press * 100).toFixed(1).padStart(9) + ' →' +
    (m.press * 100).toFixed(1).padStart(6) + (r.back * 100).toFixed(0).padStart(12) + ' →' +
    (m.back * 100).toFixed(0).padStart(5) + String(g.gates).padStart(6));
}
console.log('-'.repeat(60));
console.log('잰 판 ' + n + ' · 올랐다 ' + up + ' · 그대로 ' + same + ' · 내렸다 ' + down);
console.log('평균 압박  ' + (sB / n * 100).toFixed(1) + '%  →  ' + (sA / n * 100).toFixed(1) + '%');
console.log('문벽 평균 ' + (gsum / n).toFixed(1) + '개');
