// 🔴 **브레이크 판을 만든다** (rev.12, 2026-09-05).
//
// 얼음을 넓히기만 해선 안 어려웠다. 답이 그냥 **딴 길로 돌아간다** —
// 미끄러진 걸음 77번 중 "고민하는 멈춤"은 2번뿐이었다 (나머지는 벽이나 얼음 끝).
//
// 이 게임에서 얼음 위에 **놓을 수 있는 것**은 둘뿐이다:
//   · 홈을 채우고 남긴 **굳은 몸** (자리가 정해져 있다)
//   · **미는 상자** (내가 옮길 수 있다)  ← 이쪽이 진짜 브레이크다
//
// 그래서 거꾸로 만든다:
//   ① 얼음을 **못 풀 만큼** 넓힌다 (서고 싶은 자리에 못 서게 된다)
//   ② 상자를 하나 놓아 **다시 풀리게** 한다
//   → 그러면 그 상자는 **반드시 브레이크로 쓰인다.** 안 그러면 안 풀리니까.
//
// 사장님이 상자를 물리셨던 건("4판 중 1판만 괜찮았어") 상자가 그냥 **발판**이어서다.
// 여기서는 쓰임이 다르다 — **멈추기 위한 것**이다.
//
//   node tools/brake.js tools/ice-pool.json
'use strict';
const fs = require('fs');
const E = require('./engine.js').SlimeEngine;

const IN   = process.argv[2] || 'tools/ice-pool.json';
const OUT  = process.env.OUT || IN.replace(/\.json$/, '-brake.json');
const WIDE = +(process.env.WIDE || 14);          // 얼음을 몇 칸까지 넓혀 보나
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
const solve = g => E.solve({ grid: g, gravity: true, clear: 'all', id: 'b' }, { cap: CAP });

/// 🔴 **얼음 줄을 양옆으로 늘릴 자리.**
///    처음엔 "정답이 내려앉는 칸"만 후보로 삼았더니 두세 칸 만에 말라버렸다
///    (얼음이 되고 나면 더는 후보가 아니라서). 줄을 통째로 늘려야 복도가 미끄러워진다.
function widenSpots(grid) {
  const L = E.parse({ grid, gravity: true, clear: 'all', id: 'w' });
  const out = new Set();
  const okCell = c => {
    if (c < 0 || c >= L.w * L.h) return false;
    const y = (c / L.w) | 0, x = c % L.w;
    if (y <= 0 || y >= L.h - 1 || x <= 0 || x >= L.w - 1) return false;
    if (L.g[y][x] !== '.') return false;
    if (c === L.start || c === L.star) return false;
    if (L.foodIdx.has(c) || L.iceSet.has(c) || L.portal.has(c)) return false;
    if (L.zoneSet.has(c) || L.crackIdx.has(c)) return false;
    if ((L.boxes || []).includes(c)) return false;
    if (L.doors.some(d => d.set.has(c))) return false;
    return true;
  };
  for (const c of L.iceSet) {
    //  같은 줄에서만 늘린다 — 얼음은 가로로만 뜻이 있다
    for (const n of [c - 1, c + 1])
      if (((n / L.w) | 0) === ((c / L.w) | 0) && okCell(n)) out.add(n);
  }
  return [...out];
}

/// 정답에서 **가로로 움직여 내려앉은 칸** — 여기를 얼음으로 바꾸면 그 자리에 못 선다
function landings(grid, path) {
  const L = E.parse({ grid, gravity: true, clear: 'all', id: 'r' });
  let st = E.startState(L);
  const out = [];
  for (const ch of path) {
    const d = SYM[ch];
    const n = E.step(L, st, d);
    if (!n) return [];
    if (d === 2 || d === 3) {
      const h = n.body[0];
      if (!L.iceSet.has(h) && !L.foodIdx.has(h) && h !== L.start && h !== L.star
          && !L.doors.some(dd => dd.set.has(h)) && !L.portal.has(h)
          && !L.zoneSet.has(h) && !L.crackIdx.has(h) && !(L.boxes || []).includes(h))
        out.push(h);
    }
    st = n;
  }
  return [...new Set(out)];
}

/// 이 판에서 정답이 상자를 **정말 쓰는가** — 상자를 빼면 못 풀려야 한다
function boxNeeded(grid) {
  const bare = grid.map(x => x.replace(/B/g, '.'));
  return !solve(bare).ok;
}

