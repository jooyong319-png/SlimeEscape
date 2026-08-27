// 🔬 실험: "몇 걸음마다 한 겹 줄어드는가"를 바꾸면 판이 어떻게 달라지나.
//
// 문제의식: 지금은 매 걸음 −1이라 먹이를 두 칸에 하나씩 깔아야 한다.
//           그러면 판이 먹이로 포장된 복도가 되고, Snakebird 같은 '빈 공간의 퍼즐'이 안 나온다.
//
//   node tools/exp-shrink.js
const E = require('./engine.js').SlimeEngine;

function maxSizeAlong(def, path) {
  const L = E.parse(def);
  let st = E.startState(L);
  if (!st) return Infinity;
  let mx = st.n;
  for (const c of path) {
    st = E.move(L, st, c === '→' ? 1 : -1);
    if (!st) return Infinity;
    mx = Math.max(mx, st.n);
  }
  return mx;
}

const CASES = [
  {
    name: '① 먹이가 드문 평지 (빈 공간이 있다)',
    startSize: 3,
    grid: [
      '####################',
      '#..................#',
      '#..................#',
      '#..................#',
      '#..................#',
      '#..................#',
      '#S....o.....o.....E#',
      '####################',
    ],
  },
  {
    name: '② 층이 있는 판 (오르고 떨어진다)',
    startSize: 3,
    grid: [
      '####################',
      '#..................#',
      '#..................#',
      '#..................#',
      '#..........o......E#',
      '#.........##########',
      '#....o.............#',
      '#...#####..........#',
      '#S.................#',
      '####################',
    ],
  },
  {
    name: '③ 되돌아가야 하는 판 (내려가는 길이 왼쪽에만)',
    startSize: 3,
    grid: [
      '####################',
      '#..................#',
      '#..................#',
      '#..o......S........#',
      '####.###############',
      '#..................#',
      '#..................#',
      '#.......o.........E#',
      '####################',
    ],
  },
];

console.log('shrinkEvery = 몇 걸음마다 한 겹 줄어드는가 (1 = 지금 규칙)\n');
for (const c of CASES) {
  console.log('='.repeat(66));
  console.log(c.name);
  c.grid.forEach(r => console.log('   ' + r));
  const foods = c.grid.join('').split('o').length - 1;
  console.log(`   먹이 ${foods}개 · 가로 ${c.grid[0].length}칸`);
  for (const every of [1, 2, 3, 4]) {
    const def = { ...c, shrinkEvery: every };
    const r = E.solve(def);
    if (!r.ok) { console.log(`   every ${every} → ${r.why}`); continue; }
    const back = (r.path.match(/←/g) || []).length;
    console.log(
      `   every ${every} → ${String(r.moves).padStart(2)}걸음 · 되돌아가기 ${back} · ` +
      `최단해 ${r.shortest}개 · 최대크기 ${maxSizeAlong(def, r.path)} · ${r.path}`);
  }
  console.log();
}
