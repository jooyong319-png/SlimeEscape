// 🔬 첫 판 후보들을 돌려서 쓸 만한 걸 고른다.
//   node tools/level1.js          요약
//   node tools/level1.js <번호>   그 판의 수순을 그림으로 재생
//
// 기호: # 벽 · . 빈칸 · S 시작 · + 조각 · = 채워야 할 칸 · * 심(머리가 마지막에)
// 규칙상 조각 수 + 1 == 목표 칸 수 여야 한다.
const E3 = require('./engine.js').SlimeEngine;

/// 막다른 상태 비율 — 설계 연구 6회차에서 추가하기로 한 지표.
/// 도달 가능한 상태 중 "더 못 움직이는" 것의 비율. 높으면 자주 막힌다 = 어렵다.
function deadEndRatio(def) {
  const L = E3.parse(def);
  const seen = new Map([[E3.keyOf(E3.startState(L)), E3.startState(L)]]);
  const q = [E3.startState(L)];
  let head = 0, dead = 0;
  while (head < q.length) {
    const st = q[head++];
    let moves = 0;
    for (let d = 0; d < 4; d++) {
      const ns = E3.step(L, st, d);
      if (!ns) continue;
      moves++;
      const k = E3.keyOf(ns);
      if (!seen.has(k) && !E3.isWin(L, ns)) { seen.set(k, ns); q.push(ns); }
    }
    if (moves === 0) dead++;
  }
  return { states: seen.size, dead, ratio: seen.size ? dead / seen.size : 0 };
}

const W = 20, H = 12;
function blank() {
  const g = [];
  for (let y = 0; y < H; y++)
    g.push(Array.from({ length: W }, (_, x) =>
      (x === 0 || y === 0 || x === W - 1 || y === H - 1) ? '#' : '.'));
  return g;
}
const box = (g, x0, y0, x1, y1, c) => { for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) g[y][x] = c; };
const S = g => g.map(r => r.join(''));

const CASES = [
  {
    name: 'A · 넓은 방 · 목표 2×3 · 조각 5',
    build: () => {
      const g = blank();
      box(g, 15, 8, 17, 9, '=');  g[8][17] = '*';
      g[9][3] = 'S';
      [[5,3],[8,6],[11,3],[13,8],[6,9]].forEach(([x,y]) => g[y][x] = '+');
      return S(g);
    },
  },
  {
    name: 'B · 기둥으로 길을 좁힘 · 목표 2×3 · 조각 5',
    build: () => {
      const g = blank();
      box(g, 6, 3, 6, 7, '#'); box(g, 12, 4, 12, 8, '#');
      box(g, 15, 8, 17, 9, '=');  g[8][17] = '*';
      g[9][2] = 'S';
      [[4,3],[9,2],[9,8],[14,3],[17,5]].forEach(([x,y]) => g[y][x] = '+');
      return S(g);
    },
  },
  {
    name: 'C · 목표가 벽에 둘러싸인 방 안 · 조각 5',
    build: () => {
      const g = blank();
      box(g, 13, 6, 18, 6, '#'); box(g, 13, 7, 13, 10, '#');
      g[7][14] = '.';                       // 입구 하나
      box(g, 15, 8, 17, 9, '=');  g[9][15] = '*';
      g[3][2] = 'S';
      [[5,3],[8,5],[4,8],[10,9],[11,3]].forEach(([x,y]) => g[y][x] = '+');
      return S(g);
    },
  },
  {
    name: 'D · 좁은 통로 + 목표 2×3 · 조각 5',
    build: () => {
      const g = blank();
      box(g, 2, 6, 9, 6, '#'); box(g, 11, 3, 11, 9, '#');
      g[6][5] = '.';                        // 아래위를 잇는 구멍
      box(g, 15, 8, 17, 9, '=');  g[8][15] = '*';
      g[2][2] = 'S';
      [[4,2],[8,3],[3,9],[8,9],[14,5]].forEach(([x,y]) => g[y][x] = '+');
      return S(g);
    },
  },
  {
    name: 'E · L자 목표 6칸 · 조각 5',
    build: () => {
      const g = blank();
      box(g, 15, 7, 15, 9, '='); box(g, 16, 9, 17, 9, '=');  g[7][15] = '*';
      box(g, 7, 2, 7, 8, '#');
      g[5][7] = '.';
      g[9][2] = 'S';
      [[4,3],[3,7],[9,4],[12,8],[13,3]].forEach(([x,y]) => g[y][x] = '+');
      return S(g);
    },
  },
];

const pick = process.argv[2] ? Number(process.argv[2]) - 1 : -1;

CASES.forEach((c, i) => {
  const grid = c.build();
  console.log('='.repeat(70));
  console.log(`${i + 1}. ${c.name}`);
  if (pick < 0) grid.forEach(r => console.log('   ' + r));

  const r = E3.solve({ grid });
  if (!r.ok) { console.log(`   ❌ ${r.why}${r.states ? ` (탐색 ${r.states})` : ''}`); return; }

  const de = deadEndRatio({ grid });
  const uniq = r.shortest === 1;
  console.log(
    `   ${uniq ? '✅' : '⚠️ '} 최단 ${r.moves}걸음 · 최단해 ${r.shortest}개 · ` +
    `탐색 ${r.states} · 막다른 상태 ${de.dead}/${de.states} (${(de.ratio * 100).toFixed(1)}%)`);
  console.log(`   ${r.path}`);

  if (pick === i) {
    const t = E3.trace({ grid });
    t.steps.forEach((s, k) => {
      console.log(`\n   ${k === 0 ? '시작' : s.sym}  길이 ${s.st.body.length}`);
      E3.render(t.L, s.st).forEach(row => console.log('   ' + row));
    });
  }
});
