// 🔴 **긴 판에 덤 조각을 얹는다** (2026-09-05 밤).
//
// 09-05 에 덤 조각 얹기가 판당 3분이라 *25걸음 넘는 판은 건너뛰게* 했다.
// 그 결과 **제일 어려워야 할 판에서만 고민거리가 빠졌다** —
// 걸음 25 초과 22판 중 11판이 남는 조각 0개였다 (25 이하는 26판 중 1판).
//
// 사장님: *"48 만든거 너무 쉽다."* 그 판은 남는 조각이 0개였다.
// 조각이 딱 맞으면 **"어느 걸 먹지 말까"가 아예 없다** — 길만 찾으면 된다.
//
//   node tools/addtraps-long.js            보여만 준다
//   WRITE=1 node tools/addtraps-long.js    levels.json 에 쓴다
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { cutOf, putStar } = require('./star.js');
const { rng } = require('./grow.js');

const LP = path.join(__dirname, '../game/Assets/Resources/levels.json');
const CAP = +(process.env.CAP3 || 2500000);
const WANT = +(process.env.WANT || 3);
const MIN = +(process.env.MIN || 26);
const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };
const solve = (g, o) => E.solve({ grid: g, gravity: true, clear: 'all', id: 'x' },
                                Object.assign({ cap: CAP }, o || {}));

/// 정답이 밟는 칸 (떨어지며 지나는 칸까지)
function walkedOf(L, sol) {
  const seen = new Set();
  let st = E.startState(L);
  for (const c of st.body) seen.add(c);
  for (const ch of sol) {
    const d = SYM[ch]; if (d === undefined) continue;
    const ns = E.step(L, st, d); if (!ns) break;
    const f = st.body[0], t = ns.body[0];
    if ((t - f) % L.w === 0 && t !== f) {
      const s = t > f ? L.w : -L.w;
      for (let c = f; c !== t; c += s) seen.add(c);
    }
    for (const c of ns.body) seen.add(c);
    st = ns;
  }
  return seen;
}

const d = JSON.parse(fs.readFileSync(LP, 'utf8'));
let done = 0, added = 0;
for (const lv of d.levels) {
  if (lv.tutorial || lv.demo) continue;
  if (lv.best < MIN) continue;
  const L0 = E.parse({ grid: lv.grid, gravity: true, clear: 'all', id: lv.id });
  const spare = L0.foods.length - L0.doors.reduce((s, x) => s + x.cells.length - 1, 0);
  if (spare >= 2) continue;                    // 이미 고민거리가 있다
  const t0 = Date.now();

  //  별을 뺀 판에 얹는다 — 별 자리는 마지막에 다시 잡는다
  const bare = lv.grid.map(r => r.replace(/o/g, '.'));
  let base = solve(bare);
  if (!base.ok) { console.log('🔴 ' + lv.id + ' 안 풀린다'); continue; }
  const walked = walkedOf(E.parse({ grid: bare, gravity: true, clear: 'all', id: lv.id }), base.path);

  const G = bare.map(r => r.split(''));
  const H = G.length, W = G[0].length;
  const cand = [];
  for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) {
    if (G[y][x] !== '.') continue;
    const c = y * W + x;
    if (walked.has(c)) continue;
    let near = 0;
    for (const n of [c - 1, c + 1, c - W, c + W]) if (walked.has(n)) near++;
    if (near) cand.push([x, y, near]);
  }
  cand.sort((a, b) => b[2] - a[2]);            // 정답 길에 많이 닿는 칸부터
  const trialCap = Math.min(CAP, base.states * 3 + 60000);
  let put = 0;
  for (const [x, y] of cand) {
    if (put >= WANT) break;
    G[y][x] = '+';
    const a = solve(G.map(r => r.join('')), { cap: trialCap });
    //  🔴 **최단해가 그대로여야 한다.** 판 자료의 sol 을 그대로 쓰려면 필요하다.
    if (a.ok && a.moves === base.moves && a.shortest === 1) put++;
    else G[y][x] = '.';
  }
  const grid2 = G.map(r => r.join(''));
  if (!put) { console.log('  ' + lv.id.padEnd(7) + '얹을 자리 없음 (후보 ' + cand.length + ')'); done++; continue; }

  //  별을 다시 잡는다 — 제일 멀리 도는 자리로
  const a2 = solve(grid2);
  const st2 = putStar(grid2, a2.moves, rng(lv.best * 31 + 7), 24, 12,
                      Math.min(CAP, a2.states * 3 + 60000));
  const grid3 = st2 ? st2.grid : grid2;
  const a3 = solve(grid3);
  const s3 = solve(grid3, { needStar: true });
  if (!a3.ok || !s3.ok) { console.log('🔴 ' + lv.id + ' 별 넣고 깨졌다 — 그대로 둔다'); done++; continue; }

  lv.grid = grid3; lv.best = a3.moves; lv.sol = a3.path;
  lv.bestStar = s3.moves; lv.cut = cutOf(s3.moves);
  lv.traps = (lv.traps || 0) + put;
  added += put; done++;
  console.log('  ' + lv.id.padEnd(7) + '덤 +' + put + ' · 걸음 ' + a3.moves +
              ' · 별먹고 ' + s3.moves + ' · ' + ((Date.now() - t0) / 1000).toFixed(0) + '초');
}
console.log('\n손본 판 ' + done + ' · 얹은 덤 조각 ' + added);
if (process.env.WRITE !== '1') { console.log('(보여주기만 했다. 쓰려면 WRITE=1)'); process.exit(0); }
fs.writeFileSync(path.join(__dirname, 'levels.bak.json'), fs.readFileSync(LP));
fs.writeFileSync(LP, JSON.stringify(d, null, 2) + '\n');
console.log('판 자료에 썼다');
