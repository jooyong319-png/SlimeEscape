// 🔴 **피날레 판을 짓는다** — 기믹 넷이 한 판에 (2026-09-05).
//
// 사장님: *"마지막 5판은 최대한 많이 섞이고 큰 맵이면 좋겠어. 정말 어렵게."*
//
// hardpool.js 로도 되지만 **띠가 끝나야 저장**해서 몇 시간을 돌려도 화면에 아무것도 안 나온다.
// 피날레는 수율이 낮아 그게 치명적이라 따로 뺐다 — **한 판 나올 때마다 저장하고 찍는다.**
//
// 🔴 넷이 **다 발동해야** 받는다. 다만 문턱을 잘못 잡으면 다 걸러진다 —
//    처음에 「얼음 2번 미끄러짐 · 상자를 민다」로 뒀더니 **0판**이었다.
//    재보니 이 판들에서 상자의 쓰임은 미는 게 아니라 **딛고 서는 것**이었다.
//    고치니 최단 유일 12판 중 9판이 통과했다.
//
//   node tools/finale.js            30분 돌린다
//   MIN=40 node tools/finale.js     40분
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { grow, rng, SHAPES } = require('./grow.js');

const OUT = process.env.OUT || path.join(__dirname, 'final-pool.json');
const MIN = +(process.env.MIN || 30);              // 몇 분 돌리나
const WANT = +(process.env.WANT || 12);
//  ⚠️ 판이 커지면 풀어야 할 상태가 폭증한다 — 상한이 낮으면 **큰 판이 전부 capped 로 버려진다.**
//     09-17 에 틀을 키우면서 200만 → 400만으로 올렸다. 더 오래 걸리는 대신 큰 판이 살아남는다.
const CAP = +(process.env.SOLVE_CAP || 4000000);
//  🔴 **걸음 하한** (사장님: *"정말 어렵게"*). 20 이었을 때 24걸음짜리도 통과했다 —
//     지금 쓰는 1-36 이 딱 24걸음이라, 그것보다 쉬운 판이 후보로 올라오는 셈이었다.
const MOVES = +(process.env.MOVES || 26);
const D = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };

//  🔴 넷을 한 판에 넣으려면 **홈은 둘 · 굴은 넓게** 여야 한다.
//     3홈 19x11 ratio 0.32 → 씨앗 1500개에 넷 다 든 판 1개
//     2홈 19x11 ratio 0.42 → 씨앗 1500개에 넷 다 든 판 14개
//  🔴 **판이 왜 작게 나왔나** (09-17). 사장님: *"큰 맵이면 좋겠어"* 였는데 뽑아 보니
//     실제 격자가 13x10~18x10 으로, 지금 쓰는 1-36~1-40(17x10~19x10)보다 **작았다.**
//     까닭은 둘이 겹친 것이다:
//      ① carve() 는 틀 전체가 아니라 `(W-2)*(H-2)*ratio` 칸만 뚫는다 — ratio 0.38~0.44 면 **40% 남짓.**
//      ② grow() 는 끝에 crop() 으로 **안 쓰인 바깥 통돌을 잘라낸다** (벽 한 겹만 남긴다).
//     그래서 19x11 틀이 16x10 으로 줄었다. 키우려면 **틀도 키우고 ratio 도 올려야** 한다.
//
//  🔴 ratio 를 올리는 건 수율에도 좋다 — 위 기록대로 0.32 에서 1 개, 0.42 에서 14 개였다.
//     넷을 한 판에 넣으려면 **홈은 둘 · 굴은 넓게** 여야 하기 때문이다.
//  ⚠️ 다만 판이 커지면 풀어야 할 상태가 폭증한다. SOLVE_CAP 을 같이 올려 두지 않으면
//     큰 판이 전부 capped 로 버려져 **오히려 한 판도 안 나온다.**
const BANDS = [
  { W: 23, H: 13, ratio: 0.54, wmin: 20, wmax: 40, lens: [6, 5], turn: 2, box: 2, ice: 12, crack: 4 },
  { W: 23, H: 13, ratio: 0.52, wmin: 20, wmax: 40, lens: [7, 5], turn: 2, box: 2, ice: 12, crack: 4 },
  { W: 25, H: 13, ratio: 0.56, wmin: 22, wmax: 42, lens: [7, 6], turn: 2, box: 1, ice: 13, crack: 4 },
  { W: 21, H: 13, ratio: 0.50, wmin: 16, wmax: 34, lens: [6, 5], turn: 2, box: 2, ice: 11, crack: 3 },
];

