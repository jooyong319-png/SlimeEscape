// 🔴 **얼음을 넓혀 「멈추고 싶은 자리에서 못 멈추게」 한다** (rev.12, 2026-09-05).
//
// 왜 필요한가 — 지으면서 넣은 얼음은 **빠른 복도**일 뿐이었다.
// 재보니 미끄러진 걸음 29번 중
//   · 벽에 부딪혀 멈춤 10   · 얼음이 끝나서 멈춤 17   · 굳은 몸에 부딪힘 2
// 즉 27번은 **고민할 게 없다.** 알아서 멈춰 준다.
//
// 얼음이 어려워지는 건 딱 하나, **서고 싶은 칸에 못 서는 것**이다.
// 그래서 정답이 *멈추던 칸*을 얼음으로 바꿔 **지나쳐 버리게** 만든다.
// 그러면 거기 서려면 뭔가로 막아야 하고, 그 뭔가가 결국 **자기 몸**이다.
//
// 🔴 받는 조건은 addcrack.js 와 같다: **원래 길이 더는 안 통해야 한다.**
//    "다시 풀린다"만 보면 그냥 판을 미끄럽게 칠한 것이 된다.
//
//   node tools/spreadice.js tools/ice-pool.json
'use strict';
const fs = require('fs');
const E = require('./engine.js').SlimeEngine;

const IN   = process.argv[2] || 'tools/ice-pool.json';
const OUT  = process.env.OUT || IN.replace(/\.json$/, '-wide.json');
const ADD  = +(process.env.ADD || 5);            // 한 판에 넓힐 칸 수
const CAP  = +(process.env.SOLVE_CAP || 700000);
const SYM  = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };

function rng(seed) {
  let s = (seed | 0) || 1;
  return () => { s ^= s << 13; s |= 0; s ^= s >>> 17; s ^= s << 5; s |= 0;
                 return ((s >>> 0) % 100000) / 100000; };
}

const put = (grid, cell, W, ch) => {
  const y = (cell / W) | 0, x = cell % W;
  const r = grid[y].split(''); r[x] = ch;
  const g = grid.slice(); g[y] = r.join(''); return g;
};

/// 이 정답이 이 판에서 **그대로 통하는가**
function replays(grid, path) {
  const L = E.parse({ grid, gravity: true, clear: 'all', id: 'r' });
  let st = E.startState(L);
  for (const ch of path) { const n = E.step(L, st, SYM[ch]); if (!n) return false; st = n; }
  return E.isWin(L, st, false);
}

/// 정답을 따라가며 **가로로 움직여 내려앉은 칸**을 모은다.
/// 거기를 얼음으로 바꾸면 그 자리에 못 선다.
function landings(grid) {
  const L = E.parse({ grid, gravity: true, clear: 'all', id: 'r' });
  const a = E.solve({ grid, gravity: true, clear: 'all', id: 'r' }, { cap: CAP });
  if (!a.ok || a.shortest !== 1) return null;
  let st = E.startState(L);
  const out = [];
  for (const ch of a.path) {
    const d = SYM[ch];
    const n = E.step(L, st, d);
    if (!n) return null;
    if (d === 2 || d === 3) {
      const h = n.body[0];
      //  이미 얼음이면 넓힐 게 없다. 홈·조각 칸도 못 바꾼다 (뜻이 달라진다)
      if (!L.iceSet.has(h) && !L.foodIdx.has(h) && h !== L.start
          && !L.doors.some(dd => dd.set.has(h))
          && !L.portal.has(h) && !L.zoneSet.has(h) && !L.crackIdx.has(h)
          && !(L.boxes || []).includes(h) && h !== L.star)
        out.push(h);
    }
    st = n;
  }
  return { cells: [...new Set(out)], best: a };
}

const pool = JSON.parse(fs.readFileSync(IN, 'utf8'));
const out = [];
let widened = 0, t0 = Date.now();

for (let i = 0; i < pool.length; i++) {
  const p = pool[i];
  const W = p.grid[0].length;
  let grid = p.grid, n = 0;
  const r = rng(i * 7717 + 11);
  let cur = landings(grid);
  if (!cur) { out.push(p); continue; }

  for (let round = 0; round < ADD; round++) {
    if (!cur) break;
    const cand = cur.cells.slice().sort(() => r() - 0.5);
    let took = false;
    for (const cell of cand) {
      const trial = put(grid, cell, W, 'I');
      //  🔴 **원래 길이 막혀야** 넓힌 뜻이 있다
      if (replays(trial, cur.best.path)) continue;
      const nx = landings(trial);
      if (!nx) continue;                        // 못 풀거나 최단이 여럿
      grid = trial; cur = nx; n++; took = true; break;
    }
    if (!took) break;
  }

  if (!n) { out.push(p); continue; }
  out.push({ ...p, grid, best: cur.best.moves, sol: cur.best.path,
             wasBest: p.best, wide: n });
  widened++;
  if ((i + 1) % 10 === 0)
    console.log(`  ${i + 1}/${pool.length} · 넓힌 판 ${widened} · ${((Date.now() - t0) / 1000).toFixed(0)}초`);
}

fs.writeFileSync(OUT, JSON.stringify(out, null, 1) + '\n');
const g = out.filter(x => x.wide);
console.log(`\n판 ${out.length}개 중 ${widened}개의 얼음을 넓혔다 → ${OUT}`);
if (g.length)
  console.log(`  넓힌 칸 평균 ${(g.reduce((s, x) => s + x.wide, 0) / g.length).toFixed(1)}개`);
