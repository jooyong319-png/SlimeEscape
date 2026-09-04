// 🔴 **홈이 얼마나 접혀 있나**를 잰다 (2026-09-05 밤).
//
// 사장님: *"48 만든거 너무 쉽다."* → 나는 조각 개수를 의심했다.
//         *"조각 문제가 아니야. 14,15,16 봐봐."* → 보니 **홈 모양**이었다.
//
//   옛 1-14 홈:  .=.        새 1-48(버린 것) 홈:  *===
//                ==.                             %---
//                ==.                             @~~
//                .*.
//
// 접힌 홈은 슬라임을 **그 모양으로 접어 넣어야** 한다.
// 일자 홈은 **길이만 맞춰 쭉 밀어 넣으면** 된다 — 전혀 다른 문제다.
//
// 🔴 이건 걸음·압박·밀도 어느 자로도 안 잡혔다. 판을 눈으로 봐야 보였다.
//
//   node tools/shape.js                    판 자료를 잰다
//   node tools/shape.js tools/trap-pool.json   못을 잰다
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const file = process.argv[2] || path.join(__dirname, '../game/Assets/Resources/levels.json');
const raw = JSON.parse(fs.readFileSync(file, 'utf8'));
const list = (raw.levels || raw).filter(l => l.grid && !l.tutorial && !l.demo);

/// 홈 하나의 **접힘값** — 짧은 변 × 칸수. 일자는 짧은 변이 1 이라 낮다.
function doorShape(L, dr) {
  const xs = dr.cells.map(c => c % L.w), ys = dr.cells.map(c => (c / L.w) | 0);
  const w = Math.max(...xs) - Math.min(...xs) + 1;
  const h = Math.max(...ys) - Math.min(...ys) + 1;
  return { w, h, n: dr.cells.length, thin: Math.min(w, h), fold: Math.min(w, h) * dr.cells.length };
}

const rows = [];
for (const l of list) {
  let L; try { L = E.parse({ grid: l.grid, gravity: true, clear: 'all', id: l.id || 'x' }); }
  catch (e) { continue; }
  const ds = L.doors.map(d => doorShape(L, d));
  rows.push({
    id: l.id || '#' + rows.length,
    moves: l.best,
    doors: ds.length,
    fat: ds.filter(s => s.thin > 1).length,
    big: Math.max(...ds.map(s => s.n)),
    fold: ds.reduce((s, x) => s + x.fold, 0),
    shape: ds.map(s => s.n + '칸 ' + s.w + 'x' + s.h).join(' · '),
  });
}

const tot = rows.reduce((s, r) => s + r.doors, 0);
const fat = rows.reduce((s, r) => s + r.fat, 0);
console.log(file);
console.log('판 ' + rows.length + ' · 홈 ' + tot + ' · **접힌 홈 ' + fat + ' (' + (fat / tot * 100).toFixed(0) + '%)**');
console.log('접힘값 평균 ' + (rows.reduce((s, r) => s + r.fold, 0) / rows.length).toFixed(1) +
            ' · 제일 큰 홈 평균 ' + (rows.reduce((s, r) => s + r.big, 0) / rows.length).toFixed(1) + '칸');
console.log('');
const flat = rows.filter(r => r.fat === 0);
if (flat.length) {
  console.log('🔴 홈이 **전부 일자**인 판 ' + flat.length + '개 — 길이만 맞추면 된다:');
  for (const r of flat.sort((a, b) => b.moves - a.moves).slice(0, 14))
    console.log('  ' + String(r.id).padEnd(8) + String(r.moves).padStart(3) + '걸음  ' + r.shape);
} else console.log('🟢 홈이 전부 일자인 판은 없다');
