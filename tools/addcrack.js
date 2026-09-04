// 🔴 **금 간 바닥을 얹는다** (rev.12, 2026-09-05).
//
// 왜 나중에 얹나: 부서진 자리가 **상태**라, 지으면서 넣으면 걸음마다 상태를 이어 나르는
// 곳(relevel)이 그걸 잃어버린다. 다 지은 판에 얹고 **다시 풀어서** 확인하는 게 맞다.
//
// 🔴 벽('#')을 금 간 바닥('X')으로 바꾸는 건 **한 쪽으로만 어려워진다**:
//    처음엔 똑같이 막혀 있고(= 벽), 지나가면 없어질 뿐이다. 쉬워질 수가 없다.
//    그러니 "다시 풀린다"만 확인하면 된다.
//
//   node tools/addcrack.js tools/ice-pool.json
'use strict';
const fs = require('fs');
const E = require('./engine.js').SlimeEngine;

const IN  = process.argv[2] || 'tools/ice-pool.json';
const OUT = process.env.OUT || IN.replace(/\.json$/, '-crack.json');
const MAXX = +(process.env.MAXX || 6);     // 한 판에 얹을 금 간 바닥 수 (상태가 2^n 로 는다)
const CAP  = +(process.env.SOLVE_CAP || 700000);

function rng(seed) {
  let s = (seed | 0) || 1;
  return () => { s ^= s << 13; s |= 0; s ^= s >>> 17; s ^= s << 5; s |= 0;
                 return ((s >>> 0) % 100000) / 100000; };
}

/// 정답을 되짚어 **몸이 딛고 지나간 벽**을 모은다. 거기가 무너져야 뜻이 있다.
function steppedOn(grid, path) {
  const L = E.parse({ grid, gravity: true, clear: 'all', id: 'x' });
  let st = E.startState(L);
  const used = new Set();
  const mark = () => { for (const c of st.body) {
    const b = c + L.w;
    if (b >= L.w * L.h) continue;
    const by = (b / L.w) | 0, bx = b % L.w;
    //  🔴 가장자리 벽은 안 건드린다 — 뚫리면 판 밖으로 떨어지는 길이 생긴다
    if (bx === 0 || by === 0 || bx === L.w - 1 || by === L.h - 1) continue;
    if (L.g[by][bx] === '#') used.add(b);
  } };
  mark();
  const D = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };
  for (const ch of path) {
    const n = E.step(L, st, D[ch]);
    if (!n) return null;                       // 정답이 안 통한다 = 이 판은 못 쓴다
    st = n; mark();
  }
  return E.isWin(L, st, false) ? used : null;
}

const put = (grid, cell, W, ch) => {
  const y = (cell / W) | 0, x = cell % W;
  const r = grid[y].split(''); r[x] = ch;
  const g = grid.slice(); g[y] = r.join(''); return g;
};

const pool = JSON.parse(fs.readFileSync(IN, 'utf8'));
const out = [];
let t0 = Date.now(), hardened = 0;

for (let i = 0; i < pool.length; i++) {
  const p = pool[i];
  const W = p.grid[0].length;
  const base = E.solve({ grid: p.grid, gravity: true, clear: 'all', id: 'b', cap: CAP });
  if (!base.ok) { out.push(p); continue; }

  const r = rng(i * 9176 + 3);
  let grid = p.grid, best = base, n = 0;

  //  🔴 한 칸 얹을 때마다 **정답이 바뀐다.** 그러니 후보도 새 정답에서 다시 뽑는다.
  //     한 번만 뽑으면 첫 정답이 딛던 자리밖에 못 본다.
  for (let round = 0; round < MAXX; round++) {
    const used = steppedOn(grid, best.path);
    if (!used || !used.size) break;
    const cand = [...used].sort(() => r() - 0.5);
    let took = false;
    for (const cell of cand) {
      const trial = put(grid, cell, W, 'X');
      //  🔴 **원래 길이 더는 안 통해야** 얹은 뜻이 있다 (09-05).
      //     그냥 "다시 풀린다"만 보면 무너지기만 하고 정답은 그대로인 장식이 된다 —
      //     처음 재봤을 때 28판 중 26판이 그랬다. 걸음 수가 아니라 **길이 막혔는가**로 본다.
      if (steppedOn(trial, best.path)) continue;
      const a = E.solve({ grid: trial, gravity: true, clear: 'all', id: 'c', cap: CAP });
      //  다시 풀려야 하고, **최단해가 하나**여야 한다 (여러 갈래면 "찾았다"가 흐려진다)
      if (!a.ok || a.shortest !== 1) continue;
      grid = trial; best = a; n++; took = true; break;
    }
    if (!took) break;
  }

  if (!n) { out.push(p); continue; }
  //  🔴 **정말 무너지는가.** 안 무너지면 그냥 벽을 다르게 칠한 것뿐이다.
  const L2 = E.parse({ grid, gravity: true, clear: 'all', id: 'v' });
  let st = E.startState(L2);
  const D = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };
  let ok = true;
  for (const ch of best.path) { const nx = E.step(L2, st, D[ch]); if (!nx) { ok = false; break; } st = nx; }
  if (!ok || !(st.cm || 0)) { out.push(p); continue; }

  const broke = [...Array(L2.crack.length).keys()].filter(k => (st.cm >> k) & 1).length;
  out.push({ ...p, grid, best: best.moves, sol: best.path,
             crack: n, crackBroke: broke, wasBest: p.best,
             gim: (p.gim === 'none' ? '' : p.gim + '+') + 'crack' });
  hardened++;
  if ((i + 1) % 10 === 0)
    console.log(`  ${i + 1}/${pool.length} · 얹은 판 ${hardened} · ${((Date.now() - t0) / 1000).toFixed(0)}초`);
}

fs.writeFileSync(OUT, JSON.stringify(out, null, 1) + '\n');
const g = out.filter(x => x.crack);
console.log(`\n판 ${out.length}개 중 ${hardened}개에 금 간 바닥을 얹었다 → ${OUT}`);
if (g.length) {
  const up = g.filter(x => x.best > x.wasBest).length;
  console.log(`  금 간 칸 평균 ${(g.reduce((s, x) => s + x.crack, 0) / g.length).toFixed(1)}개 ·` +
              ` 실제로 무너진 칸 평균 ${(g.reduce((s, x) => s + x.crackBroke, 0) / g.length).toFixed(1)}개 ·` +
              ` 걸음 수가 늘어난 판 ${up}개`);
}