/// 정답을 되짚어 넷이 **정말 발동했는지** 센다
function fired(grid, sol) {
  const L = E.parse({ grid, gravity: true, clear: 'all', id: 'f' });
  let st = E.startState(L);
  let slid = 0, warp = 0, push = 0, stand = 0;
  for (const ch of sol) {
    const n = E.step(L, st, D[ch]);
    if (!n) return null;
    if ((n._n || 1) >= 2) slid++;
    const hx = st.body[0] % L.w, hy = (st.body[0] / L.w) | 0;
    const nx = n.body[0] % L.w, ny = (n.body[0] / L.w) | 0;
    //  머리가 미끄러진 칸 수보다 멀리 갔으면 포탈을 탄 것이다
    if (Math.abs(hx - nx) + Math.abs(hy - ny) > (n._n || 1) + 1) warp++;
    if ((st.bx || []).join(',') !== (n.bx || []).join(',')) push++;
    //  🔴 상자는 **딛고 서는 것도** 쓰임이다 (밀기만 세면 12판 중 3판만 남는다)
    for (const c of n.body) if ((n.bx || []).includes(c + L.w)) { stand++; break; }
    st = n;
  }
  if (!E.isWin(L, st, false)) return null;
  let brk = 0;
  for (let k = 0; k < L.crack.length; k++) if ((st.cm || 0) & (1 << k)) brk++;
  if (slid < 1 || brk < 1 || warp < 1 || (push < 1 && stand < 1)) return null;
  return { slid, brk, warp, push, stand };
}

const out = fs.existsSync(OUT) ? JSON.parse(fs.readFileSync(OUT, 'utf8')) : [];
const seen = new Set(out.map(p => p.grid.join('')));
console.log('있던 판 ' + out.length + '개에 이어서 만든다 · ' + MIN + '분 돈다');

const t0 = Date.now();
let tried = 0, all4 = 0, uniq = 0, seed = +(process.env.SEED || 1);
while (Date.now() - t0 < MIN * 60000 && out.length < WANT) {
  const b = BANDS[seed % BANDS.length];
  const r = rng(seed * 7919 + b.W * 131 + Math.round(b.ratio * 1000));
  seed++;
  const grid = grow(r, b.W, b.H, b.lens, b.ratio, b.wmin, b.wmax,
                    SHAPES[Math.floor(r() * SHAPES.length)],
                    0, b.box, 1, b.turn, 0, b.ice, b.crack);
  if (!grid) continue;
  tried++;
  const t = grid.join('');
  if (!(t.includes('I') && t.includes('X') && t.includes('B') && /[PQpq]/.test(t))) continue;
  all4++;
  const a = E.solve({ grid, gravity: true, clear: 'all', id: 'f' }, { cap: CAP });
  if (!a.ok || a.capped || a.shortest !== 1 || a.moves < MOVES) continue;
  uniq++;
  const key = t;
  if (seen.has(key)) continue;
  const f = fired(grid, a.path);
  if (!f) continue;
  seen.add(key);
  out.push({ grid, best: a.moves, sol: a.path, states: a.states,
             doors: 2, W: b.W, H: b.H, ratio: b.ratio,
             ice: b.ice, crack: b.crack, box: b.box, port: 1,
             gim: 'ice+crack+box+portal', ...f });
  //  🔴 **한 판 나올 때마다 저장한다.** 몇 시간짜리를 통째로 날린 적이 있다 (09-05).
  fs.writeFileSync(OUT, JSON.stringify(out, null, 1) + '\n');
  console.log(`  ★ ${out.length}판째 · ${b.W}x${b.H} · ${a.moves}걸음 ·` +
              ` 미끄럼 ${f.slid} 무너짐 ${f.brk} 포탈 ${f.warp} 상자 ${f.push + f.stand} ·` +
              ` ${((Date.now() - t0) / 60000).toFixed(1)}분`);
}
console.log(`\n지어봄 ${tried} · 넷 다 든 판 ${all4} · 최단유일 ${uniq} · **받은 판 ${out.length}** → ${OUT}`);
