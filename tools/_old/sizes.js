// 각 판에서 정답을 밟는 동안 몸이 몇까지 커지는지 잰다.
// "슬라임이 너무 크다"는 이 숫자 문제다 — 안 재고 있었다.
const fs = require('fs'), path = require('path');
const E = require('./engine.js').SlimeEngine;
const doc = JSON.parse(fs.readFileSync(path.join(__dirname,'..','game','Assets','Resources','levels.json'),'utf8'));

for (const lv of doc.levels) {
  const L = E.parse(lv);
  let st = E.startState(L);
  if (!st) { console.log(`${lv.id.padEnd(10)} 시작 불가`); continue; }
  const sizes = [st.n];
  for (const c of lv.sol) {
    st = E.move(L, st, c === '→' ? 1 : -1);
    if (!st) break;
    sizes.push(st.n);
  }
  const open = L.grid.filter(r => r.includes('.')).length;   // 대략적인 세로 여유
  console.log(
    `${lv.id.padEnd(10)} 시작표기 ${lv.startSize} → 실제시작 ${sizes[0]} · ` +
    `최대 ${Math.max(...sizes)} · 판높이 ${L.h}(열린줄 ${L.h-2})   [${sizes.join(' ')}]`);
}
