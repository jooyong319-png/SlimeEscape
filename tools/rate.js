// 🔴 **못에 쌓인 판을 재서 줄 세운다** (2026-09-20).
//
// 사장님이 150판을 직접 찍어 보시고 표를 주셨다. 거기서 나온 것:
//
//   표            평균 걸음   평균 상태수
//   1 힌트 필요     32.4      752,922
//   2 1분 컷        32.3      142,608
//   3 버림          27.8        1,002
//
// 🔴 **걸음 수는 난이도와 상관이 없다** (32.4 대 32.3). 갈리는 건 **상태수** —
//    그 판에서 뒤져야 하는 경우의 수다. 3 을 받은 판은 평균 1,002 가지뿐이라
//    "보자마자 답이 보이는" 게 당연했다. 걸음 수로 고른 게 헛수고였다.
//
// 🔴 사장님의 3 에는 또 하나가 있다 — *"굴의 테마가 적절히 사용되지 않은 것"*.
//    그래서 **정답을 되짚어 기믹이 정말 발동하는지** 같이 센다 (finale.js 의 fired 와 같은 셈법).
//    crack·box 굴이 대량으로 버려진 게 이쪽으로 보인다: 격자에 있기만 하고 안 쓰인다.
//
//   node tools/rate.js                 못 전체를 잰다 (오래 걸린다 — 뒤에서 돌린다)
//   CAP=2000000 node tools/rate.js     상태 상한
//
// ⚠️ **한 판 잴 때마다 저장한다.** 몇 시간짜리를 통째로 날린 적이 있다 (09-05).
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const OUT = process.env.OUT || path.join(__dirname, 'rated.json');
const CAP = +(process.env.CAP || 2000000);
const D = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };

/// 정답을 되짚어 기믹이 **정말 발동했는지** 센다 (finale.js 와 같다)
function fired(p) {
  try {
    const L = E.parse({ grid: p.grid, gravity: true, clear: p.clear || 'all', id: 'r' });
    let st = E.startState(L);
    let slid = 0, warp = 0, push = 0, stand = 0;
    for (const ch of p.sol) {
      const n = E.step(L, st, D[ch]);
      if (!n) return null;
      if ((n._n || 1) >= 2) slid++;
      const hx = st.body[0] % L.w, hy = (st.body[0] / L.w) | 0;
      const nx = n.body[0] % L.w, ny = (n.body[0] / L.w) | 0;
      if (Math.abs(hx - nx) + Math.abs(hy - ny) > (n._n || 1) + 1) warp++;
      if ((st.bx || []).join(',') !== (n.bx || []).join(',')) push++;
      for (const c of n.body) if ((n.bx || []).includes(c + L.w)) { stand++; break; }
      st = n;
    }
    let brk = 0;
    for (let k = 0; k < L.crack.length; k++) if ((st.cm || 0) & (1 << k)) brk++;
    return { slid, brk, warp, box: push + stand };
  } catch (e) { return null; }
}

/// 격자 글자로 기믹을 가린다 — gim 칸은 못 믿는다 (250판이 비어 있었다)
function kindOf(grid) {
  const t = grid.join('');
  const on = [];
  if (t.includes('I')) on.push('ice');
  if (t.includes('X')) on.push('crack');
  if (t.includes('B')) on.push('box');
  if (/[PQpq]/.test(t)) on.push('portal');
  return on.length === 0 ? 'none' : on.length === 1 ? on[0] : 'mix';
}

//  ---- 재야 할 판 모으기 ----
const usedPath = path.join(__dirname, '../game/Assets/Resources/levels.json');
const used = new Set(JSON.parse(fs.readFileSync(usedPath, 'utf8'))
  .levels.map(l => l.grid.join('|').replace(/o/g, '.')));
const pool = new Map();
for (const f of fs.readdirSync(__dirname).filter(x => x.endsWith('.json'))) {
  if (f === path.basename(OUT)) continue;
  let a; try { a = JSON.parse(fs.readFileSync(path.join(__dirname, f), 'utf8')); } catch (e) { continue; }
  for (const p of (Array.isArray(a) ? a : (a.levels || a.pool || []))) {
    if (!p || !p.grid || !p.best || !p.sol) continue;
    const k = p.grid.join('|').replace(/o/g, '.');
    if (used.has(k) || pool.has(k)) continue;
    pool.set(k, p);
  }
}

//  이미 잰 것은 건너뛴다 — 끊겼다 다시 돌려도 이어진다
const out = fs.existsSync(OUT) ? JSON.parse(fs.readFileSync(OUT, 'utf8')) : [];
const done = new Set(out.map(p => p.grid.join('|').replace(/o/g, '.')));
const todo = [...pool.entries()].filter(([k]) => !done.has(k));
console.log('재야 할 판 ' + todo.length + '개 (이미 잰 것 ' + out.length + '개) · 상한 ' + CAP.toLocaleString());

const t0 = Date.now();
let n = 0, capped = 0;
for (const [, p] of todo) {
  n++;
  let a;
  try { a = E.solve({ grid: p.grid, gravity: true, clear: p.clear || 'all', id: 'r' }, { cap: CAP }); }
  catch (e) { continue; }
  if (!a || !a.ok) continue;
  if (a.capped) capped++;
  const f = fired({ ...p, sol: a.path || p.sol });
  out.push({
    grid: p.grid, clear: p.clear || 'all',
    best: a.moves, sol: a.path || p.sol,
    states: a.states || 0, capped: !!a.capped, shortest: a.shortest,
    kind: kindOf(p.grid),
    slid: f ? f.slid : -1, brk: f ? f.brk : -1, warp: f ? f.warp : -1, box: f ? f.box : -1,
  });
  fs.writeFileSync(OUT, JSON.stringify(out, null, 1) + '\n');   // 한 판마다 저장
  if (n % 25 === 0)
    console.log('  ' + n + '/' + todo.length + ' · ' + ((Date.now() - t0) / 60000).toFixed(1) + '분 · 상한걸림 ' + capped);
}
console.log('\n끝. 잰 판 ' + out.length + '개 → ' + OUT + ' · ' + ((Date.now() - t0) / 60000).toFixed(1) + '분');
const big = out.filter(p => p.states >= 200000).length;
console.log('상태 20만 이상: ' + big + '개 (사장님이 1 을 준 판 수준)');
