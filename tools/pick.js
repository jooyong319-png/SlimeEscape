// 🔴 **어려움으로 고른다** (2026-09-04, 09-05 다시 씀).
//
// 지금까지는 `best`(걸음 수) 하나로 골랐다 — 난이도 곡선이 사실 **길이 곡선**이었다.
// 이제 **압박(press)** 을 함께 본다. 압박은 "갈림길에서 지는 쪽이 몇 할이냐" 다.
//
//   node tools/pick.js                   골라서 보여만 준다
//   WRITE=1 node tools/pick.js           levels.json 에 쓴다 (사본 먼저 뜬다)
//
//   POOL=... STAT=... N=47 로 바꿀 수 있다.
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { rng } = require('./grow.js');
const { putStar, cutOf } = require('./star.js');

const POOL = process.env.POOL || 'tools/trap-pool.json';
const STAT = process.env.STAT || 'tools/trapstat-after.json';
const LP   = path.join(__dirname, '../game/Assets/Resources/levels.json');
const BAK  = path.join(__dirname, 'levels.bak.json');
const N    = +(process.env.N || 47);     // 튜토리얼 뒤로 몇 판

const pool = JSON.parse(fs.readFileSync(POOL, 'utf8'));
const stat = JSON.parse(fs.readFileSync(STAT, 'utf8'));
console.log('못', pool.length, '· 잰 판', stat.length);

// 잰 값과 판을 붙인다. **못 잰 판은 버린다** — 못 잰 걸 곡선에 끼우면
// 압박을 0 으로 보고 앞쪽에 몰아넣게 된다 (그게 09-04 의 '길이 곡선' 이었다).
//  🔴 **못 잰 판도 쓴다** (09-05 밤). 재는 게 비싼 것과 판이 안 좋은 것은 다르다 —
//     상태가 커서 못 잰 판이 곧 **긴 판**이라, 버리면 뒤쪽이 또 짧아진다.
//     실제로 35걸음 넘는 판 47개 중 35개가 '못 잰 판' 이었다.
//     못 잰 판은 압박을 **모른다**(null)고 두고, 길이로 고른다.
const byIdx = new Map(stat.map(r => [r.i, r]));
const rows = pool.map((p, i) => {
  const s = byIdx.get(i);
  return Object.assign({}, s || {}, p, {
    moves: p.best,
    //  크기는 격자에서 바로 읽는다 — 잰 값에만 있으면 못 잰 판이 undefined 로 찍힌다
    w: p.grid[0].length, h: p.grid.length,
    doors: p.doors,
    press: s ? s.press : null,
    back: s ? s.back : 0,
    tight: s ? s.tight : 0,
    states: s ? s.states : 0,
  });
}).filter(r => r.grid && r.moves >= 8);
console.log('그중 압박을 못 잰 판 ' + rows.filter(r => r.press === null).length + '개 — 길이로만 본다');
//  🔴 **기믹이 정말 쓰이는지 여기서 다시 본다** (09-05 밤).
//     못을 만들 때 한 번 걸렀는데, 그 뒤 **덤 조각을 얹으면서 정답이 바뀌어**
//     기믹이 안 쓰이게 된 판이 생겼다. 하필 그게 포탈 첫 등장 판이었다 —
//     처음 만나는 기믹이 아무 일도 안 하면 그건 없느니만 못하다.
{
  const before = rows.length;
  for (let i = rows.length - 1; i >= 0; i--) {
    const r = rows[i];
    if ((r.gim || 'none') === 'none') continue;
    const a = E.solve({ grid: r.grid, gravity: true, clear: 'all', id: 'u' }, { cap: 400000 });
    const b = E.solve({ grid: r.grid.map(x => x.replace(/[:BPQpq]/g, '.')),
                        gravity: true, clear: 'all', id: 'u' }, { cap: 400000 });
    //  🔴 **포탈은 더 엄하게 본다** (09-05 사장님: "포털이 꼭 필요하게 써야한다").
    //     걸어서도 갈 수 있으면 포탈이 거기 있을 이유가 없다 — 지름길은 기믹이 아니다.
    //     상자·무중력은 지름길이어도 손으로 쓰는 것이라 그대로 둔다.
    const strict = (r.gim || 'none') === 'portal';
    const useless = strict ? (b.ok) : (a.ok && b.ok && b.moves === a.moves);
    if (useless) rows.splice(i, 1);
  }
  console.log('기믹이 장식뿐인 판 ' + (before - rows.length) + '개를 뺐다 (포탈은 없으면 못 푸는 것만)');
}
console.log('쓸 수 있는 판', rows.length);
if (rows.length < N) console.log('⚠️ ' + N + '판을 채우기엔 모자란다 — 있는 만큼만 고른다');

