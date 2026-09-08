// 🔴 **별 걸음과 커트를 다시 잰다** (2026-09-08).
//
// 사장님: *"야 판이 끝나는데 어떻게 별을 먹어?"*
// 솔버가 틀렸었다 — 별을 찾을 때 **「홈을 다 채우면 거기서 끝난다」를 안 보고**
// 그 상태를 지나쳐 계속 탐색했다. 그래서 26수에 끝나는 판에
// **「별 포함 33수」라는 있을 수 없는 값**을 적어뒀고, 커트도 그걸 기준으로 박혔다.
//
// 엔진을 고쳤으니 판마다 다시 잰다. 셋 중 하나가 된다:
//   ① 별 걸음이 그대로  → 손댈 것 없다
//   ② 별 걸음이 늘어남  → 커트를 다시 박는다 (안 그러면 여유가 0이라 사실상 3별 불가)
//   ③ 별을 먹고는 못 깸 → 🔴 별을 **뺀다.** 못 받는 별을 화면에 두면 안 된다
//
//   node tools/restar.js            보여만 준다
//   WRITE=1 node tools/restar.js    판 자료에 적는다
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { cutOf } = require('./star.js');

const LP = path.join(__dirname, '../game/Assets/Resources/levels.json');
const CAP = +(process.env.SOLVE_CAP || 3000000);
const solve = (g, o) => E.solve({ grid: g, gravity: true, clear: 'all', id: 'r' },
                                Object.assign({ cap: CAP }, o || {}));

const d = JSON.parse(fs.readFileSync(LP, 'utf8'));
let same = 0, moved = 0, dropped = 0;

console.log('판     걸음  옛별  새별  옛컷  새컷   무엇이');
for (const l of d.levels) {
  if (l.demo) continue;
  if (!l.grid.join('').includes('o')) continue;        // 별이 없는 판은 건너뛴다
  const a = solve(l.grid);
  if (!a.ok) { console.log(l.id.padEnd(6) + '  🔴 못 푼다'); continue; }
  const s = solve(l.grid, { needStar: true });

  const oldStar = l.bestStar, oldCut = l.cut;
  let note;
  if (!s.ok) {
    //  🔴 별을 먹고는 못 깬다 — 받을 수 없는 별이다. 격자에서 뺀다.
    l.grid = l.grid.map(r => r.replace('o', '.'));
    l.bestStar = a.moves;
    l.cut = 0;                                          // 커트가 없으면 별 둘까지만
    note = '🔴 못 받는 별 — 뺐다';
    dropped++;
  } else if (s.moves === oldStar) {
    l.cut = cutOf(s.moves);
    note = oldCut === l.cut ? '그대로' : '커트만 고침';
    if (oldCut === l.cut) same++; else moved++;
  } else {
    l.bestStar = s.moves;
    l.cut = cutOf(s.moves);
    note = '별걸음이 틀렸었다';
    moved++;
  }
  console.log(l.id.padEnd(6) + String(a.moves).padStart(5) +
              String(oldStar).padStart(6) + String(l.bestStar).padStart(6) +
              String(oldCut).padStart(6) + String(l.cut).padStart(6) + '   ' + note);
}

console.log(`\n그대로 ${same} · 고침 ${moved} · 별 뺌 ${dropped}`);
if (process.env.WRITE !== '1') { console.log('(보여주기만 했다. 쓰려면 WRITE=1)'); process.exit(0); }
fs.writeFileSync(path.join(__dirname, 'levels.bak.json'), JSON.stringify(d, null, 2) + '\n');
fs.writeFileSync(LP, JSON.stringify(d, null, 2) + '\n');
console.log('판 자료에 적었다');
