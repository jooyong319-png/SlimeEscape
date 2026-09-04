// 🔴 **사장님이 직접 해보실 판**을 골라 게임에 넣는다 (2026-09-05 밤).
//
// 사장님: *"나오는 판 내가 다 해볼게 그럼. 그리고 걸러보자. 괜찮은 판 안괜찮은판."*
//
// ⚠️ 그래서 **덜 거른다.** 내 자는 오늘만 다섯 번 틀렸다 —
//    압박 → 길이 → 밀도 → 조각 수 → 홈 모양. 사장님 판단이 자료다.
//    딱딱한 조건(풀린다·최단 유일)만 걸고, 나머지는 **자로 줄만 세워** 드린다.
//
//   node tools/review.js             보여만 준다
//   WRITE=1 node tools/review.js     levels.json 에 넣는다 (지금 판은 사본 뜬다)
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { putStar, cutOf } = require('./star.js');
const { rng } = require('./grow.js');

const LP = path.join(__dirname, '../game/Assets/Resources/levels.json');
const POOL = process.env.POOL || 'tools/hard-pool.json';
const N = +(process.env.N || 30);
const CAP = +(process.env.CAP7 || 800000);
const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };
const solve = (g, o) => E.solve({ grid: g, gravity: true, clear: 'all', id: 'r' },
                                Object.assign({ cap: CAP }, o || {}));

/// 홈에 몸을 넣는 길을 **다 헤아려** 제일 많이 꺾는 값을 센다.
/// ⚠️ 처음엔 이웃 하나를 아무렇게나 골라 훑었더니 **6칸 4꺾음 홈을 1꺾음으로** 셌다.
///    홈은 2칸 폭이라 갈림길이 있어서, 아무 데나 따라가면 엉뚱한 길을 센다.
function doorTurns(L, d) {
  const cells = d.cells, core = d.core >= 0 ? d.core : cells[0];
  const set = new Set(cells);
  const nb = c => [c - 1, c + 1, c - L.w, c + L.w].filter(n => set.has(n) &&
                  (Math.abs(n - c) === L.w || ((n / L.w) | 0) === ((c / L.w) | 0)));
  let best = 0;
  const seen = new Set([core]);
  (function walk(at, n, lastDir, turns) {
    if (n === cells.length) { if (turns > best) best = turns; return; }
    for (const m of nb(at)) {
      if (seen.has(m)) continue;
      seen.add(m);
      walk(m, n + 1, m - at, turns + (lastDir !== null && (m - at) !== lastDir ? 1 : 0));
      seen.delete(m);
    }
  })(core, 1, null, 0);
  return best;
}

/// 정답을 따라가며 재는 것 — 되나옴 · 굳은 몸 딛고 오름 · 홈 구멍
function trace(L, sol) {
  let st = E.startState(L);
  let re = 0, climb = 0, thru = 0;
  const inside = new Array(L.doors.length).fill(false);
  for (const ch of sol) {
    const d = SYM[ch]; if (d === undefined) continue;
    const ns = E.step(L, st, d); if (!ns) break;
    for (let i = 0; i < L.doors.length; i++) {
      if ((ns.dm || 0) & (1 << i)) { inside[i] = false; continue; }
      const on = L.doors[i].set.has(ns.body[0]);
      if (on) thru++;
      if (inside[i] && !on) re++;
      inside[i] = on;
    }
    //  굳은 몸을 딛고 **위로** 갔나
    if (d === 0 && (ns.dm || 0)) {
      let bySpent = false, byWall = false;
      for (const c of ns.body) {
        const below = c + L.w;
        if (below >= L.w * L.h) { byWall = true; break; }
        if (L.g[(below / L.w) | 0][below % L.w] === '#') { byWall = true; break; }
        for (let i = 0; i < L.doors.length; i++)
          if (((ns.dm || 0) & (1 << i)) && L.doors[i].set.has(below)) bySpent = true;
      }
      if (bySpent && !byWall) climb++;
    }
    st = ns;
  }
  return { re, climb, thru };
}