// ---- 목표 곡선 ----
// 🔴 **곡선은 여전히 있어야 한다.** 1-1 이 어려우면 아무도 2판까지 못 간다.
//    다만 목표는 **못에 실제로 있는 것**으로 잡는다. 09-05 에 걸음 55 를 좇았는데
//    걸음 30 넘는 판이 여덟 개뿐이라, 애꿎은 압박을 버려가며 긴 판을 주웠다.
const want = [];
for (let k = 0; k < N; k++) {
  const t = k / (N - 1);                       // 0 → 1
  want.push({
    //  🔴 **앞은 짧게, 뒤는 훨씬 길게** (09-05 사장님).
    //     옛 16판(사장님이 "어렵다" 하신 것)이 최소 20 · 중앙 34 · 최대 60 이었다.
    //     어제 것은 최소 8 · 중앙 18 이라 판당 2.5분이 1.0분으로 줄었다 — 그게 "쉬워졌다" 였다.
    //     앞은 배우는 자리라 짧게 두되, 뒤는 못에 있는 만큼(최대 55) 끝까지 쓴다.
    press: 0.04 + 0.36 * t * t,                // 4% → 40%
    moves: 8 + 42 * t * (0.55 + 0.45 * t),     // 8 → 50 (뒤에서 가파르게)
    doors: t < 0.22 ? 1 : t < 0.55 ? 2 : 3,    // 앞은 홈 하나부터
  });
}

