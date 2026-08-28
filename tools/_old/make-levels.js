// 판을 코드로 짓는다. 손으로 그리면 칸을 틀린다 (실제로 여러 번 틀렸다).
//
//   node tools/make-levels.js          지어보고 결과만 보여준다
//   node tools/make-levels.js --write  levels.json에 써 넣는다
//
// 먹이는 "한 칸 걸러 하나"가 손익분기다(이동 −1 / 먹이 +1). 그 간격을 기준으로 두고
// 시작 위치·간격 오프셋을 바꿔가며 **솔버가 통과시키는 배치**를 찾는다.
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const MAX_SIZE = 3;     // 몸이 이보다 커지면 판을 꽉 채워서 답답하다
const MAX_FOOD = 30;    // 비트마스크 한계 (양쪽 엔진 공통)

// ---------- 격자 만들기 도구 ----------
function blank(w, h) {
  const g = [];
  for (let y = 0; y < h; y++) {
    g.push(Array.from({ length: w }, (_, x) =>
      (x === 0 || y === 0 || x === w - 1 || y === h - 1) ? '#' : '.'));
  }
  return g;
}
const fill = (g, x0, y0, x1, y1, ch) => {
  for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) g[y][x] = ch;
};
const put = (g, x, y, ch) => { g[y][x] = ch; };
const toStrings = g => g.map(r => r.join(''));

/// 걸어다니는 줄(y, x범위)에 한 칸 걸러 먹이를 놓는다
function scatter(g, runs, offset, gap = 2) {
  let n = 0;
  for (const [y, x0, x1] of runs) {
    for (let x = x0 + offset; x <= x1; x += gap) {
      if (g[y][x] === '.') { g[y][x] = 'o'; n++; }
    }
  }
  return n;
}

/// 정답을 밟는 동안 몸이 몇까지 커지는가
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

/// build(offset) 가 격자를 돌려주면, 통과하는 (offset, startSize)를 찾는다
function findGood(id, name, build, opts = {}) {
  const gaps = opts.gaps || [2];
  for (const gap of gaps)
    for (let offset = 0; offset <= 3; offset++)
      for (const startSize of (opts.sizes || [3, 2])) {
        const { grid, foods } = build(offset, gap);
        if (foods > MAX_FOOD) continue;
        const def = { grid, startSize, fireCost: opts.fireCost };
        const r = E.solve(def);
        if (!r.ok || r.shortest !== 1) continue;
        if (maxSizeAlong(def, r.path) > MAX_SIZE) continue;
        const back = (r.path.match(/←/g) || []).length;
        if (back < (opts.minBack || 0)) continue;
        return {
          id, name, startSize, grid, best: r.moves, sol: r.path, back,
          ...(opts.fireCost ? { fireCost: opts.fireCost } : {}),
          _meta: { offset, gap, foods, maxSize: maxSizeAlong(def, r.path) },
        };
      }
  return null;
}

// ---------- 판 다섯 ----------
// 🔴 판 크기는 고정이다 — 한 칸이 화면에서 항상 같은 크기여야 하기 때문.
//    GameController.BoardW/BoardH 와 반드시 같아야 한다.
const W = 20, H = 12;

