// 🔴 **기믹 둘을 섞은 판**을 짓는다 (2026-09-20).
//
// 사장님이 굴 구성을 이렇게 잡으셨다 — 소개와 조합을 **번갈아** 둔다:
//   1 기본 · 2 상자 · 3 얼음 · **4 상자+얼음** · 5 금간 · **6 금간+얼음 / 상자+금간**
//   · 7 포탈 · **8 포탈 조합** · 9 새 기믹 · 10 새 기믹+조합 · 11~12 몸 둘
//
// 🔴 **어려움은 「새 규칙」이 만든다** (위키 09-05). 판을 잘 고르는 것으로는 여덟 번 틀렸고,
//    09-20 에 나도 네 번 더 틀렸다 (걸음 수 · 상태수 · lost · 홈 개수). 그러니 재료는
//    **새 규칙**이어야 한다 — 기믹 하나로는 네 번뿐이지만, 둘씩 섞으면 여섯 번이 더 생긴다.
//    조합에는 그 조합에서만 나오는 규칙이 있다. 예: **상자는 얼음 위에서 안 미끄러진다.**
//
// ⚠️ 못에 쌓인 1,418판에는 조합이 거의 없다 — 얼음+포탈 35 · 금간+얼음 23 · 넷 9 뿐이고
//    **상자가 낀 조합은 0개**다. 옛 도구들이 기믹을 하나씩만 넣었기 때문이다. 그래서 새로 짓는다.
//
//   node tools/combo.js                    기본: 상자+얼음
//   MIX=box+crack MIN=30 node tools/combo.js
//   MIX=crack+portal WANT=20 node tools/combo.js
//
// 받는 조건 (위키: 도구가 거르는 건 **객관적인 것만**)
//   · 풀린다 · 최단해가 하나 · **지정한 기믹이 정답에서 전부 발동** · 다른 기믹은 안 섞인다 · 안 겹침
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { grow, rng, SHAPES } = require('./grow.js');

const MIX = (process.env.MIX || 'box+ice').split('+').map(s => s.trim()).sort();
const MIN = +(process.env.MIN || 25);              // 몇 분 돌리나
const WANT = +(process.env.WANT || 16);
const CAP = +(process.env.SOLVE_CAP || 2000000);
const MOVES = +(process.env.MOVES || 20);          // 걸음 하한
const OUT = process.env.OUT || path.join(__dirname, 'combo-' + MIX.join('+') + '.json');
const D = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };

const KNOWN = ['box', 'crack', 'ice', 'portal'];
for (const k of MIX) if (!KNOWN.includes(k)) { console.error('모르는 기믹: ' + k + ' (쓸 수 있는 것: ' + KNOWN.join(' ') + ')'); process.exit(1); }

//  격자 글자 — 기믹이 들어 있나
const CH = { box: /B/, crack: /X/, ice: /I/, portal: /[PQpq]/ };

//  🔴 조합은 **둘 다 넉넉히** 깔려야 섞인 판이 나온다. 하나만 많으면 다른 쪽이 장식이 된다.
//     틀은 finale.js 를 손볼 때 배운 값을 쓴다 — carve 는 틀의 ratio 만큼만 뚫고
//     grow 는 끝에 남는 통돌을 잘라내므로, **틀과 ratio 를 같이 키워야** 판이 커진다.
const BANDS = [
  { W: 21, H: 12, ratio: 0.50, wmin: 18, wmax: 38, lens: [6, 5], turn: 2 },
  { W: 21, H: 12, ratio: 0.52, wmin: 18, wmax: 38, lens: [7, 5], turn: 2 },
  { W: 19, H: 12, ratio: 0.48, wmin: 16, wmax: 34, lens: [6, 5], turn: 2 },
  { W: 23, H: 12, ratio: 0.54, wmin: 20, wmax: 40, lens: [7, 6], turn: 2 },
];

/// 그 기믹에 몇 칸을 줄까 — 고른 것만 준다
function budget(kind, b) {
  const has = k => MIX.includes(k);
  return {
    box: has('box') ? 2 : 0,
    ice: has('ice') ? 11 : 0,
    crack: has('crack') ? 4 : 0,
    port: has('portal') ? 1 : 0,
  };
}