const pool = JSON.parse(fs.readFileSync(POOL, 'utf8'));
console.log('못 ' + pool.length + '판');
const rows = [];
for (let i = 0; i < pool.length; i++) {
  const p = pool[i];
  const a = solve(p.grid);
  if (!a.ok || a.shortest !== 1) continue;          // 딱딱한 조건만
  let L; try { L = E.parse({ grid: p.grid, gravity: true, clear: 'all', id: 'r' }); } catch (e) { continue; }
  let open = 0;
  for (let y = 0; y < L.h; y++) for (let x = 0; x < L.w; x++) if (L.g[y][x] !== '#') open++;
  const t = trace(L, a.path);
  rows.push({
    i, grid: p.grid, moves: a.moves, states: a.states, gim: p.gim || 'none',
    doors: L.doors.length, big: Math.max(...L.doors.map(d => d.cells.length)),
    turn: Math.max(...L.doors.map(d => doorTurns(L, d))),
    dens: a.moves / open, re: t.re, climb: t.climb, thru: t.thru,
    //  🔴 줄 세우는 값 — 사장님이 짚어주신 것만 넣는다.
    //     되나옴(홈 통과) · 굳은 몸 딛고 오름 · 홈 꺾음 · 포탈. **걸음 수는 안 본다.**
    score: t.re * 5 + t.climb * 4 + t.turn * 2 + (p.gim === 'portal' ? 6 : 0),
  });
}
console.log('풀리고 최단 유일: ' + rows.length + '판');
console.log('  되나옴 1↑ ' + rows.filter(r => r.re > 0).length +
            ' · 굳은몸 딛고오름 1↑ ' + rows.filter(r => r.climb > 0).length +
            ' · 홈꺾음 4↑ ' + rows.filter(r => r.turn >= 4).length +
            ' · 포탈 ' + rows.filter(r => r.gim === 'portal').length);

rows.sort((a, b) => b.score - a.score);
//  🔴 **포탈을 따로 챙긴다.** 점수순으로만 뽑았더니 30판 전부 포탈이 없었다 —
//     사장님이 재미의 원인으로 짚으신 게 포탈인데 하나도 안 보여드릴 뻔했다.
//     그리고 **홈 꺾음 4** 짜리도 따로 챙긴다. 둘 다 이 묶음의 핵심이다.
const pick = [];
const take = (list, n) => { for (const r of list) { if (pick.length >= N) break;
  if (n-- <= 0) break; if (!pick.includes(r)) pick.push(r); } };
take(rows.filter(r => r.gim === 'portal'), Math.round(N * 0.35));   // 포탈 셋에 하나
take(rows.filter(r => r.turn >= 4), Math.round(N * 0.3));           // 꺾음 4 짜리
take(rows, N);                                                       // 나머지는 점수순
//  섞어서 보여드린다 — 앞에만 포탈이 몰리면 뒤로 갈수록 지루하다
for (let i = pick.length - 1; i > 0; i--) {
  const j = (i * 2654435761 + 7) % (i + 1);
  const tmp = pick[i]; pick[i] = pick[j]; pick[j] = tmp;
}
console.log('\n고른 ' + pick.length + '판');
console.log('번호  크기     걸음  홈  제일큰홈 꺾음 되나옴 딛고오름 통과 기믹    빽빽함');
pick.forEach((r, k) => console.log(String(k + 1).padStart(4) + '  ' +
  (r.grid[0].length + 'x' + r.grid.length).padEnd(8) + String(r.moves).padStart(4) +
  String(r.doors).padStart(4) + String(r.big).padStart(8) + String(r.turn).padStart(5) +
  String(r.re).padStart(6) + String(r.climb).padStart(8) + String(r.thru).padStart(5) +
  '  ' + r.gim.padEnd(8) + r.dens.toFixed(2)));

if (process.env.WRITE !== '1') { console.log('\n(보여주기만 했다. 쓰려면 WRITE=1)'); process.exit(0); }

console.log('\n별을 박는다…');
const d = JSON.parse(fs.readFileSync(LP, 'utf8'));
const tut = d.levels.find(l => l.tutorial);
const out = [];
for (let k = 0; k < pick.length; k++) {
  const p = pick[k];
  const st = putStar(p.grid, p.moves, rng(k * 7919 + 3), 24, 10,
                     Math.min(CAP, p.states * 3 + 60000));
  const grid = st ? st.grid : p.grid;
  const a = solve(grid);
  const s = solve(grid, { needStar: true });
  if (!a.ok) continue;
  out.push({
    id: '1-' + (out.length + 1), grid, clear: 'all',
    best: a.moves, sol: a.path,
    bestStar: s.ok ? s.moves : a.moves, cut: cutOf(s.ok ? s.moves : a.moves),
    lost: 0, tight: 0, backtrack: 0, states: a.states,
    press: -1, traps: 0, gim: p.gim,
    review: true, poolIdx: p.i,
  });
}
fs.writeFileSync(path.join(__dirname, 'levels.bak.json'), fs.readFileSync(LP));
d.levels = [tut, ...out];
fs.writeFileSync(LP, JSON.stringify(d, null, 2) + '\n');
console.log('판 자료에 넣었다 · ' + out.length + '판 (+튜토리얼)');
console.log('⚠️ 지금 판은 tools/levels.bak.json 과 git 에 있다');
