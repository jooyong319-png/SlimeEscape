// 모든 판을 고정 크기(BOARD_W × BOARD_H)로 맞춘다.
//
// 🔴 판 크기를 고정해야 "한 칸이 화면에서 항상 같은 크기"가 성립한다.
//    판마다 크기가 다르면 카메라가 판에 맞추느라 칸 크기가 달라진다.
//
//   node tools/normalize.js          맞춰보고 결과만 (파일 안 고침)
//   node tools/normalize.js --write  levels.json에 써 넣는다
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const BOARD_W = 20, BOARD_H = 12;

/// 이 줄이 통째로 벽인가 (바닥/천장) — 늘릴 때 뭘 채울지 정한다
const solidRow = row => row.slice(1, -1).split('').every(c => c === '#');

function pad(grid) {
  let g = grid.slice();
  if (g[0].length > BOARD_W || g.length > BOARD_H)
    return { error: `${g[0].length}x${g.length} 는 ${BOARD_W}x${BOARD_H}보다 크다 — 줄여야 한다` };

  // 가로: 오른쪽 테두리 '앞'에 채운다
  g = g.map(row => {
    const filler = solidRow(row) ? '#' : '.';
    return row.slice(0, -1) + filler.repeat(BOARD_W - row.length) + row.slice(-1);
  });

  // 세로: 위쪽 테두리 '아래'에 빈 줄을 넣는다 (머리 위 여유가 늘 뿐 지형은 그대로)
  const empty = '#' + '.'.repeat(BOARD_W - 2) + '#';
  while (g.length < BOARD_H) g.splice(1, 0, empty);

  return { grid: g };
}

const file = path.join(__dirname, '..', 'game', 'Assets', 'Resources', 'levels.json');
const doc = JSON.parse(fs.readFileSync(file, 'utf8'));
const write = process.argv.includes('--write');

let fail = 0;
for (const lv of doc.levels) {
  const before = `${lv.grid[0].length}x${lv.grid.length}`;
  const r = pad(lv.grid);
  if (r.error) { console.log(`❌ ${lv.id.padEnd(10)} ${before} — ${r.error}`); fail++; continue; }

  const solved = E.solve({ ...lv, grid: r.grid });
  if (!solved.ok) { console.log(`❌ ${lv.id.padEnd(10)} ${before} → ${BOARD_W}x${BOARD_H} 후 ${solved.why}`); fail++; continue; }

  // 머리 위 여유가 늘면 더 커질 수 있다 — 실제로 커지는지 다시 잰다
  const L = E.parse({ ...lv, grid: r.grid });
  let st = E.startState(L), mx = st ? st.n : 0;
  for (const c of solved.path) { st = E.move(L, st, c === '→' ? 1 : -1); if (!st) break; mx = Math.max(mx, st.n); }

  const back = (solved.path.match(/←/g) || []).length;
  const changed = solved.moves !== lv.best || solved.path !== lv.sol;
  console.log(
    `${solved.shortest === 1 ? '✅' : '⚠️ '} ${lv.id.padEnd(10)} ${before} → ${BOARD_W}x${BOARD_H} · ` +
    `${solved.moves}걸음 · 되돌아가기 ${back} · 최단해 ${solved.shortest}개 · 최대크기 ${mx}` +
    (changed ? `   ← 정답이 바뀜 (${lv.best}→${solved.moves})` : ''));
  if (solved.shortest !== 1) fail++;
  if (mx > 3) { console.log(`   ⚠️ 최대크기 ${mx} — 머리 위가 늘어서 더 커졌다`); }

  lv.grid = r.grid; lv.best = solved.moves; lv.sol = solved.path; lv.back = back;
}

console.log(`\n판 ${doc.levels.length}개 · 고정 크기 ${BOARD_W}x${BOARD_H}`);
if (fail) { console.log(`${fail}개 문제 — 쓰지 않는다`); process.exit(1); }
if (write) {
  doc._판크기 = `모든 판은 ${BOARD_W}x${BOARD_H} 고정. 한 칸이 화면에서 항상 같은 크기여야 하기 때문이다 (tools/normalize.js)`;
  fs.writeFileSync(file, JSON.stringify(doc, null, 2) + '\n');
  console.log('levels.json 갱신함');
} else console.log('(--write 를 주면 써 넣는다)');
