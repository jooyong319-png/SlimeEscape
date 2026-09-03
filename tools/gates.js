// 🔴 다 만든 판에 **문벽**을 얹는다 (2026-09-05, rev.10).
//
// 문벽 `1` `2` `3` 은 짝이 되는 홈을 채우면 사라지는 벽이다.
// 엔진(JS·C#)과 그리기·사라지는 연출까지 **08-30 부터 다 돼 있었는데**,
// 47판 중 쓰는 판이 하나도 없었다 — 쓰면 카메라가 방을 따라 미끄러졌기 때문이다.
// rev.10 에서 방 나누기를 걷어냈으니 이제 쓸 수 있다.
//
// 놓는 자리: **정답이 그 홈을 채운 뒤에야 처음 밟는 칸.**
// 그러면 정답은 그대로 통하고, 일찍 그쪽으로 가려던 사람만 막힌다.
//
//   node tools/gates.js <들어올 못> <나갈 못>
'use strict';
const fs = require('fs');
const E = require('./engine.js').SlimeEngine;
const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };

/// 정답을 따라가며 **칸마다 「처음 밟을 때 어느 홈까지 채웠나」** 를 적는다
function firstTouch(L, sol) {
  const when = new Map();          // 칸 -> 그때의 dm
  let st = E.startState(L);
  const mark = (cells, dm) => { for (const c of cells) if (!when.has(c)) when.set(c, dm); };
  mark(st.body, st.dm || 0);
  for (const ch of sol) {
    const d = SYM[ch];
    if (d === undefined) continue;
    const ns = E.step(L, st, d);
    if (!ns) break;
    //  떨어진 구간도 밟은 것으로 친다
    const from = st.body[0], to = ns.body[0];
    if ((to - from) % L.w === 0 && to !== from) {
      const step = to > from ? L.w : -L.w;
      for (let c = from; c !== to; c += step) if (!when.has(c)) when.set(c, st.dm || 0);
    }
    mark(ns.body, ns.dm || 0);
    st = ns;
  }
  return when;
}

/// 열린 칸 넷 중 몇 개가 이웃인가 — 좁은 자리일수록 '문' 처럼 읽힌다
function openNb(G, W, H, x, y) {
  let n = 0;
  for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
    const nx = x + dx, ny = y + dy;
    if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
    if (G[ny][nx] !== '#') n++;
  }
  return n;
}

/// 판 하나에 문벽을 얹는다. 얹은 개수를 준다 (못 얹으면 null)
function addGates(grid, want) {
  const def = { grid, gravity: true, clear: 'all', id: 'g' };
  const base = E.solve(def);
  if (!base.ok) return null;
  const L = E.parse(def);
  const doors = L.doors.length;
  if (doors < 2) return null;             // 홈이 하나면 열 문이 없다
  const when = firstTouch(L, base.path);

  const G = grid.map(r => r.split(''));
  const H = G.length, W = G[0].length;

  let put = 0;
  //  홈 0 부터 doors-2 까지 — 마지막 홈의 문은 다 끝난 뒤에나 열려 쓸모가 없다
  for (let gi = 0; gi < doors - 1 && put < want; gi++) {
    const bit = 1 << gi;
    const cand = [];
    for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) {
      if (G[y][x] !== '.') continue;              // 빈 칸에만
      const c = y * W + x;
      const dm = when.get(c);
      if (dm === undefined) continue;             // 정답이 안 밟는 칸 — 막아도 티가 안 난다
      if (!(dm & bit)) continue;                  // 🔴 그 홈을 채우기 **전에** 밟는다 → 막으면 못 깬다
      cand.push([x, y, openNb(G, W, H, x, y)]);
    }
    //  좁은 자리부터 — 넓은 데 한 칸 세우면 벽이 아니라 기둥으로 보인다
    cand.sort((a, b) => a[2] - b[2]);
    for (const [x, y] of cand) {
      G[y][x] = String.fromCharCode(49 + gi);
      const g2 = G.map(r => r.join(''));
      const a = E.solve({ grid: g2, gravity: true, clear: 'all', id: 'g' });
      //  최단해가 그대로이고 여전히 하나뿐이어야 한다
      if (a.ok && a.moves === base.moves && a.shortest === 1) { put++; break; }
      G[y][x] = '.';
    }
  }
  return put ? { grid: G.map(r => r.join('')), gates: put } : null;
}

module.exports = { addGates };

if (require.main === module) {
  const IN = process.argv[2] || 'tools/trap-pool.json';
  const OUT = process.argv[3] || 'tools/gate-pool.json';
  const WANT = +(process.env.GATES || 2);
  const pool = JSON.parse(fs.readFileSync(IN, 'utf8'));
  console.log(IN, '—', pool.length, '개 ·', WANT, '개까지 얹는다');
  const out = [];
  let none = 0;
  for (let i = 0; i < pool.length; i++) {
    const p = pool[i];
    const r = addGates(p.grid, WANT);
    if (!r) { none++; out.push(Object.assign({}, p, { gates: 0 })); continue; }
    const a = E.solve({ grid: r.grid, gravity: true, clear: 'all', id: 'g' });
    out.push(Object.assign({}, p, { grid: r.grid, gates: r.gates, best: a.moves, sol: a.path }));
    //  🔴 중간에 저장한다 — 도중에 끊겨도 거기까지는 남는다
    if ((i + 1) % 20 === 0) {
      fs.writeFileSync(OUT, JSON.stringify(out, null, 1) + '\n');
      process.stderr.write('  ' + (i + 1) + '/' + pool.length + '\n');
    }
  }
  fs.writeFileSync(OUT, JSON.stringify(out, null, 1) + '\n');
  const avg = out.reduce((s, p) => s + p.gates, 0) / out.length;
  console.log('문벽 평균 ' + avg.toFixed(2) + '개 · 하나도 못 얹은 판 ' + none + ' → ' + OUT);
}
