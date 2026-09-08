// 🔴 **굴 하나를 짓는다** (2026-09-05).
//
// 사장님과 정한 구성 (40판):
//   굴 1 홈만+덤조각 6 · 굴 2 상자 8 · 굴 3 금간 7 · 굴 4 얼음 7 · 굴 5 포탈 7 · 굴 6 피날레 5
//
// 🔴 **자리마다 조건이 다르다.** 굴의 첫 판은 "새 규칙 하나만" 이어야 하고
//    (사장님: *"처음부터 모든 기믹이 다 있을 필욘 없잖아"*), 뒤로 갈수록 조인다.
//    그래서 판을 점수순으로 뽑지 않고 **자리마다 따로** 뽑는다.
//
// ⚠️ 어렵다/쉽다는 안 잰다. 여덟 번 틀렸다 — 사장님이 해보고 판단하신다.
//    여기서 거르는 건 **객관적인 것만**: 풀린다 · 최단해 하나 · 기믹이 실제로 발동 · 안 겹침.
//
//   node tools/chapter.js 1              보여만 준다
//   WRITE=1 node tools/chapter.js 1      판 자료 앞에 넣는다
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { putStar, cutOf } = require('./star.js');
const { rng } = require('./grow.js');

const LP = path.join(__dirname, '../game/Assets/Resources/levels.json');
const CAP = +(process.env.CAP7 || 700000);
const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };
const solve = (g, o) => E.solve({ grid: g, gravity: true, clear: 'all', id: 'c' },
                                Object.assign({ cap: CAP }, o || {}));

//  못은 여기서 다 긁는다 (같은 판이 여러 못에 들어 있다)
const POOLS = ['brutal-pool', 'hard-pool', 'make40-pool', 'measured-pool', 'pool',
               'trap-pool', 'ice-pool', 'crack-pool', 'final-pool', 'box-pool'];

