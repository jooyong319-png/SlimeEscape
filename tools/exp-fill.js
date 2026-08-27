// 🔬 목표 = "표시된 칸을 몸으로 정확히 채운다"
//   ^ 세로+1   > 가로+1   v 세로−1   < 가로−1   = 채워야 할 칸
//
//   node tools/exp-fill.js            요약
//   node tools/exp-fill.js <번호>      그 판의 수순을 그림으로
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
const F = 10;   // 바닥 윗줄 (발이 서는 줄)

const CASES = [
  {
    name: '① 옆으로 두 칸 — 넓어져서 채운다',
    build: () => {
      const g = blank();
      g[F][1] = 'S';
      g[F][5] = '>';
      fill(g, 15, F, 16, F, '=');            // 2×1 목표
      return S(g);
    },
  },
  {
    name: '② 위로 두 칸 — 높아져서 채운다',
    build: () => {
      const g = blank();
      g[F][1] = 'S';
      g[F][5] = '^';
      fill(g, 15, F - 1, 15, F, '=');        // 1×2 목표
      return S(g);
    },
  },
  {
    name: '③ 딱 맞춰야 — 지나치면 너무 커진다',
    build: () => {
      const g = blank();
      g[F][1] = 'S';
      g[F][4] = '>'; g[F][6] = '>'; g[F][8] = '>';   // 셋 다 먹으면 4칸 — 너무 넓다
      g[F - 1][6] = '.';                              // 위로 돌아가는 길
      fill(g, 15, F, 16, F, '=');                     // 2×1 목표 = '>' 하나만 먹어야
      return S(g);
    },
  },
  {
    name: '④ 되돌아가기 — 오른쪽에서 먹고 왼쪽 목표로',
    build: () => {
      const g = blank();
      g[F][9] = 'S';
      g[F][12] = '^'; g[F][14] = '>';
      fill(g, 2, F - 1, 3, F, '=');          // 왼쪽에 2×2 목표
      return S(g);
    },
  },
  {
    name: '⑤ 키웠다가 줄인다 — 턱을 넘고 나서 납작하게',
    build: () => {
      const g = blank();
      fill(g, 8, F - 1, 11, F, '#');         // 2칸 턱
      g[F][1] = 'S'; g[F][3] = '^'; g[F][5] = '^';    // 올라가려면 세로 2 필요
      g[F - 2][13] = 'v';                              // 넘어온 뒤 줄이는 것
      fill(g, 16, F, 16, F, '=');                      // 1×1 목표
      return S(g);
    },
  },
];

const pick = process.argv[2] ? Number(process.argv[2]) - 1 : -1;
let ok = 0, withBack = 0;

CASES.forEach((c, i) => {
  const grid = c.build();
  console.log('='.repeat(66));
  console.log(`${i + 1}. ${c.name}`);
  if (pick < 0) grid.forEach(r => console.log('   ' + r));

  let r;
  try { r = E2.solve({ grid }); }
  catch (e) { console.log(`   ❌ ${e.message}`); return; }
  if (!r.ok) { console.log(`   ❌ ${r.why} (탐색 ${r.states || 0})`); return; }

  ok++;
  const back = (r.path.match(/←/g) || []).length;
  if (back) withBack++;
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

console.log('='.repeat(66));
console.log(`풀림 ${ok}/${CASES.length} · 되돌아가기 있는 판 ${withBack}개`);
