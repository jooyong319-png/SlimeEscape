// 🔴 **「홈을 통과해야 닿는가」**를 잰다 (2026-09-05 밤).
//
// 사장님이 짚어주신 어려움의 정체:
//   *"홈은 「나중에 몸으로 채울 곳」이라 들어가면 안 되는 데로 읽힌다.
//     그런데 여기선 거기가 유일한 길이다."*
//
// 옛 1-16 의 첫째 홈은 가운데가 뚫린 ㄷ자였고, 그 구멍으로 내려가야 바닥 조각에 닿았다.
// 정답 21~30 걸음이 `← ← ← ↓ ↑ ↑ → ↑ ↑ →` — 홈으로 들어갔다 도로 나온다.
//
// 재는 것 셋:
//   ① 통과걸음 — 정답이 **아직 안 채운 홈 칸** 위에 있는 걸음 수
//   ② 구멍     — 홈에 둘러싸인 빈 칸 (ㄷ·ㅁ·S자 홈이면 생긴다)
//   ③ 되나옴   — 홈에 들어갔다 **도로 나오는** 횟수 (한 번 들어가 채우는 것과 다르다)
//
//   node tools/thru.js                        판 자료
//   node tools/thru.js tools/trap-pool.json   못
//   node tools/thru.js old                    옛 16판 묶음
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };
const CAP = +(process.env.CAP5 || 2500000);

function judge(l) {
  const def = { grid: l.grid, gravity: true, clear: l.clear || 'all', id: l.id || 'x' };
  let L; try { L = E.parse(def); } catch (e) { return null; }
  const a = E.solve(def, { cap: CAP });
  if (!a.ok) return null;

  //  ② 구멍 — 홈 칸에 둘러싸인 빈 칸. 위아래 또는 좌우가 같은 홈이면 구멍으로 친다.
  let holes = 0;
  for (let i = 0; i < L.doors.length; i++) {
    const S = L.doors[i].set;
    for (let y = 1; y < L.h - 1; y++) for (let x = 1; x < L.w - 1; x++) {
      const c = y * L.w + x;
      if (S.has(c) || L.g[y][x] === '#') continue;
      const up = S.has(c - L.w), dn = S.has(c + L.w);
      const lf = S.has(c - 1), rt = S.has(c + 1);
      if ((up && dn) || (lf && rt)) holes++;
    }
  }

  //  ① 통과걸음 · ③ 되나옴 — 정답을 따라가며 본다
  let st = E.startState(L);
  let thru = 0, reenter = 0;
  const inside = new Array(L.doors.length).fill(false);
  for (const ch of a.path) {
    const d = SYM[ch]; if (d === undefined) continue;
    const ns = E.step(L, st, d); if (!ns) break;
    for (let i = 0; i < L.doors.length; i++) {
      if ((ns.dm || 0) & (1 << i)) { inside[i] = false; continue; }   // 이미 채운 홈은 안 센다
      const on = L.doors[i].set.has(ns.body[0]);
      if (on) thru++;
      //  들어갔다 나왔다 = 되나옴
      if (inside[i] && !on) reenter++;
      inside[i] = on;
    }
    st = ns;
  }
  return { moves: a.moves, thru, holes, reenter, score: thru + holes * 3 + reenter * 4 };
}

const arg = process.argv[2];
let list, title;
if (arg === 'old') {
  const o = JSON.parse(fs.readFileSync(path.join(process.env.TEMP || '/tmp', 'old16.json'), 'utf8'));
  list = o.levels.filter(l => !l.tutorial); title = '옛 16판 묶음';
} else if (arg) {
  const raw = JSON.parse(fs.readFileSync(arg, 'utf8'));
  list = (raw.levels || raw).filter(l => l.grid && !l.tutorial && !l.demo); title = arg;
} else {
  const d = JSON.parse(fs.readFileSync(path.join(__dirname, '../game/Assets/Resources/levels.json'), 'utf8'));
  list = d.levels.filter(l => !l.tutorial && !l.demo); title = '지금 판 자료';
}

console.log(title + ' — ' + list.length + '판');
console.log('이름      걸음  통과걸음  구멍  되나옴  점수');
const rows = [];
list.forEach((l, i) => {
  const r = judge(l);
  if (!r) return;
  rows.push(Object.assign({ id: l.id || '#' + i }, r));
});
rows.sort((a, b) => b.score - a.score);
for (const r of rows.slice(0, +(process.env.TOP || 16)))
  console.log(String(r.id).padEnd(9) + String(r.moves).padStart(4) + String(r.thru).padStart(9) +
              String(r.holes).padStart(6) + String(r.reenter).padStart(7) + String(r.score).padStart(6));
const avg = f => (rows.reduce((s, r) => s + f(r), 0) / Math.max(1, rows.length)).toFixed(1);
console.log('-'.repeat(46));
console.log('잰 판 ' + rows.length + ' · 평균 통과걸음 ' + avg(r => r.thru) +
            ' · 구멍 ' + avg(r => r.holes) + ' · 되나옴 ' + avg(r => r.reenter));
console.log('구멍 있는 판 ' + rows.filter(r => r.holes > 0).length +
            ' · 되나오는 판 ' + rows.filter(r => r.reenter > 0).length);