/// 자리마다의 조건. `want` 는 판 하나를 받아 참/거짓.
const PLAN = {
  //  🔴 굴 1 — 기믹 없이 홈만. 처음 둘은 **짧게** (스토어에서 처음 여는 사람은 2분 안에 정한다).
  //     셋째부터 **덤 조각**으로 조인다 — 새 규칙 없이 어렵게 만들 수 있는 유일한 것이다.
  1: [
    { name: '홈 하나 · 짧게',      want: v => v.gim === 'none' && v.doors === 1 && v.best <= 10 },
    { name: '홈 하나 · 짧게',      want: v => v.gim === 'none' && v.doors === 1 && v.best <= 14 },
    { name: '홈 둘 · 덤 조각',     want: v => v.gim === 'none' && v.doors === 2 && v.spare >= 1 },
    { name: '홈 둘 · 덤 조각 2↑',  want: v => v.gim === 'none' && v.doors === 2 && v.spare >= 2 },
    { name: '홈 셋 · 덤 조각',     want: v => v.gim === 'none' && v.doors === 3 && v.spare >= 1 },
    { name: '홈 셋 · 덤 조각 3↑',  want: v => v.gim === 'none' && v.doors === 3 && v.spare >= 3 },
  ],
  //  🔴 굴 2 — 미는 상자. 첫 판은 **상자만** 있어야 규칙이 읽힌다.
  //     ⚠️ 지금 못에 있는 상자 판은 전부 '발판' 식이다. 사장님이 물리셨던 그 쓰임이라
  //        (「4판 중 1판만 괜찮았어」) **먼저 판단을 받아본다** — 지루하다 하시면
  //        생성기에 「떨어뜨리기」를 넣어 다시 찍는다.
  //  🔴 새 BOX 띠로 다시 찍은 뒤 (120판 · 13x10~17x11 · 걸음 중앙값 29).
  //     옛 못은 9x6 에 13~17걸음뿐이라, 그걸로 판단받으면 "상자가 지루하다"가 아니라
  //     "판이 작다"를 판단받게 된다.
  2: [
    { name: '상자 · 배우는 판',   want: v => v.gim === 'box' && v.doors === 2 && v.best <= 24 },
    { name: '상자 · 홈 둘',       want: v => v.gim === 'box' && v.doors === 2 && v.best >= 25 },
    { name: '상자 · 홈 둘 · 덤',  want: v => v.gim === 'box' && v.doors === 2 && v.spare >= 1 },
    { name: '상자 · 홈 셋',       want: v => v.gim === 'box' && v.doors === 3 },
    { name: '상자 · 홈 셋 · 길게', want: v => v.gim === 'box' && v.doors === 3 && v.best >= 30 },
    { name: '상자 · 홈 셋 · 덤',  want: v => v.gim === 'box' && v.doors === 3 && v.spare >= 1 },
  ],
  //  🔴 굴 6 — 피날레. 기믹 넷이 한 판에 (사장님: *"최대한 많이 섞이고 큰 맵. 정말 어렵게."*)
  //     ⚠️ 이 판들은 tools/finale.js 로만 나온다. 씨앗 714개에 12판이었다.
  6: [
    //  🔴 **빡빡한 자리를 먼저** 놓는다. 널널한 자리가 먼저 오면 그 자리가
    //     제일 긴 판을 집어가서, 정작 뒤의 빡빡한 자리에 쓸 게 없어진다.
    { name: '피날레 · 제일 길게',   want: v => v.gim === 'ice+crack+box+portal' && v.best >= 38 },
    { name: '피날레 · 길게',       want: v => v.gim === 'ice+crack+box+portal' && v.best >= 32 },
    { name: '피날레 · 넷 다',      want: v => v.gim === 'ice+crack+box+portal' && v.best >= 30 },
    { name: '피날레 · 넷 다',      want: v => v.gim === 'ice+crack+box+portal' && v.best >= 27 },
    { name: '피날레 · 넷 다',      want: v => v.gim === 'ice+crack+box+portal' && v.best >= 24 },
  ],
  //  🔴 굴 3 — 금 간 바닥. 지나온 길을 두 번 못 쓴다.
  3: [
    { name: '금간 · 배우는 판',   want: v => v.gim === 'crack' && v.doors === 2 && v.best <= 24 },
    { name: '금간 · 많이 무너짐', want: v => v.gim === 'crack' && v.broke >= 3 },
    { name: '금간 · 홈 셋',       want: v => v.gim === 'crack' && v.doors === 3 },
    { name: '금간 · 덤 조각',     want: v => v.gim === 'crack' && v.spare >= 1 },
    { name: '금간 · 길게',        want: v => v.gim === 'crack' && v.best >= 30 },
  ],
  //  🔴 굴 5 — 포탈. 홈을 채워야 켜지므로 홈이 둘 이상이어야 한다.
  //     사장님이 **재미의 원인**으로 짚으신 기믹이다 (09-05).
  5: [
    { name: '포탈 · 짧게',        want: v => v.gim === 'portal' && v.best <= 24 },
    { name: '포탈 · 홈 둘',       want: v => v.gim === 'portal' && v.doors === 2 },
    { name: '포탈 · 홈 둘',       want: v => v.gim === 'portal' && v.doors === 2 },
    { name: '포탈 · 홈 셋',       want: v => v.gim === 'portal' && v.doors === 3 },
    { name: '포탈 · 홈 셋',       want: v => v.gim === 'portal' && v.doors === 3 },
    { name: '포탈 · 덤 조각',     want: v => v.gim === 'portal' && v.spare >= 1 },
  ],
};

//  🔴 판이 너무 크면 카메라가 그 판에 맞춰져 **나머지가 다 쪼그라든다.**
//     굴 나누기가 아직 없으니 검수 묶음은 크기를 묶어둔다.
const MAXW = +(process.env.MAXW || 99);
const MAXH = +(process.env.MAXH || 99);