const RECIPES = [
  {
    id: 'walk', name: '한 걸음마다 한 겹 줄어든다',
    build: (off, gap) => {
      const g = blank(W, H), floor = H - 2;
      put(g, 1, floor, 'S'); put(g, W - 2, floor, 'E');
      const foods = scatter(g, [[floor, 3, W - 3]], off, gap);
      return { grid: toStrings(g), foods };
    },
  },
  {
    id: 'drop', name: '떨어져서 간다',
    build: (off, gap) => {
      const g = blank(W, H);
      const ledge = 5, floor = H - 2, cut = 13;
      fill(g, 1, ledge + 1, cut, ledge + 1, '#');       // 왼쪽 선반 바닥
      put(g, 1, ledge, 'S'); put(g, W - 2, floor, 'E');
      const foods = scatter(g, [[ledge, 3, cut], [floor, cut + 1, W - 3]], off, gap);
      return { grid: toStrings(g), foods };
    },
  },
  {
    id: 'climb', name: '커야 오른다',
    build: (off, gap) => {
      const g = blank(W, H);
      const floor = H - 2, ledgeTop = floor - 2, cut = 14;
      fill(g, cut, ledgeTop + 1, W - 2, floor, '#');    // 오른쪽 2칸 턱
      put(g, 1, floor, 'S'); put(g, W - 2, ledgeTop, 'E');
      const foods = scatter(g, [[floor, 3, cut - 1], [ledgeTop, cut, W - 3]], off, gap);
      return { grid: toStrings(g), foods };
    },
  },
  {
    id: 'backtrack', name: '되돌아가서 먹고 온다',
    minBack: 1,
    build: (off, gap) => {
      const g = blank(W, H);
      const shelf = 5, floor = H - 2, sx = 12, hole = 5;
      fill(g, 1, shelf + 1, W - 2, shelf + 1, '#');     // 위층 바닥
      put(g, hole, shelf + 1, '.');                     // 🔴 내려가는 구멍은 '왼쪽'에만 있다
      put(g, sx, shelf, 'S'); put(g, W - 2, floor, 'E');
      // 출구는 오른쪽 아래인데 내려가는 길은 왼쪽뿐이다.
      // 오른쪽에도 먹이를 조금 둬서 '그냥 오른쪽으로 가고 싶게' 만든다 — 가면 막다른 길이다.
      // ⚠️ 왕복은 항상 손해다(2걸음 −2, 먹이 +1). 그래서 '갔다가 돌아오는' 판은 안 만든다.
      const foods = scatter(g, [
        [shelf, 2, sx - 2],          // 왼쪽: 구멍으로 가는 길
        [shelf, sx + 2, sx + 7],     // 오른쪽: 미끼 (막다른 길)
        [floor, hole, W - 3],        // 아래층: 출구까지
      ], off, gap);
      return { grid: toStrings(g), foods };
    },
  },
  {
    id: 'fire', name: '불을 덮어서 끈다', fireCost: 1,
    build: (off, gap) => {
      const g = blank(W, H), floor = H - 2;
      put(g, 1, floor, 'S'); put(g, W - 2, floor, 'E');
      const foods = scatter(g, [[floor, 3, W - 4]], off, gap);
      put(g, Math.floor(W / 2), floor, 'f');            // 한가운데에 불
      return { grid: toStrings(g), foods };
    },
  },
];

const out = [];
let fail = 0;
for (const r of RECIPES) {
  const lv = findGood(r.id, r.name, r.build, {
    fireCost: r.fireCost, minBack: r.minBack, gaps: [2, 3],
  });
  if (!lv) { console.log(`❌ ${r.id.padEnd(10)} 조건에 맞는 배치를 못 찾음`); fail++; continue; }
  const m = lv._meta;
  console.log(`${lv.back ? '✅' : '🟡'} ${lv.id.padEnd(10)} ${lv.grid[0].length}x${lv.grid.length} · ` +
              `시작 ${lv.startSize} · ${lv.best}걸음 · 되돌아가기 ${lv.back} · ` +
              `최대크기 ${m.maxSize} · 먹이 ${m.foods} · 간격 ${m.gap}/오프셋 ${m.offset}`);
  console.log(`   ${lv.sol}`);
  delete lv._meta;
  out.push(lv);
}

if (fail) { console.log(`\n${fail}개 실패 — 쓰지 않는다`); process.exit(1); }

console.log(`\n판 ${out.length}개 · 되돌아가기 없는 판 ${out.filter(l => !l.back).length}개`);

if (process.argv.includes('--write')) {
  const file = path.join(__dirname, '..', 'game', 'Assets', 'Resources', 'levels.json');
  const doc = JSON.parse(fs.readFileSync(file, 'utf8'));
  doc._크기 = `2026-08-28: 판을 ${W}x${H}로 키웠다. 몸은 최대 ${MAX_SIZE} — 판 높이의 1/4쯤. tools/make-levels.js가 짓는다`;
  doc.levels = out;
  fs.writeFileSync(file, JSON.stringify(doc, null, 2) + '\n');
  console.log('levels.json에 써 넣었다');
} else {
  console.log('(--write 를 주면 levels.json에 써 넣는다)');
  out.forEach(l => { console.log(`\n--- ${l.id}`); l.grid.forEach(r => console.log('  ' + r)); });
}
