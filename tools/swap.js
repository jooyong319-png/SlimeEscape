// 🔴 **판 몇 개만 갈아끼운다** (2026-09-05).
//
// 사장님이 열두 판을 다 해보시고 *"1-5는 좀 쉬웠음 · 11은 쉬운 정도"* 라 하셨다.
// 묶음을 통째로 다시 만들면 이미 좋다고 하신 열 판까지 날아간다 — **지목한 것만 바꾼다.**
//
// 고르는 조건은 그 열두 판의 평가에서 나왔다:
//   **빽빽함(걸음/빈칸) 0.5 이상  또는  되나옴 1 이상**
//   (쉽다 하신 둘은 넓은 판에 되나옴 0 이었다. 12판 중 11판이 이 자와 맞았다)
//
//   node tools/swap.js 1-5 1-11            보여만 준다
//   WRITE=1 node tools/swap.js 1-5 1-11    갈아끼운다
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { putStar, cutOf } = require('./star.js');
const { rng } = require('./grow.js');

const LP = path.join(__dirname, '../game/Assets/Resources/levels.json');
const POOL = process.env.POOL || 'tools/hard-pool2.json';
const CAP = +(process.env.CAP7 || 700000);
const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };
const solve = (g, o) => E.solve({ grid: g, gravity: true, clear: 'all', id: 's' },
                                Object.assign({ cap: CAP }, o || {}));

//  🔴 ADD=n — 바꾸는 게 아니라 **뒤에 n판 이어 붙인다.**
const ADD = +(process.env.ADD || 0);
const targets = process.argv.slice(2);
if (!ADD && !targets.length) {
  console.log('쓰기: node tools/swap.js 1-5 1-11   또는   ADD=12 node tools/swap.js');
  process.exit(1);
}