/// 상자에 부딪혀 멈춘 걸음이 있는가 (그림으로도 읽히는 순간)
function brakeMoves(grid, path) {
  const L = E.parse({ grid, gravity: true, clear: 'all', id: 'r' });
  let st = E.startState(L), hit = 0;
  for (const ch of path) {
    const d = SYM[ch];
    const n = E.step(L, st, d);
    if (!n) break;
    if (d === 2 || d === 3) {
      const gap = Math.abs((n.body[0] % L.w) - (st.body[0] % L.w));
      if (gap >= 2) {
        const dx = d === 3 ? 1 : -1, c = n.body[0] + dx;
        if ((n.bx || []).includes(c)) hit++;
      }
    }
    st = n;
  }
  return hit;
}

const pool = JSON.parse(fs.readFileSync(IN, 'utf8'));
const out = [];
let made = 0, t0 = Date.now();

for (let i = 0; i < pool.length; i++) {
  const p = pool[i];
  if (!p.grid.join('').includes('I')) continue;      // 얼음 판만
  if (p.grid.join('').includes('B')) continue;       // 이미 상자가 있으면 건드리지 않는다
  const W = p.grid[0].length;
  const r = rng(i * 4517 + 29);

  let grid = p.grid;
  let a = solve(grid);
  if (!a.ok) continue;

  //  ① 못 풀 때까지 얼음을 넓힌다
  let broke = false;
  for (let k = 0; k < WIDE && !broke; k++) {
    //  🔴 내려앉는 칸을 **먼저**, 그다음 얼음 줄 늘리기. 앞쪽이 더 곧장 막는다.
    const land = landings(grid, a.path);
    const cand = land.concat(widenSpots(grid).filter(c => !land.includes(c)))
                     .sort(() => r() - 0.5);
    if (!cand.length) break;
    let moved = false;
    for (const cell of cand) {
      const trial = put(grid, cell, W, 'I');
      const t = solve(trial);
      if (t.ok && t.shortest === 1) { grid = trial; a = t; moved = true; break; }
      if (!t.ok) { grid = trial; broke = true; moved = true; break; }   // 여기서 막혔다
    }
    if (!moved) break;
  }
  if (!broke) continue;                              // 안 막혔으면 브레이크가 필요 없다

  //  ② 상자 하나로 다시 풀리게 한다
  const L = E.parse({ grid, gravity: true, clear: 'all', id: 'r' });
  const spots = [];
  for (let y = 1; y < L.h - 1; y++) for (let x = 1; x < L.w - 1; x++) {
    const c = y * L.w + x;
    if (L.g[y][x] !== '.') continue;
    if (c === L.start || c === L.star) continue;
    if (L.foodIdx.has(c) || L.iceSet.has(c) || L.portal.has(c)) continue;
    if (L.zoneSet.has(c) || L.crackIdx.has(c)) continue;
    if (L.doors.some(d => d.set.has(c))) continue;
    spots.push(c);
  }
  spots.sort(() => r() - 0.5);

  let done = null;
  for (const c of spots) {
    const trial = put(grid, c, W, 'B');
    const t = solve(trial);
    if (!t.ok || t.shortest !== 1) continue;
    if (!boxNeeded(trial)) continue;                 // 상자가 없어도 풀리면 뜻이 없다
    done = { grid: trial, best: t };
    break;
  }
  if (!done) continue;

  const hit = brakeMoves(done.grid, done.best.path);
  out.push({ ...p, grid: done.grid, best: done.best.moves, sol: done.best.path,
             wasBest: p.best, brake: hit,
             gim: (p.gim === 'none' ? '' : p.gim + '+') + 'brake' });
  made++;
  console.log(`  ${i + 1}/${pool.length} · 만든 판 ${made} · 상자에 부딪혀 멈춤 ${hit}번` +
              ` · ${((Date.now() - t0) / 1000).toFixed(0)}초`);
}

fs.writeFileSync(OUT, JSON.stringify(out, null, 1) + '\n');
console.log(`\n브레이크 판 ${out.length}개 → ${OUT}`);
if (out.length)
  console.log(`  상자에 부딪혀 멈추는 판 ${out.filter(x => x.brake > 0).length}개`);
