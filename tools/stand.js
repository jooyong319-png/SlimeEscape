// 🔴 **굳은 몸을 딛고 오르는가**를 잰다 (2026-09-05 밤, 사장님).
//
// 사장님: *"35가 재밌었던 건 내가 홈으로 만든 몸을 올라가야 하는 기믹이어서 신선했어."*
//
// 이건 **이미 규칙에 있다** (08-30: "두고 온 몸은 디딤돌이다").
// 홈을 채우면 그 자리에 몸이 굳고, 그 위에 **설 수 있다.**
// 새 규칙을 하나도 안 배우고 재미가 나오는 자리다 — *내가 채운 홈이 곧 지형이 된다.*
//
// ⚠️ 그런데 **그걸 일부러 시키는 판이 없다.** 우연히 나온 판만 있다.
//
// 재는 것:
//   딛는걸음 — 정답 도중, **굳은 몸이 아니면 떨어질 자리**에 서 있는 걸음 수
//   오름     — 그렇게 딛고 **위로 올라간** 횟수 (진짜 "딛고 올랐다")
//
//   node tools/stand.js            판 자료
//   node tools/stand.js old        옛 17판
//   node tools/stand.js <파일>     못
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };
const CAP = +(process.env.CAP6 || 2500000);

/// 이 몸이 **굳은 몸 덕분에** 떠 있나 — 굳은 몸을 빼면 떨어지는가
function standsOnSpent(L, st) {
  const dm = st.dm || 0;
  if (!dm) return false;
  let bySpent = false;
  for (const c of st.body) {
    const below = c + L.w;
    if (below >= L.w * L.h) return false;                 // 판 바닥
    const y = (below / L.w) | 0, x = below % L.w;
    if (L.g[y][x] === '#') return false;                  // 벽이 받쳐준다 — 굳은 몸 덕이 아니다
    //  이미 연 홈의 칸인가 = 굳은 몸
    for (let i = 0; i < L.doors.length; i++)
      if ((dm & (1 << i)) && L.doors[i].set.has(below)) { bySpent = true; break; }
  }
  return bySpent;
}

function judge(l) {
  const def = { grid: l.grid, gravity: true, clear: l.clear || 'all', id: l.id || 'x' };
  let L; try { L = E.parse(def); } catch (e) { return null; }
  const a = E.solve(def, { cap: CAP });
  if (!a.ok) return null;
  let st = E.startState(L);
  let stand = 0, climb = 0, was = false;
  for (const ch of a.path) {
    const d = SYM[ch]; if (d === undefined) continue;
    const ns = E.step(L, st, d); if (!ns) break;
    const on = standsOnSpent(L, ns);
    if (on) stand++;
    //  🔴 굳은 몸을 딛고 **위로** 갔으면 그게 "딛고 올랐다"
    if (on && d === 0) climb++;
    st = ns; was = on;
  }
  return { id: l.id, moves: a.moves, stand, climb };
}

const arg = process.argv[2];
let list, title;
if (arg === 'old') {
  const o = JSON.parse(fs.readFileSync(path.join(process.env.TEMP || '/tmp', 'old16.json'), 'utf8'));
  list = o.levels.filter(l => !l.tutorial); title = '옛 17판';
} else if (arg) {
  const raw = JSON.parse(fs.readFileSync(arg, 'utf8'));
  list = (raw.levels || raw).filter(l => l.grid && !l.tutorial && !l.demo); title = arg;
} else {
  const d = JSON.parse(fs.readFileSync(path.join(__dirname, '../game/Assets/Resources/levels.json'), 'utf8'));
  list = d.levels.filter(l => !l.tutorial && !l.demo); title = '지금 판 자료';
}

const rows = [];
for (const l of list) { const r = judge(l); if (r) rows.push(r); }
console.log(title + ' — ' + rows.length + '판');
const hit = rows.filter(r => r.stand > 0).sort((a, b) => b.climb - a.climb || b.stand - a.stand);
console.log('굳은 몸을 딛는 판 ' + hit.length + ' / ' + rows.length +
            ' · 그 위로 **오르는** 판 ' + rows.filter(r => r.climb > 0).length);
console.log('');
console.log('번호     걸음  딛는걸음  오름');
//  ⚠️ 상위 20개만 찍었더니 1-35(사장님이 재밌다 하신 판)가 안 보여 자를 의심했다.
//     자는 맞았고 잘라 보여준 게 문제였다. ALL=1 이면 다 찍는다.
for (const r of (process.env.ALL === '1' ? hit : hit.slice(0, 20)))
  console.log(String(r.id).padEnd(8) + String(r.moves).padStart(4) + String(r.stand).padStart(9) + String(r.climb).padStart(6));