/// 정답을 되짚어 기믹이 **정말 발동했는지** 센다 (finale.js 와 같은 셈법)
function fired(grid, sol, clear) {
  const L = E.parse({ grid, gravity: true, clear: clear || 'all', id: 'c' });
  let st = E.startState(L);
  let slid = 0, warp = 0, push = 0, stand = 0;
  for (const ch of sol) {
    const n = E.step(L, st, D[ch]);
    if (!n) return null;
    if ((n._n || 1) >= 2) slid++;
    const hx = st.body[0] % L.w, hy = (st.body[0] / L.w) | 0;
    const nx = n.body[0] % L.w, ny = (n.body[0] / L.w) | 0;
    //  🔴 포탈을 탔나. ⚠️ **가로로 튄 것만** 센다 — 예전 식은 세로 거리도 더해서
    //     **중력으로 떨어진 것을 포탈로 오해**했다 (09-20: 포탈이 아예 없는 box+crack 판에
    //     "포탈 2" 가 찍혔다). 낙하는 세로로만 멀리 가고, 포탈은 짝이 있는 곳으로 **가로로 튄다.**
    //     세로로만 움직였으면 포탈이 아니다.
    if (Math.abs(hx - nx) > (n._n || 1) + 1) warp++;
    if ((st.bx || []).join(',') !== (n.bx || []).join(',')) push++;
    for (const c of n.body) if ((n.bx || []).includes(c + L.w)) { stand++; break; }
    st = n;
  }
  if (!E.isWin(L, st, false)) return null;
  let brk = 0;
  for (let k = 0; k < L.crack.length; k++) if ((st.cm || 0) & (1 << k)) brk++;
  return { slid, brk, warp, box: push + stand };
}

/// <summary>
/// 고른 기믹이 **전부, 넉넉히** 발동했나.
/// 🔴 **두 번 이상**을 받는다. 한 번만 미끄러지고 마는 판은 얼음이 **장식**이지 규칙이 아니다 —
///    사장님이 3(버림)을 주는 까닭 중 하나가 *"굴의 테마가 적절히 사용되지 않은 것"* 이다.
///    09-20 시험: 1 회 이상으로 받았더니 6판 중 5판이 「미끄럼 1」 이었다.
/// ⚠️ 포탈 셈(warp)은 **중력으로 떨어지는 것을 포탈로 오해한다** — 머리가 한 칸 넘게 움직이면
///    세는데, 낙하도 그렇다. 포탈이 없는 판에서 "포탈 2" 가 찍힌 게 그것이다.
///    여기서는 고른 기믹만 보므로 해가 없지만, `finale.js` 도 같은 식을 쓴다.
/// </summary>
function allFired(f) {
  const need = { ice: f.slid, crack: f.brk, portal: f.warp, box: f.box };
  return MIX.every(k => need[k] >= 2);
}

const out = fs.existsSync(OUT) ? JSON.parse(fs.readFileSync(OUT, 'utf8')) : [];
const seen = new Set(out.map(p => p.grid.join('')));
console.log('조합 [' + MIX.join(' + ') + '] · 있던 판 ' + out.length + '개에 이어서 · ' + MIN + '분 돈다');

const t0 = Date.now();
let tried = 0, both = 0, uniq = 0, seed = +(process.env.SEED || 1);
while (Date.now() - t0 < MIN * 60000 && out.length < WANT) {
  const b = BANDS[seed % BANDS.length];
  const r = rng(seed * 7919 + b.W * 131 + Math.round(b.ratio * 1000));
  seed++;
  const bd = budget(MIX, b);
  const grid = grow(r, b.W, b.H, b.lens, b.ratio, b.wmin, b.wmax,
                    SHAPES[Math.floor(r() * SHAPES.length)],
                    0, bd.box, bd.port, b.turn, 0, bd.ice, bd.crack);
  if (!grid) continue;
  tried++;
  const t = grid.join('');
  //  고른 기믹은 다 있어야 하고, 안 고른 기믹은 없어야 한다 — 순수한 조합이라야 그 굴의 규칙이 된다
  if (!MIX.every(k => CH[k].test(t))) continue;
  if (KNOWN.some(k => !MIX.includes(k) && CH[k].test(t))) continue;
  both++;
  const a = E.solve({ grid, gravity: true, clear: 'all', id: 'c' }, { cap: CAP });
  if (!a.ok || a.capped || a.shortest !== 1 || a.moves < MOVES) continue;
  uniq++;
  if (seen.has(t)) continue;
  const f = fired(grid, a.path, 'all');
  if (!f || !allFired(f)) continue;
  seen.add(t);
  out.push({ grid, clear: 'all', best: a.moves, sol: a.path, states: a.states,
             mix: MIX.join('+'), W: b.W, H: b.H, ...f });
  fs.writeFileSync(OUT, JSON.stringify(out, null, 1) + '\n');   // 🔴 한 판마다 저장
  console.log('  ★ ' + out.length + '판째 · ' + grid[0].length + 'x' + grid.length + ' · ' + a.moves + '걸음 ·' +
              ' 미끄럼 ' + f.slid + ' 무너짐 ' + f.brk + ' 포탈 ' + f.warp + ' 상자 ' + f.box +
              ' · ' + ((Date.now() - t0) / 60000).toFixed(1) + '분');
}
console.log('\n지어봄 ' + tried + ' · 둘 다 든 판 ' + both + ' · 최단유일 ' + uniq + ' · **받은 판 ' + out.length + '** → ' + OUT);
