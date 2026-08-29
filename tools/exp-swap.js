// 🔬 요소 ② 머리 바꾸기 — 넣으면 무슨 일이 벌어지나.
//
//   node tools/exp-swap.js
//
// 🔴 정본 엔진(engine.js)은 안 건드린다. 여기서만 걸음 하나를 더 준다.
//
// 규칙 안: **머리와 꼬리를 맞바꾼다. 한 걸음을 쓴다.**
//   · 칸이 하나도 안 움직이므로 지지도 그대로다 — 뒤집어도 안 떨어진다
//     (중력이 들어오면서 요소 ②에 붙었던 물음표 하나가 이걸로 풀린다)
//   · 막히는 일이 없다. 언제나 쓸 수 있다
//   · 먹지 않는다 (칸에 새로 들어가는 게 아니므로)
//
// 재는 것 — 전부 **사실**이다. 재미를 예측하려는 게 아니다.
//   1. 🔴 이미 만든 판이 쉬워지나 (최단 걸음이 줄어드나)
//   2. 상태 공간이 얼마나 커지나 (솔버가 버티나)
//   3. 못 풀던 판이 풀리게 되나
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const FILE = path.join(__dirname, '..', 'game', 'Assets', 'Resources', 'levels.json');
const doc = JSON.parse(fs.readFileSync(FILE, 'utf8'));
const CAP = 3000000;

const swap = st => ({ body: st.body.slice().reverse(), fm: st.fm, pg: st.pg || 0 });

/// 최단해를 찾는다. withSwap이면 걸음 하나(머리 바꾸기)를 더 준다.
function solve(L, withSwap) {
  const start = E.startState(L);
  if (E.isWin(L, start)) return { ok: true, moves: 0, states: 1, shortest: 1 };
  const seen = new Map([[E.keyOf(start), 0]]);
  let frontier = [start];
  for (let depth = 1; depth <= 200; depth++) {
    const next = [];
    let wins = 0;
    for (const st of frontier) {
      const kids = [];
      for (let d = 0; d < 4; d++) { const ns = E.step(L, st, d); if (ns) kids.push(ns); }
      if (withSwap) kids.push(swap(st));
      for (const ns of kids) {
        const k = E.keyOf(ns);
        if (seen.has(k)) continue;
        if (E.isWin(L, ns)) wins++;
        seen.set(k, depth);
        if (seen.size > CAP) return { ok: false, why: '상태가 너무 많다', states: seen.size };
        next.push(ns);
      }
    }
    if (wins) return { ok: true, moves: depth, states: seen.size, shortest: wins };
    if (!next.length) break;
    frontier = next;
  }
  return { ok: false, why: '해가 없음', states: seen.size };
}

console.log('판    지금(4수)          머리바꾸기 있음(5수)     달라진 것');
console.log('      걸음  최단해  상태   걸음  최단해  상태');
let shorter = 0, same = 0;
for (const j of doc.levels) {
  const L = E.parse({ grid: j.grid, gravity: doc.gravity !== false });
  const a = solve(L, false);
  const b = solve(L, true);
  if (!a.ok || !b.ok) {
    console.log(j.id.padEnd(6) + '  ' + (a.ok ? a.moves : a.why) + ' / ' + (b.ok ? b.moves : b.why));
    continue;
  }
  const diff = a.moves - b.moves;
  if (diff > 0) shorter++; else same++;
  console.log(
    j.id.padEnd(6) +
    String(a.moves).padStart(5) + String(a.shortest).padStart(7) + String(a.states).padStart(8) +
    String(b.moves).padStart(7) + String(b.shortest).padStart(7) + String(b.states).padStart(8) +
    '   ' + (diff > 0 ? '🔴 ' + diff + '걸음 짧아짐' : '같음') +
    '  · 상태 ' + (b.states / a.states).toFixed(1) + '배'
  );
}
console.log('\n짧아진 판 ' + shorter + '개 · 그대로인 판 ' + same + '개');