const used = new Set();
const picked = [];
//  홈 개수는 난이도가 아니지만 **보기에는** 다르다. 안 밀면 마흔일곱 판 중 서른셋이
//  홈 둘이 된다. 못에 있는 만큼만 부드럽게 민다 (넘칠 때만, 아주 약하게).
const quota = { 1: 14, 2: 21, 3: 12 };
const mixCount = { 1: 0, 2: 0, 3: 0 };
//  🔴 기믹은 **골고루, 너무 많지 않게** (09-05 사장님: "적절히 배치해줘 너무 다 쓰지말고").
//     47판 중 기믹 판은 열두 개쯤. 나머지는 맨판이라 기믹이 '특별한 판'으로 읽힌다.
const gimQuota = { none: 35, zone: 4, box: 4, portal: 4 };
const gimCount = { none: 0, zone: 0, box: 0, portal: 0 };
for (const w of want) {
  let best = null, bestD = Infinity;
  for (let i = 0; i < rows.length; i++) {
    if (used.has(i)) continue;
    const r = rows[i];
    const dr = r.doors || 1;
    const over = Math.max(0, mixCount[dr] - quota[dr]) * 0.06;
    const gm = r.gim || 'none';
    //  정해둔 수를 넘으면 세게 민다 — 기믹이 판을 뒤덮으면 안 된다
    const gOver = gimCount[gm] >= (gimQuota[gm] || 0) ? 3.0 : 0;
    //  홈은 난이도가 아니라 **보기의 결**이다 — 부드럽게 민다 (09-05 측정:
    //  홈 1/2/3 압박 중앙값이 14.3/13.9/14.1% 로 같았다). 다만 첫 판은 단순해야 한다.
    //  앞쪽에서는 홈 개수를 더 세게 본다 — 첫 판이 홈 둘이면 시작이 무겁다
    const doorW = (picked.length / N) < 0.25 ? 1.6 : 0.3;
    const d = Math.abs(dr - w.doors) * doorW + over + gOver
            //  압박을 모르는 판은 **중간쯤 페널티**를 준다 — 아는 판이 같은 조건이면 이기되,
            //  길이가 훨씬 잘 맞으면 모르는 판도 뽑히게 (길이 20 차이 = 0.9 이다)
            + (r.press === null ? 0.35 : Math.abs(r.press - w.press) * 4.0)
            //  🔴 걸음 무게를 60 으로 나눴더니 **압박이 다 이겨서** 8걸음짜리가 여덟 판
            //     뽑혔다 (뒤쪽에 셋이 연달아 왔다). 09-05 에 "길이 곡선"을 버렸다고
            //     이번엔 길이를 아예 안 본 것이다 — 둘 다 봐야 한다.
            + Math.abs(r.moves - w.moves) / 22
            //  🔴 뒤로 갈수록 **너무 짧은 판은 안 받는다.** 짧고 조이는 판은 쉼표로 쓸 만하지만
            //     연달아 나오면 "쉬워졌다"로 읽힌다.
            //  🔴 뒤로 갈수록 **너무 짧은 판은 안 받는다.** 문턴이 자리에 따라 올라간다 —
            //     앞 30% 는 그대로 두고, 그 뒤로는 12 → 22 걸음까지 올린다.
            + (r.moves < 12 + Math.max(0, (picked.length / N - 0.3) / 0.7) * 10 ? 1.5 : 0);
    if (d < bestD) { bestD = d; best = i; }
  }
  if (best === null) break;
  used.add(best);
  mixCount[rows[best].doors || 1]++;
  gimCount[rows[best].gim || 'none']++;
  picked.push(rows[best]);
}

// 🔴 **줄어들면 안 된다.** 걸음만으로 세우면 또 길이 곡선이 되니
//    걸음과 압박의 **등수를 더해서** 세운다. 압박에 조금 더 무게를 준다.
{
  const rank = get => {
    const ord = picked.map((_, i) => i).sort((a, b) => get(picked[a]) - get(picked[b]));
    const out = new Array(picked.length);
    ord.forEach((idx, pos) => { out[idx] = pos; });
    return out;
  };
  //  못 잰 판은 압박 등수를 **가운데**로 놓는다 (모르는 것을 0 으로 치면 앞으로 쏠린다)
  const known = picked.filter(r => r.press !== null).map(r => r.press).sort((a, b) => a - b);
  const midPress = known.length ? known[Math.floor(known.length / 2)] : 0.1;
  const rm = rank(r => r.moves), rp = rank(r => r.press === null ? midPress : r.press);
  //  🔴 **길이에 더 무게**를 준다 (09-05 사장님: "게임이 너무 쉽다").
  //     사람이 느끼는 어려움은 길이 쪽이 컸다 — 압박은 거들게 둔다.
  const scored = picked.map((r, i) => ({ r, s: rm[i] * 1.6 + rp[i] * 0.9 }));
  scored.sort((x, y) => x.s - y.s);
  picked.length = 0;
  scored.forEach(x => picked.push(x.r));
}

// 🔴 기믹이 **처음 나오는 판을 정해진 칸에 꽂는다** (09-05).
//    점수로 밀었더니 셋이 1·2·3판에 몰렸다 — 아직 기본도 못 배운 자리다.
//      상자  6번  — 제일 알기 쉽다. 밀면 밀린다, 그게 전부다
//      무중력 11번 — 튜토리얼에서 봤지만 실제로 쓰는 건 여기가 처음
//      포탈  18번 — **튜토리얼에서 안 가르친다** (홈을 채워야 켜져서 5칸 시범에 안 들어간다).
//                   홈을 여러 번 채워본 뒤라야 "홈을 채우면 켜진다"가 읽힌다
{
  //  ⚠️ 하나만 옮기면 **같은 기믹의 다른 판이 앞에 남는다** (09-05 에 그랬다).
  //     그 기믹 판을 **전부** 데뷔 칸 뒤로 민다. 칸이 작은 것부터 처리한다.
  const SLOT = [['box', 5], ['zone', 10], ['portal', 17]];   // 0부터 세는 자리
  for (const [g, slot] of SLOT) {
    const early = [];
    for (let i = picked.length - 1; i >= 0; i--)
      if (i < slot && (picked[i].gim || 'none') === g) early.unshift(picked.splice(i, 1)[0]);
    if (early.length) picked.splice(Math.min(slot, picked.length), 0, ...early);
  }
}

