// 🔴 게임 판의 best/sol 을 **지금 엔진으로** 다시 잰다 (09-23).
//    combo.js 로 뽑아 넣은 판 중 저장값이 지금 엔진과 다른 판이 있었다 (portal-329: 저장 36, 실제 44 —
//    정답 재생이 18걸음째에 막힌다). 커밋해 둔 엔진으로도 같아서 이번 수정 탓은 아니다.
//    바뀐 판만 고치고, 못 푸는 판은 목록으로 남긴다 (지우지 않는다 — 사장님이 거른다).
//   node tools/restamp.js
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const P = path.join(__dirname, '..', 'game', 'Assets', 'Resources', 'levels.json');
const src = fs.readFileSync(P, 'utf8');
const eol = src.includes('\r\n') ? '\r\n' : '\n';
const doc = JSON.parse(src);
let same = 0, fixed = [], broken = [];
for (const l of doc.levels) {
  const a = E.solve({ grid: l.grid, gravity: true, clear: l.clear, id: l.id }, { cap: 3000000 });
  if (!a.ok) { broken.push(l.id + '(' + a.why + ')'); continue; }
  if (a.moves === l.best && a.path === l.sol) { same++; continue; }
  //  같은 걸음 수에 다른 정답이면 정답만 바꾼다 — 판 고르기 표(걸음)는 안 흔들린다
  if (a.moves !== l.best) fixed.push(l.id + ' ' + l.best + '→' + a.moves);
  l.best = a.moves; l.sol = a.path; l.bestStar = a.moves; l.cut = a.moves + 3;
}
const txt = JSON.stringify(doc, null, 2).split('\n').join(eol) + eol;
JSON.parse(txt); fs.writeFileSync(P, txt);
console.log('그대로 ' + same + ' · 걸음 고침 ' + fixed.length + ' · 못 풂 ' + broken.length);
fixed.forEach(s => console.log('  ' + s));
broken.forEach(s => console.log('  ✗ ' + s));
