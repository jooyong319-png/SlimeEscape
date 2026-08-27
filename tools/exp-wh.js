// 🔬 새 규칙 시험 — 이동은 공짜, 몸은 판 위의 것으로만 변한다.
//   ^ 세로+1   > 가로+1   v 세로−1   < 가로−1
//
//   node tools/exp-wh.js            요약
//   node tools/exp-wh.js <번호>      그 판의 수순을 그림으로 재생
const E2 = require('./engine2.js').SlimeEngine2;

const W = 20, H = 12;
function blank() {
  const g = [];
  for (let y = 0; y < H; y++)
    g.push(Array.from({ length: W }, (_, x) =>
      (x === 0 || y === 0 || x === W - 1 || y === H - 1) ? '#' : '.'));
  return g;
}
const fill = (g, x0, y0, x1, y1, c) => { for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) g[y][x] = c; };
const S = g => g.map(r => r.join(''));

const CASES = [
  {
    name: '① 빈 들판 — 이동이 공짜면 그냥 간다',
    build: () => { const g = blank(); g[10][1] = 'S'; g[10][W - 2] = 'E'; return S(g); },
  },
  {
    name: '② 높은 턱 — 세로로 커져야 오른다',
    build: () => {
      const g = blank();
      fill(g, 12, 8, W - 2, H - 2, '#');       // 3칸 턱 (윗면 7행)
      g[10][1] = 'S'; g[10][4] = '^'; g[10][7] = '^';
      g[7][W - 2] = 'E';
      return S(g);
    },
  },
  {
    name: '③ 낮은 천장 — 세로를 도로 줄여야 지난다',
    build: () => {
      const g = blank();
      fill(g, 10, 1, 13, 9, '#');              // 10행만 열린 좁은 통로
      g[10][1] = 'S'; g[10][3] = '^'; g[10][5] = '^';   // 일부러 키워 두고
      g[10][8] = 'v'; g[10][7] = 'v';                    // 다시 줄여야 통과
      g[10][W - 2] = 'E';
      return S(g);
    },
  },
  {
    name: '④ 되돌아가기 — 오른쪽 것을 먼저 먹고 와야 왼쪽 턱을 오른다',
    build: () => {
      const g = blank();
      fill(g, 1, 8, 6, H - 2, '#');            // 왼쪽 3칸 턱 (윗면 7행)
      g[7][1] = 'E';
      g[10][9] = 'S'; g[10][12] = '^'; g[10][14] = '^';
      return S(g);
    },
  },
  {
    name: '⑤ 순서가 중요 — 넓어지면 통로를 못 지난다',
    build: () => {
      const g = blank();
      fill(g, 8, 1, 8, 9, '#'); fill(g, 11, 1, 11, 9, '#');  // 폭 2짜리 문 두 개
      g[10][1] = 'S'; g[10][3] = '>'; g[10][5] = '>';
      g[10][9] = '<';
      g[10][W - 2] = 'E';
      return S(g);
    },
  },
];

const pick = process.argv[2] ? Number(process.argv[2]) - 1 : -1;

CASES.forEach((c, i) => {
  const grid = c.build();
  console.log('='.repeat(66));
  console.log(`${i + 1}. ${c.name}`);
  if (pick < 0) grid.forEach(r => console.log('   ' + r));

  const r = E2.solve({ grid });
  if (!r.ok) { console.log(`   ❌ ${r.why} (탐색 ${r.states || 0})`); return; }
  const back = (r.path.match(/←/g) || []).length;
  console.log(`   ${back ? '✅' : '🟡'} ${r.moves}걸음 · 되돌아가기 ${back} · ` +
              `최단해 ${r.shortest}개 · 탐색 ${r.states}`);
  console.log(`   ${r.path}`);

  if (pick === i) {
    const t = E2.trace({ grid });
    for (const s of t.steps) {
      console.log(`\n   ${s.sym}  몸 ${s.st.w}x${s.st.h}`);
      E2.render(t.L, s.st).forEach(row => console.log('   ' + row));
    }
  }
});