console.log('\n고른 판', picked.length);
console.log('번호  홈  크기      걸음   압박    되짚기   덤   상태');
console.log('-'.repeat(58));
picked.forEach((r, k) => {
  if (k % 4 !== 0 && k !== picked.length - 1) return;   // 네 판마다 한 줄만
  console.log(String(k + 1).padStart(4) + String(r.doors).padStart(4) +
    ('  ' + r.w + 'x' + r.h).padEnd(10) +
    String(r.moves).padStart(5) +
    (r.press * 100).toFixed(1).padStart(7) + '%' +
    (r.back * 100).toFixed(0).padStart(7) + '%' +
    String(r.traps == null ? '-' : r.traps).padStart(5) +
    String(r.states).padStart(9));
});
const avg = f => picked.reduce((s, r) => s + f(r), 0) / picked.length;
const mix = [1, 2, 3].map(d => d + '홈 ' + picked.filter(r => r.doors === d).length).join(' · ');
console.log('-'.repeat(58));
{
  const k = picked.filter(r => r.press !== null);
  console.log('압박 잰 판 ' + k.length + '/' + picked.length +
              ' · 그중 평균 ' + (k.reduce((s, r) => s + r.press, 0) / Math.max(1, k.length) * 100).toFixed(1) + '%');
}
console.log('평균 압박 ' + (avg(r => r.press || 0) * 100).toFixed(1) + '%' +
            ' · 되짚기 ' + (avg(r => r.back) * 100).toFixed(0) + '%' +
            ' · 걸음 ' + avg(r => r.moves).toFixed(0) +
            ' · 덤 ' + avg(r => r.traps || 0).toFixed(1) + '개');
{
  const g = {};
  for (const r of picked) g[r.gim || 'none'] = (g[r.gim || 'none'] || 0) + 1;
  console.log('기믹 섞임: ' + Object.entries(g).map(([k, v]) => k + ' ' + v).join(' · '));
  picked.forEach((r, i) => {
    if ((r.gim || 'none') === 'none') return;
    if (picked.findIndex(x => (x.gim || 'none') === r.gim) !== i) return;
    console.log('  ' + r.gim + ' 첫 등장: ' + (i + 1) + '번 판');
  });
}
console.log('홈 섞임: ' + mix + ' · 크기 ' +
            new Set(picked.map(r => r.w + 'x' + r.h)).size + '가지');
{ // 덤 조각 전과 견줘 본다 (press0 가 판마다 붙어 있다)
  const has = picked.filter(r => typeof r.press0 === 'number');
  if (has.length)
    console.log('덤 조각 전 평균 압박 ' + (has.reduce((s, r) => s + r.press0, 0) / has.length * 100).toFixed(1) +
                '% → 지금 ' + (has.reduce((s, r) => s + r.press, 0) / has.length * 100).toFixed(1) + '%');
}

if (process.env.WRITE !== '1') {
  console.log('\n(보여주기만 했다. 쓰려면 WRITE=1)');
  process.exit(0);
}

