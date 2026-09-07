// 🔴 **판을 쉬운 것부터 늘어놓는다** (2026-09-05 사장님: "1-1 어떻게 깸..?" "1-2도 못 깨겠는데?").
//
// 지금까지 tools/review.js 는 **일부러 섞어서** 넣었다 — 사장님이 한 묶음을 통째로
// 해보시고 고르는 용도였기 때문이다. 그런데 이제 이게 **그대로 게임의 판 순서**가 되니,
// 튜토리얼 바로 다음에 홈 둘 · 35걸음짜리가 오는 일이 생겼다.
//
// ⚠️ **걸음 수로 줄 세우지 않는다** (09-05 사장님: "걸음수가 곧 난이도는 아니야 절대").
//    대신 사람이 한 번에 붙들어야 하는 것의 개수로 잰다:
//      · 홈이 몇 개인가 (하나면 한 번만 생각하면 된다)
//      · 홈이 몇 칸인가 (길수록 접는 모양을 그리기 어렵다)
//      · **덤 조각이 몇 개인가** (먹으면 안 되는 것을 골라내야 한다)
//      · 홈을 지나갔다 되나오나 · 홈 위를 밟고 지나가나
//      · 기믹이 몇 번 발동하나 (얼음·금 간 바닥)
//
//   node tools/order.js          보여만 준다
//   WRITE=1 node tools/order.js  levels.json 을 다시 늘어놓는다
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const LP = path.join(__dirname, '../game/Assets/Resources/levels.json');
const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };

/// 정답을 되짚어 이 판이 사람에게 무엇을 시키는지 센다
function weigh(l) {
  const L = E.parse({ grid: l.grid, gravity: true, clear: 'all', id: l.id });
  let st = E.startState(L);
  let re = 0, thru = 0, slid = 0, broke0 = 0;
  const inside = new Array(L.doors.length).fill(false);
  for (const ch of l.sol || '') {
    const d = SYM[ch];
    if (d === undefined) continue;
    const n = E.step(L, st, d);
    if (!n) break;
    for (let i = 0; i < L.doors.length; i++) {
      if ((n.dm || 0) & (1 << i)) { inside[i] = false; continue; }
      const on = L.doors[i].set.has(n.body[0]);
      if (on) thru++;
      if (inside[i] && !on) re++;
      inside[i] = on;
    }
    if ((n._n || 1) >= 2) slid++;
    st = n;
  }
  for (let k = 0; k < L.crack.length; k++) if ((st.cm || 0) & (1 << k)) broke0++;

  //  🔴 덤 조각 — 있는 조각에서 꼭 먹어야 하는 수를 뺀 것.
  //     "먹으면 안 되는 게 섞여 있다"가 이 게임에서 제일 무거운 짐이다.
  const need = L.doors.reduce((s, d) => s + d.cells.length - 1, 0);
  const spare = Math.max(0, L.foods.length - need);
  const big = Math.max(...L.doors.map(d => d.cells.length));

  const score = (L.doors.length - 1) * 6      // 홈이 둘이면 두 번 생각해야 한다
              + Math.max(0, big - 4) * 3      // 홈이 길수록 접는 모양이 어렵다
              + spare * 2                     // 덤 조각
              + re * 4                        // 홈에 들어갔다 되나오기
              + thru * 0.4                    // 홈 위를 지나가기
              + (slid + broke0) * 1.5;        // 기믹 발동
  return { score, doors: L.doors.length, big, spare, re, thru, slid, broke: broke0 };
}

const data = JSON.parse(fs.readFileSync(LP, 'utf8'));
const tut = data.levels.filter(l => l.demo || l.tutorial || l.id === '1-0');
const rest = data.levels.filter(l => !tut.includes(l));

const rows = rest.map(l => ({ l, ...weigh(l) }));
rows.sort((a, b) => a.score - b.score);

console.log('순서  옛번호  크기     걸음  홈 큰홈 덤조각 되나옴 통과 미끄럼 무너짐  무게');
rows.forEach((r, k) => console.log(
  String(k + 1).padStart(4) + '  ' + r.l.id.padEnd(7) +
  (r.l.grid[0].length + 'x' + r.l.grid.length).padEnd(8) +
  String(r.l.best).padStart(4) + String(r.doors).padStart(4) +
  String(r.big).padStart(5) + String(r.spare).padStart(6) +
  String(r.re).padStart(7) + String(r.thru).padStart(5) +
  String(r.slid).padStart(6) + String(r.broke).padStart(7) +
  r.score.toFixed(1).padStart(7)));

if (process.env.WRITE !== '1') { console.log('\n(보여주기만 했다. 쓰려면 WRITE=1)'); process.exit(0); }

fs.writeFileSync(path.join(__dirname, 'levels.bak.json'), JSON.stringify(data, null, 1) + '\n');
//  🔴 번호를 다시 매긴다. 판 내용은 **한 글자도 안 건드린다** — 순서만 바꾼다.
rows.forEach((r, k) => { r.l.id = '1-' + (k + 1); });
data.levels = tut.concat(rows.map(r => r.l));
fs.writeFileSync(LP, JSON.stringify(data, null, 1) + '\n');
console.log('\n다시 늘어놓았다 · ' + rows.length + '판 (튜토리얼 ' + tut.length + '판은 그대로)');
console.log('⚠️ 옛 순서는 tools/levels.bak.json 에 있다');
