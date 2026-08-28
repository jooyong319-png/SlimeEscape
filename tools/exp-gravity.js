// 🔬 중력이 난이도를 얼마나 올리는가 — 같은 판을 켜고/끄고 비교한다.
//   지표: "이미 진 상태" 비율 = 도달 가능한 상태 중 거기서 승리에 도달할 수 없는 것
//        (막다른 상태 비율은 이 게임에서 쓸모없었다 — 빈 방이라 막힐 일이 없다)
//
//   node tools/exp-gravity.js          비교표
//   node tools/exp-gravity.js <번호> <g|n>   그 판을 그림으로 재생 (g=중력 켬)
const E3 = require('./engine.js').SlimeEngine;

function analyze(def, cap = 700000) {
  const L = E3.parse(def);
  const idOf = new Map(), states = [];
  const id = st => { const k = E3.keyOf(st); let v = idOf.get(k);
    if (v === undefined) { v = states.length; idOf.set(k, v); states.push(st); } return v; };
  id(E3.startState(L));
  const edges = [], win = [];
  for (let i = 0; i < states.length; i++) {
    if (states.length > cap) return { over: true, total: states.length };
    const st = states[i], out = [];
    if (E3.isWin(L, st)) { win.push(i); edges.push(out); continue; }
    for (let d = 0; d < 4; d++) { const ns = E3.step(L, st, d); if (ns) out.push(id(ns)); }
    edges.push(out);
  }
  const rev = states.map(() => []);
  edges.forEach((o, i) => o.forEach(j => rev[j].push(i)));
  const canWin = new Uint8Array(states.length);
  const q = [...win]; win.forEach(i => canWin[i] = 1);
  for (let h = 0; h < q.length; h++) for (const p of rev[q[h]]) if (!canWin[p]) { canWin[p] = 1; q.push(p); }
  let dead = 0; for (let i = 0; i < states.length; i++) if (!canWin[i]) dead++;
  return { total: states.length, dead, lost: dead / states.length };
}

// 발판이 있는 판들. 중력이 없으면 그냥 벽이고, 있으면 지형이 된다.
const CASES = [
  {
    name: '① 층 두 개 · 목표는 아래층 · 조각 5',
    grid: [
      '##############',
      '#............#',
      '#..+..+...+..#',
      '#............#',
      '#####...######',
      '#............#',
      '#..+.....+...#',
      '#.........==*#',
      '#.S.......===#',
      '##############',
    ],
  },
  {
    name: '② 계단식 · 목표는 맨 아래 · 조각 5',
    grid: [
      '##############',
      '#.S..........#',
      '#..+.........#',
      '####.........#',
      '#....+.......#',
      '#.######.....#',
      '#....+..+....#',
      '#.......####.#',
      '#..+......==*#',
      '#.........===#',
      '##############',
    ],
  },
  {
    name: '③ 목표가 공중 선반 위 · 조각 5',
    grid: [
      '##############',
      '#............#',
      '#..+.....==*.#',
      '#........===.#',
      '#........#####',
      '#..+..+......#',
      '#............#',
      '#####........#',
      '#.S..+...+...#',
      '##############',
    ],
  },
];

const pick = process.argv[2] ? Number(process.argv[2]) - 1 : -1;
const pickG = (process.argv[3] || 'g') === 'g';

if (pick < 0) {
  console.log('같은 판 · 중력 끔 vs 켬\n');
  console.log('  판                                  중력  풀림  최단  해수   상태수      이미 진 상태');
  CASES.forEach((c, i) => {
    for (const gravity of [false, true]) {
      const def = { grid: c.grid, gravity };
      const r = E3.solve(def);
      const label = i === 0 || gravity ? '' : '';
      let line = `  ${(gravity ? '' : (i + 1) + '. ' + c.name).padEnd(34)} ${gravity ? '켬 ' : '끔 '}  `;
      if (!r.ok) { console.log(line + `❌ ${r.why}`); continue; }
      const a = analyze(def);
      if (a.over) { console.log(line + `✅  ${String(r.moves).padStart(3)}   ${String(r.shortest).padStart(2)}   상태 70만 초과`); continue; }
      console.log(line + `✅  ${String(r.moves).padStart(3)}   ${String(r.shortest).padStart(2)} ` +
                  `${String(a.total.toLocaleString()).padStart(11)}   ${(a.lost * 100).toFixed(1)}%`);
    }
    console.log('');
  });
} else {
  const c = CASES[pick];
  const def = { grid: c.grid, gravity: pickG };
  console.log(`${pick + 1}. ${c.name}  ·  중력 ${pickG ? '켬' : '끔'}`);
  const t = E3.trace(def);
  if (!t.r.ok) { console.log('  ' + t.r.why); }
  t.steps.forEach((s, k) => {
    console.log(`\n  ${k === 0 ? '시작' : s.sym}  길이 ${s.st.body.length}`);
    E3.render(t.L, s.st).forEach(row => console.log('  ' + row));
  });
}