// ---- 별·커트라인 박기 ----
console.log('\n별을 박는다…');
const out = [];
let noStar = 0, moved = 0, swapped = 0;
//  🔴 별을 못 박은 판을 그냥 버리면 **판 수가 모자라진다** (09-05 에 47 -> 45 가 됐다).
//     자리를 더 뒤져보고, 그래도 안 되면 **비슷한 판으로 갈아 끼운다.**
function stampStar(p, seed) {
  //  🔴 먼저 한 번 풀어 **그 판이 얼마나 무거운지** 본다. 별 자리 시험은 그 세 배에서 끊는다.
  //     안 끊으면 못 가는 자리 하나에 수십 초가 간다 (09-05 밤에 70분을 헛돌았다).
  const base = E.solve({ grid: p.grid, gravity: true, clear: 'all', id: 's' }, { cap: 1200000 });
  if (!base.ok) return null;
  const cap = Math.min(1200000, base.states * 3 + 50000);
  //  무거운 판은 시험 자리도 줄인다 — 어차피 한 자리만 되면 충분하다
  const heavy = base.states > 150000;
  const tries = heavy ? 12 : (p.doors >= 3 ? 28 : 40);
  return putStar(p.grid, base.moves, rng(seed), tries, p.doors >= 3 ? 5 : 8, cap);
}
for (let k = 0; k < picked.length; k++) {
  let p = picked[k];
  let st = stampStar(p, k * 7919 + 13);
  if (!st) {
    //  같은 자리의 목표에 제일 가까운, 아직 안 쓴 판으로 바꿔 본다
    const w = want[Math.min(k, want.length - 1)];
    const cand = rows
      .map((r, i) => ({ r, i }))
      .filter(x => !used.has(x.i))
      .sort((a, b) =>
        (Math.abs(a.r.press - w.press) * 4 + Math.abs(a.r.moves - w.moves) / 22) -
        (Math.abs(b.r.press - w.press) * 4 + Math.abs(b.r.moves - w.moves) / 22))
      .slice(0, 6);
    for (const c of cand) {
      const s2 = stampStar(c.r, k * 7919 + 101);
      if (!s2) continue;
      used.add(c.i); p = c.r; st = s2; swapped++; break;
    }
  }
  if (!st) { noStar++; continue; }
  // 🔴 별을 얹고 **별 없는 최단이 그대로인지** 확인한다. 안 그러면 1별 커트가 어긋난다.
  const a = E.solve({ grid: st.grid, gravity: true, clear: 'all', id: 'c' });
  if (!a.ok) { noStar++; continue; }
  if (a.moves !== p.moves) moved++;
  out.push({
    grid: st.grid, clear: 'all',
    best: a.moves, sol: a.path,
    bestStar: st.moves, cut: cutOf(st.moves),
    lost: 0, tight: p.tight, backtrack: p.back, states: p.states,
    //  -1 = 못 쟀다 (0 이 아니다 — 0 이면 '쉽다'로 잘못 읽힌다)
    press: p.press === null ? -1 : +p.press.toFixed(4), traps: p.traps || 0, gim: p.gim || 'none',
  });
}
console.log('별 박은 판 ' + out.length + ' · 갈아 끼운 판 ' + swapped + ' · 끝내 못 박은 판 ' + noStar +
            ' · 별 때문에 최단이 바뀐 판 ' + moved);

const d = JSON.parse(fs.readFileSync(LP, 'utf8'));
const tut = d.levels.find(l => l.tutorial);
if (!tut) { console.log('🔴 튜토리얼 판이 없다 — 그만둔다'); process.exit(1); }
tut.id = '1-0';
// ⚠️ 여긴 git 이 아니다. 덮어쓰기 전에 사본을 뜬다.
fs.writeFileSync(BAK, fs.readFileSync(LP));
console.log('사본 → ' + BAK);

d.levels = [tut, ...out.map((l, i) => Object.assign({ id: '1-' + (i + 1) }, l))];
d.chapter = 1;
fs.writeFileSync(LP, JSON.stringify(d, null, 2) + '\n');
console.log('판 자료에 썼다 · 판 ' + (d.levels.length - 1) + '개 (+튜토리얼)');
console.log('다음: 유니티 ConformanceCheck');
