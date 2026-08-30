// 판이 얼마나 어려운지 잰다.
//
// 🔴 "못 이기는 상태 비율"은 틀린 자였다. 되돌리기가 있으니 망해도 그만이다.
//    사장님이 그 값 95%짜리 판을 "쉽다"고 하셨다 (08-30).
//
// 대신 재는 것:
//   · 최단해 길이 — 길수록 앞을 많이 봐야 한다
//   · 상태 수 — 뒤져야 할 공간
//   · 🔴 외길 비율 — **정답 위에서 삐끗하면 지는 지점의 비율.**
//        높으면 줄타기다. 낮으면 아무렇게나 눌러도 어떻게든 된다.
//   · 되짚기 — 정답이 목표에서 **멀어지는** 걸음의 비율
'use strict';
const E = require('./engine.js').SlimeEngine;

/// 이길 수 있는 상태들을 모아둔다
function winnable(L, cap = 400000) {
  const s0 = E.startState(L);
  const idx = new Map([[E.keyOf(s0), 0]]);
  const states = [s0], back = [[]], win = [];
  for (let i = 0; i < states.length; i++) {
    if (states.length > cap) return null;
    const st = states[i];
    if (E.isWin(L, st)) { win.push(i); continue; }
    const acts = L.pads.length ? 5 : 4;
    for (let d = 0; d < acts; d++) {
      const ns = E.step(L, st, d);
      if (!ns) continue;
      const k = E.keyOf(ns);
      let j = idx.get(k);
      if (j === undefined) { j = states.length; idx.set(k, j); states.push(ns); back.push([]); }
      back[j].push(i);
    }
  }
  if (!win.length) return null;
  const safe = new Set(win);
  const q = [...win];
  for (let h = 0; h < q.length; h++)
    for (const p of back[q[h]]) if (!safe.has(p)) { safe.add(p); q.push(p); }
  return { idx, states, safe, total: states.length };
}

/// 판 하나의 어려움
function measure(def) {
  const L = E.parse(def);
  const sol = E.solve(def);
  if (!sol.ok) return null;
  const W = winnable(L);
  if (!W) return null;

  const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };
  const acts = L.pads.length ? 5 : 4;

  let st = E.startState(L);
  let forks = 0, tight = 0, away = 0;
  const goal = L.doors.length ? L.doors[0].cells[0] : L.start;
  const dist = c => Math.abs(L.w ? (c % L.w) - (goal % L.w) : 0)
                  + Math.abs(((c / L.w) | 0) - ((goal / L.w) | 0));

  for (const ch of sol.path) {
    const right = SYM[ch];
    let alts = 0, bad = 0;
    for (let d = 0; d < acts; d++) {
      if (d === right) continue;
      const ns = E.step(L, st, d);
      if (!ns) continue;
      const j = W.idx.get(E.keyOf(ns));
      alts++;
      if (j === undefined || !W.safe.has(j)) bad++;
    }
    if (alts > 0) { forks++; if (bad === alts) tight++; }
    const before = dist(st.body[0]);
    const next = E.step(L, st, right);
    if (!next) break;
    if (dist(next.body[0]) > before) away++;
    st = next;
  }

  return {
    moves: sol.moves,
    states: W.total,
    lost: 1 - W.safe.size / W.total,
    tight: forks ? tight / forks : 0,        // 외길 비율
    back: sol.moves ? away / sol.moves : 0,  // 되짚기 비율
  };
}

const fs = require('fs');
const LP = require('path').join(__dirname, '../game/Assets/Resources/levels.json');
const d = JSON.parse(fs.readFileSync(LP, 'utf8'));
const STAMP = process.argv.includes('--stamp');   // 잰 값을 판 자료에 박는다
const only = process.argv.slice(2).find(a => !a.startsWith('--'));   // 깃발은 판 이름이 아니다
console.log('판     걸음   상태     못이김  외길   되짚기');
console.log('─'.repeat(52));
const rows = [];
for (const l of d.levels) {
  if (only && l.id !== only) continue;
  const m = measure({ grid: l.grid, gravity: true, clear: l.clear || 'all', id: l.id });
  if (!m) { console.log(l.id.padEnd(6) + '  못 잼'); continue; }
  rows.push([l.id, m]);
  if (STAMP) {
    // 🔴 검사가 읽는 값을 여기서 박는다. 안 박으면 0으로 읽혀 "쉽다"고 초록불이 뜬다.
    l.lost = +(m.lost * 100).toFixed(1);
    l.tight = +(m.tight * 100).toFixed(1);
    l.backtrack = +(m.back * 100).toFixed(1);
    l.states = m.states;
    delete l.wander;
  }
  console.log(l.id.padEnd(6) +
    String(m.moves).padStart(5) +
    String(m.states).padStart(8) +
    (' ' + (m.lost * 100).toFixed(0) + '%').padStart(8) +
    (' ' + (m.tight * 100).toFixed(0) + '%').padStart(7) +
    (' ' + (m.back * 100).toFixed(0) + '%').padStart(8));
}
if (rows.length > 1) {
  const avg = k => rows.reduce((s, [, m]) => s + m[k], 0) / rows.length;
  console.log('─'.repeat(52));
  console.log('평균  ' + avg('moves').toFixed(0).padStart(5) +
              avg('states').toFixed(0).padStart(8) +
              (' ' + (avg('lost') * 100).toFixed(0) + '%').padStart(8) +
              (' ' + (avg('tight') * 100).toFixed(0) + '%').padStart(7) +
              (' ' + (avg('back') * 100).toFixed(0) + '%').padStart(8));
}

if (STAMP) {
  fs.writeFileSync(LP, JSON.stringify(d, null, 2) + '\n');
  console.log('\n판 자료에 박음 (lost · tight · backtrack · states)');
}
