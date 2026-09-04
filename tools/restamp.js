// 🔴 규칙이 바뀌면 **판 자료의 정답을 다시 박는다** (2026-09-05 밤).
//
// 09-05: "포탈에 걸쳐 있는 동안은 안 떨어진다" 를 넣었다. 몸이 더 자유로워졌으니
//        포탈 판의 최단해가 달라졌을 수 있다. 적어둔 sol/best 를 그대로 두면 거짓말이 된다.
//
//   node tools/restamp.js            보여만 준다
//   WRITE=1 node tools/restamp.js    판 자료에 쓴다
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { cutOf } = require('./star.js');
const LP = path.join(__dirname, '../game/Assets/Resources/levels.json');
const CAP = +(process.env.CAP8 || 1500000);
const solve = (g, o) => E.solve({ grid: g, gravity: true, clear: 'all', id: 'r' },
                                Object.assign({ cap: CAP }, o || {}));

const d = JSON.parse(fs.readFileSync(LP, 'utf8'));
let changed = 0, broke = 0, same = 0;
for (const l of d.levels) {
  if (l.tutorial || l.demo) continue;
  const a = solve(l.grid);
  if (!a.ok) { console.log('🔴 ' + l.id + ' 안 풀린다 — 빼야 한다'); broke++; continue; }
  const s = solve(l.grid, { needStar: true });
  const nb = a.moves, ns = s.ok ? s.moves : a.moves;
  //  🔴 **걸음 수가 같아도 그 길이 아직 통하는지 되짚어 봐야 한다** (09-05).
  //     1-4 는 best 가 22 로 그대로였는데 적어둔 수순이 막혀 있었다 —
  //     규칙이 바뀌면서 같은 길이의 **다른 길**만 남은 것이다.
  //     걸음 수만 보고 "그대로"라고 하면 거짓말을 그대로 둔다.
  let solOk = false;
  try {
    const L = E.parse({ grid: l.grid, gravity: true, clear: 'all', id: l.id });
    let st = E.startState(L);
    solOk = true;
    for (const ch of (l.sol || '')) {
      const dir = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 }[ch];
      if (dir === undefined) continue;
      const nx = E.step(L, st, dir);
      if (!nx) { solOk = false; break; }
      st = nx;
    }
    if (solOk) solOk = E.isWin(L, st);
  } catch (e) { solOk = false; }
  if (solOk && nb === l.best && ns === l.bestStar && a.shortest === 1) { same++; continue; }
  if (!solOk) console.log('  ' + l.id.padEnd(7) + '적어둔 수순이 막혔다 — 다시 박는다');
  console.log('  ' + l.id.padEnd(7) + '걸음 ' + l.best + '→' + nb +
              ' · 별먹고 ' + l.bestStar + '→' + ns +
              (a.shortest !== 1 ? ' · ⚠️ 최단해가 ' + a.shortest + '개' : ''));
  l.best = nb; l.sol = a.path; l.bestStar = ns; l.cut = cutOf(ns); l.states = a.states;
  changed++;
}
console.log('\n그대로 ' + same + ' · 고침 ' + changed + ' · 못 푸는 판 ' + broke);
if (process.env.WRITE !== '1') { console.log('(보여주기만 했다. 쓰려면 WRITE=1)'); process.exit(0); }
fs.writeFileSync(path.join(__dirname, 'levels.bak.json'), fs.readFileSync(LP));
fs.writeFileSync(LP, JSON.stringify(d, null, 2) + '\n');
console.log('판 자료에 썼다');