/// 이 판이 사람에게 무엇을 시키나 (사장님 평가로 세운 자)
function weigh(grid, sol, best) {
  const L = E.parse({ grid, gravity: true, clear: 'all', id: 'w' });
  let st = E.startState(L), re = 0, slid = 0, broke = 0, thru = 0;
  const inside = new Array(L.doors.length).fill(false);
  for (const ch of sol || '') {
    const d = SYM[ch]; if (d === undefined) continue;
    const n = E.step(L, st, d); if (!n) return null;
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
  for (let k = 0; k < L.crack.length; k++) if ((st.cm || 0) & (1 << k)) broke++;
  let open = 0;
  for (let y = 0; y < L.h; y++) for (let x = 0; x < L.w; x++) if (L.g[y][x] !== '#') open++;
  return { dens: best / open, re, slid, broke, thru, open };
}

const d = JSON.parse(fs.readFileSync(LP, 'utf8'));
const pool = JSON.parse(fs.readFileSync(POOL, 'utf8'));
//  🔴 **이미 쓰고 있는 판은 절대 다시 안 고른다** (09-05 에 1-48 이 1-47 과 같았다)
//     ⚠️ **별을 떼고 견뎌야 한다** (09-08). 판 자료엔 별이 박혀 있고 못엔 없어서,
//        그대로 견주면 늘 "안 쓴 판"이 되어 막는 게 아무 일도 안 한다.
const bare = g => g.join('').replace(/o/g, '.');
const used = new Set(d.levels.map(l => bare(l.grid)));

const cand = [];
for (let i = 0; i < pool.length; i++) {
  const p = pool[i];
  if (used.has(bare(p.grid))) continue;
  const w = weigh(p.grid, p.sol, p.best);
  if (!w) continue;
  if (!(w.dens >= 0.5 || w.re >= 1)) continue;          // 어려움 조건
  if (!(w.slid >= 2 || w.broke >= 2)) continue;         // 기믹이 정말 쓰이는가
  cand.push({ p, w, score: w.dens * 10 + w.re * 4 + (w.slid + w.broke) * 1.5 + w.thru * 0.3 });
}
cand.sort((a, b) => b.score - a.score);
console.log('쓸 수 있는 판 ' + cand.length + '개 (이미 쓰는 판 제외)');

if (cand.length < targets.length) { console.log('모자란다'); process.exit(1); }

//  🔴 **바꾸는 판과 같은 기믹으로 고른다.** 점수순으로만 뽑으면 얼음 판만 올라와
//     묶음에서 금 간 바닥이 사라진다 — 결이 한쪽으로 쏠린다.
const taken = new Set();
function takeBy(want) {
  for (let i = 0; i < cand.length; i++) {
    if (taken.has(i)) continue;
    if (want && cand[i].p.gim !== want) continue;
    taken.add(i); return cand[i];
  }
  return null;
}
function pickFor(id) {
  const old = d.levels.find(l => l.id === id);
  return (old && old.gim && takeBy(old.gim)) || takeBy(null);
}

//  🔴 덧붙일 때는 **얼음과 금 간 바닥을 번갈아** 뽑는다.
//     점수순으로만 두면 한 기믹만 줄줄이 나와 열두 판을 내리 같은 걸 하시게 된다.
function pickAlt(k) {
  const want = k % 2 === 0 ? 'ice' : 'crack';
  return takeBy(want) || takeBy(null);
}

//  덧붙이기면 이어질 번호를 만든다
if (ADD) {
  let n = 0;
  for (const l of d.levels) { const m = /^1-(\d+)$/.exec(l.id || ''); if (m) n = Math.max(n, +m[1]); }
  for (let k = 0; k < ADD; k++) targets.push('1-' + (n + 1 + k));
}

console.log('\n' + (ADD ? '새 번호' : '바꿀 판') + '   →  새 판       걸음 빈칸 밀도 되나옴 통과 미끄럼 무너짐');
const picks = [];
for (let k = 0; k < targets.length; k++) {
  const c = ADD ? pickAlt(k) : pickFor(targets[k]); picks.push(c);
  if (!c) { console.log('  ' + targets[k] + ' — 쓸 판이 모자란다'); break; }
  const old = d.levels.find(l => l.id === targets[k]);
  console.log('  ' + targets[k].padEnd(7) + '→  ' +
    (c.p.grid[0].length + 'x' + c.p.grid.length).padEnd(9) +
    String(c.p.best).padStart(4) + String(c.w.open).padStart(5) +
    c.w.dens.toFixed(2).padStart(6) + String(c.w.re).padStart(6) +
    String(c.w.thru).padStart(5) + String(c.w.slid).padStart(6) +
    String(c.w.broke).padStart(7) + '   (' + c.p.gim + ')' +
    (old ? '   [옛 걸음 ' + old.best + ']' : '  ⚠️ 그런 판이 없다'));
}

if (process.env.WRITE !== '1') { console.log('\n(보여주기만 했다. 쓰려면 WRITE=1)'); process.exit(0); }

console.log('\n별을 박는다…');
fs.writeFileSync(path.join(__dirname, 'levels.bak.json'), fs.readFileSync(LP));
for (let k = 0; k < targets.length; k++) {
  const c = picks[k];
  if (!c) break;
  let idx = d.levels.findIndex(l => l.id === targets[k]);
  if (idx < 0 && !ADD) { console.log('⚠️ ' + targets[k] + ' 없음 — 건너뛴다'); continue; }
  const st = putStar(c.p.grid, c.p.best, rng(k * 7919 + 17), 24, 10,
                     Math.min(CAP, (c.p.states || 200000) * 3 + 60000));
  const grid = st ? st.grid : c.p.grid;
  const a = solve(grid);
  const s = solve(grid, { needStar: true });
  if (!a.ok) { console.log('⚠️ ' + targets[k] + ' — 별 박고 나니 안 풀린다. 건너뛴다'); continue; }
  //  🔴 **번호는 그대로 둔다.** 자리만 갈아끼우는 것이라 뒤 판들이 밀리면 안 된다.
  if (idx < 0) { d.levels.push(null); idx = d.levels.length - 1; }
  d.levels[idx] = {
    id: targets[k], grid, clear: 'all',
    best: a.moves, sol: a.path,
    bestStar: s.ok ? s.moves : a.moves, cut: cutOf(s.ok ? s.moves : a.moves),
    lost: 0, tight: 0, backtrack: 0, states: a.states,
    press: -1, traps: 0, gim: c.p.gim, review: true,
  };
  console.log('  ' + targets[k] + (ADD ? ' 이어 붙였다' : ' 갈아끼웠다') +
              ' · 걸음 ' + a.moves + ' · 커트 ' + cutOf(s.ok ? s.moves : a.moves));
}
fs.writeFileSync(LP, JSON.stringify(d, null, 2) + '\n');
console.log('⚠️ 옛 판은 tools/levels.bak.json 에 있다');
