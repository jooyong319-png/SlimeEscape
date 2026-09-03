// 🔴 상자를 **밀어야 쓰게** 만든다 (2026-09-05, rev.10).
//
// `grow.js` 는 상자를 **필요한 자리에 바로 놓는다.** 그러면 발판이지 퍼즐이 아니다 —
// 테스터가 원한 건 *"박스 밀어가지고 높이 만들어서"* 였다.
//
// 그래서 다 지은 뒤에 상자 **처음 자리를 옆으로 물린다.** 그러면 그 자리까지
// 밀고 가야 쓸 수 있다. 물린 뒤에도 풀리고, 최단해가 하나뿐이어야 한다.
//
//   node tools/boxes.js <들어올 못> <나갈 못>
'use strict';
const fs = require('fs');
const E = require('./engine.js').SlimeEngine;

const solve = grid => E.solve({ grid, gravity: true, clear: 'all', id: 'b' });

/// 상자가 **진짜 발판인가** — 없애면 못 푸는가
function loadBearing(grid) {
  return !solve(grid.map(r => r.replace(/B/g, '.'))).ok;
}

/// 상자 처음 자리를 옆으로 물린다. 물린 판을 준다 (못 물리면 null)
function shiftBoxes(grid, maxShift) {
  const base = solve(grid);
  if (!base.ok) return null;
  const G = grid.map(r => r.split(''));
  const H = G.length, W = G[0].length;
  let moved = 0, gain = 0;

  for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) {
    if (G[y][x] !== 'B') continue;
    let best = null;
    //  가까운 데부터 — 너무 멀면 "왜 저기 있지" 가 된다
    for (let s = 1; s <= (maxShift || 3); s++) {
      for (const dir of [-1, 1]) {
        const nx = x + dir * s;
        if (nx <= 0 || nx >= W - 1) continue;
        if (G[y][nx] !== '.') continue;
        //  가는 길이 뚫려 있어야 밀 수 있다
        let clear = true;
        for (let k = 1; k <= s; k++) if (G[y][x + dir * k] !== '.') { clear = false; break; }
        if (!clear) continue;
        G[y][x] = '.'; G[y][nx] = 'B';
        const a = solve(G.map(r => r.join('')));
        //  🔴 밀어야 쓰게 됐으니 **걸음이 늘어야** 한다. 안 늘면 안 민 것이다.
        if (a.ok && a.shortest === 1 && a.moves > base.moves) {
          if (!best || a.moves > best.moves) best = { nx, moves: a.moves };
        }
        G[y][nx] = '.'; G[y][x] = 'B';
      }
      if (best) break;
    }
    if (best) { G[y][x] = '.'; G[y][best.nx] = 'B'; moved++; gain += best.moves - base.moves; }
  }
  if (!moved) return null;
  return { grid: G.map(r => r.join('')), moved, gain };
}

module.exports = { shiftBoxes, loadBearing };

if (require.main === module) {
  const IN = process.argv[2], OUT = process.argv[3];
  if (!IN) { console.log('쓰기: node tools/boxes.js <들어올 못> <나갈 못>'); process.exit(1); }
  const pool = JSON.parse(fs.readFileSync(IN, 'utf8'));
  const out = [];
  let shifted = 0, kept = 0;
  for (const p of pool) {
    if (!p.grid.join('').includes('B')) { out.push(p); continue; }
    const s = shiftBoxes(p.grid, 3);
    if (!s) { out.push(Object.assign({}, p, { pushed: 0 })); kept++; continue; }
    const a = solve(s.grid);
    out.push(Object.assign({}, p, { grid: s.grid, pushed: s.moved, best: a.moves, sol: a.path }));
    shifted++;
  }
  if (OUT) fs.writeFileSync(OUT, JSON.stringify(out, null, 1) + '\n');
  console.log('밀게 만든 판 ' + shifted + ' · 그대로 둔 판 ' + kept + (OUT ? ' → ' + OUT : ''));
}