/// 판 하나를 재본다 — **객관적인 것만.** 정답이 지금 규칙으로 통하는지가 제일 중요하다.
function look(p) {
  let L;
  try { L = E.parse({ grid: p.grid, gravity: true, clear: 'all', id: 'v' }); } catch (e) { return null; }
  let st = E.startState(L), slid = 0;
  for (const ch of p.sol || '') {
    const d = SYM[ch]; if (d === undefined) continue;
    const n = E.step(L, st, d); if (!n) return null;
    if ((n._n || 1) >= 2) slid++;
    st = n;
  }
  if (!E.isWin(L, st, false)) return null;
  let broke = 0;
  for (let k = 0; k < L.crack.length; k++) if ((st.cm || 0) & (1 << k)) broke++;
  const t = p.grid.join('');
  const ice = t.includes('I'), cr = t.includes('X'), po = /[PQpq]/.test(t), bx = t.includes('B');
  if (t.includes(':')) return null;                       // 무중력은 안 쓴다
  //  🔴 문턱은 **1**이다. 2로 뒀더니 피날레 판이 통째로 걸렸다 —
  //     큰 판에서는 미끄러짐이 한두 번뿐이다. "장식인가 아닌가"만 여기서 보고,
  //     더 필요한 자리는 아래 자리 조건(want)에서 따로 요구한다.
  if (ice && slid < 1) return null;                       // 깔려만 있는 얼음
  if (cr && broke < 2) return null;                       // 안 무너지는 금 간 바닥
  const need = L.doors.reduce((s, d) => s + d.cells.length - 1, 0);
  const g = [];
  if (ice) g.push('ice'); if (cr) g.push('crack'); if (bx) g.push('box'); if (po) g.push('portal');
  let open = 0;
  for (let y = 0; y < L.h; y++) for (let x = 0; x < L.w; x++) if (L.g[y][x] !== '#') open++;
  return { p, doors: L.doors.length, big: Math.max(...L.doors.map(d => d.cells.length)),
           spare: L.foods.length - need, best: p.best, open, slid, broke,
           W: p.grid[0].length, H: p.grid.length, gim: g.join('+') || 'none' };
}

const ch = +(process.argv[2] || 1);
//  🔴 ONLY=n — 앞에서 n 자리만 쓴다. 굴을 **모자란 만큼만 채울** 때 쓴다.
const ONLY = +(process.env.ONLY || 0);
const plan = ONLY ? (PLAN[ch] || []).slice(0, ONLY) : PLAN[ch];
if (!plan) { console.log('굴 ' + ch + ' 의 자리 조건이 아직 없다'); process.exit(1); }

const d = JSON.parse(fs.readFileSync(LP, 'utf8'));
//  🔴 **별을 떼고 견준다** (09-08 사장님: "1-7 1-9 1-11 중복").
//     판 자료의 격자에는 별('o')이 박혀 있고 못의 격자에는 없다. 그대로 견주면
//     글자가 달라 **늘 "안 쓴 판"** 이 되어, 중복 방지가 통째로 안 먹었다.
//     같은 판이 세 번까지 들어갔다.
const bare = g => g.join('').replace(/o/g, '.');
const used = new Set(d.levels.map(l => bare(l.grid)));

const all = [], seen = new Set();
for (const f of POOLS) {
  let pool;
  try { pool = JSON.parse(fs.readFileSync(path.join(__dirname, f + '.json'), 'utf8')); } catch (e) { continue; }
  if (!Array.isArray(pool)) continue;
  for (const p of pool) {
    if (!p || !p.grid || !p.sol) continue;
    const key = bare(p.grid);
    if (seen.has(key) || used.has(key)) continue;
    seen.add(key);
    const v = look(p);
    if (v && v.W <= MAXW && v.H <= MAXH) all.push(v);
  }
}
console.log('쓸 수 있는 판 ' + all.length + '개');

