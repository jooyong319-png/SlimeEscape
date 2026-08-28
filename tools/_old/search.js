// 지형은 사람이 그리고, 먹이 배치는 기계가 찾는다.
//
//   node tools/search.js <뼈대.json> [id]
//
// 뼈대의 '?' 칸이 먹이 후보다. 그 부분집합을 전부 돌려서 조건을 만족하는 배치를 찾는다.
//   solvable · 최단해가 유일 · 걸음 수 >= minMoves
//   되돌아가기 >= minBack     (0이면 "오른쪽만 누르면 풀린다" = 퍼즐이 아니다)
//   최대 크기 <= maxSize      (몸이 판을 꽉 채우면 답답하다)
const fs = require('fs');
const E = require('./engine.js').SlimeEngine;

/// 정답을 밟는 동안 몸이 몇까지 커지는가
function maxSizeAlong(def, path) {
  const L = E.parse(def);
  let st = E.startState(L);
  if (!st) return Infinity;
  let mx = st.n;
  for (const c of path) {
    st = E.move(L, st, c === '→' ? 1 : -1);
    if (!st) return Infinity;
    if (st.n > mx) mx = st.n;
  }
  return mx;
}

function build(grid, cand, mask) {
  return grid.map((row, y) => row.split('').map((ch, x) => {
    if (ch !== '?') return ch;
    const i = cand.findIndex(p => p[0] === x && p[1] === y);
    return (mask >> i) & 1 ? 'o' : '.';
  }).join(''));
}

const layouts = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const only = process.argv[3];

for (const lay of layouts) {
  if (only && lay.id !== only) continue;
  console.log('='.repeat(64));
  console.log(`${lay.id}. ${lay.name}`);

  const cand = [];
  lay.grid.forEach((r, y) => r.split('').forEach((ch, x) => { if (ch === '?') cand.push([x, y]); }));
  if (cand.length > 20) { console.log(`   먹이 후보 ${cand.length}칸은 너무 많다 (최대 20)`); continue; }

  const minMoves = lay.minMoves || 6;
  const minBack = lay.minBack ?? 0;
  const maxSize = lay.maxSize || 3;
  const sizes = lay.sizes || [2, 3];
  console.log(`   후보 ${cand.length}칸 · 조합 ${2 ** cand.length} · ` +
              `조건: ${minMoves}걸음+ · 되돌아가기 ${minBack}+ · 최대크기 ${maxSize} 이하`);

  const found = [];
  for (let mask = 0; mask < 2 ** cand.length; mask++) {
    const grid = build(lay.grid, cand, mask);
    for (const s of sizes) {
      const def = { grid, startSize: s, fireCost: lay.fireCost };
      const r = E.solve(def);
      if (!r.ok || r.shortest !== 1 || r.moves < minMoves) continue;
      const back = (r.path.match(/←/g) || []).length;
      if (back < minBack) continue;
      const mx = maxSizeAlong(def, r.path);
      if (mx > maxSize) continue;
      let bits = 0, m = mask; while (m) { bits += m & 1; m >>= 1; }
      found.push({ s, moves: r.moves, path: r.path, back, mx, foods: bits, grid });
    }
  }

  if (!found.length) { console.log('   ❌ 조건을 만족하는 배치 없음'); continue; }
  // 되돌아가기 많고 · 길고 · 몸이 작고 · 먹이 적은 순
  found.sort((a, b) => (b.back - a.back) || (b.moves - a.moves) || (a.mx - b.mx) || (a.foods - b.foods));
  for (const [i, f] of found.slice(0, 2).entries()) {
    console.log(`   --- 후보 ${i + 1}: 시작 ${f.s} · ${f.moves}걸음 · 되돌아가기 ${f.back} · ` +
                `최대크기 ${f.mx} · 먹이 ${f.foods}개`);
    console.log(`       ${f.path}`);
    f.grid.forEach(r => console.log('       ' + r));
  }
  console.log(`   (조건 만족 ${found.length}가지 중 상위 ${Math.min(2, found.length)}개)`);
}