//  🔴 자리마다 조건에 맞는 것 중 **판 크기가 서로 비슷한 것**을 고른다.
//     카메라가 굴 안에서 제일 큰 판에 맞춰지므로, 하나만 크면 나머지가 다 쪼그라든다.
const r = rng(ch * 7919 + 41);
const picks = [];
const taken = new Set();
for (const slot of plan) {
  const fit = all.filter((v, i) => !taken.has(i) && slot.want(v))
                 .sort(() => r() - 0.5);
  if (!fit.length) { console.log('⚠️ ' + slot.name + ' — 맞는 판이 없다'); picks.push(null); continue; }
  //  이미 고른 것들과 크기가 가까운 것을 고른다
  let best = fit[0];
  if (picks.some(Boolean)) {
    const mw = Math.max(...picks.filter(Boolean).map(x => x.W));
    const mh = Math.max(...picks.filter(Boolean).map(x => x.H));
    best = fit.reduce((a, b) => {
      const da = Math.abs(a.W - mw) + Math.abs(a.H - mh);
      const db = Math.abs(b.W - mw) + Math.abs(b.H - mh);
      return db < da ? b : a;
    });
  }
  taken.add(all.indexOf(best));
  picks.push(best);
}

console.log('\n자리                크기     걸음  홈 큰홈 덤조각 기믹');
picks.forEach((v, k) => console.log('  ' + plan[k].name.padEnd(18) +
  (v ? (v.W + 'x' + v.H).padEnd(8) + String(v.best).padStart(4) + String(v.doors).padStart(4) +
       String(v.big).padStart(5) + String(v.spare).padStart(6) + '  ' + v.gim
     : '— 없음')));

if (process.env.WRITE !== '1') { console.log('\n(보여주기만 했다. 쓰려면 WRITE=1)'); process.exit(0); }

console.log('\n별을 박는다…');
fs.writeFileSync(path.join(__dirname, 'levels.bak.json'), fs.readFileSync(LP));
const made = [];
for (let k = 0; k < picks.length; k++) {
  const v = picks[k]; if (!v) continue;
  const st = putStar(v.p.grid, v.p.best, rng(k * 7919 + ch * 131), 24, 10,
                     Math.min(CAP, (v.p.states || 200000) * 3 + 60000));
  let grid = st ? st.grid : v.p.grid;
  let a = solve(grid);
  //  🔴 **별 때문에 멀쩡한 판을 버리지 않는다.** 별을 박으면 길이 막히는 판이 있는데,
  //     그때는 별 없이 쓴다 — 별은 덤이고 판이 본체다 (별이 없어도 깰 수 있다).
  if (!a.ok && st) { grid = v.p.grid; a = solve(grid); }
  const s = solve(grid, { needStar: true });
  if (!a.ok) { console.log('⚠️ ' + (k + 1) + '번 — 별 없이도 안 풀린다. 건너뛴다'); continue; }
  made.push({
    id: 'tmp' + k, chapter: ch, grid, clear: 'all',
    best: a.moves, sol: a.path,
    bestStar: s.ok ? s.moves : a.moves, cut: cutOf(s.ok ? s.moves : a.moves),
    lost: 0, tight: 0, backtrack: 0, states: a.states,
    press: -1, traps: 0, gim: v.gim, review: true,
  });
}

//  🔴 튜토리얼 바로 뒤에 넣는다. 뒤 판들은 **내용 그대로 번호만 밀린다.**
//     AT=end 면 맨 뒤에 붙인다 — 사장님이 이미 판단하신 판을 안 건드리려고.
const tut = d.levels.filter(l => l.tutorial || l.demo || l.id === '1-0');
const rest = d.levels.filter(l => !tut.includes(l));
const out = process.env.AT === 'end' ? tut.concat(rest, made) : tut.concat(made, rest);
let n = 0;
for (const l of out) { if (tut.includes(l)) continue; l.id = '1-' + (++n); }
d.levels = out;
fs.writeFileSync(LP, JSON.stringify(d, null, 2) + '\n');
console.log('굴 ' + ch + ' · ' + made.length + '판을 앞에 넣었다 (판 자료 ' + out.length + '판)');
console.log('⚠️ 옛 자료는 tools/levels.bak.json 에 있다');
