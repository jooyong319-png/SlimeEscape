// 새 규칙(중력 + N×N)으로 레이아웃을 전수 조사하고, 원하면 수순을 판으로 재생한다.
const fs = require('fs');
const E = require('./engine.js').SlimeEngine;

function render(L, st) {
  const rows = [];
  const body = new Set(E.covered(L, st.x, st.y, st.n));
  for (let y = 0; y < L.h; y++) {
    let r = '';
    for (let x = 0; x < L.w; x++) {
      const c = y * L.w + x;
      const fi = L.foodIdx.get(c), gi = L.fireIdx.get(c);
      if (body.has(c)) r += '@';
      else if (L.g[y][x] === '#') r += '#';
      else if (L.exit[0] === x && L.exit[1] === y) r += 'E';
      else if (fi !== undefined && !(st.fm & (1 << fi))) r += 'o';
      else if (gi !== undefined && !(st.gm & (1 << gi))) r += 'f';
      else r += '·';
    }
    rows.push('   ' + r);
  }
  return rows.join('\n');
}

function trace(def) {
  const L = E.parse(def);
  const r = E.solve(def);
  if (!r.ok) { console.log('   해 없음: ' + r.why); return; }
  let st = E.startState(L);
  console.log(`   시작 (크기 ${st.n})`);
  console.log(render(L, st));
  for (const sym of r.path) {
    st = E.move(L, st, sym === '→' ? 1 : -1);
    console.log(`   ${sym}  크기 ${st.n}`);
    console.log(render(L, st));
  }
}

const LAYOUTS = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const traceId = process.argv[3];

for (const lay of LAYOUTS) {
  console.log('='.repeat(60));
  console.log(`${lay.id}. ${lay.name}`);
  if (traceId && lay.id !== traceId) continue;
  if (traceId) { trace({ ...lay, startSize: Number(process.argv[4] || 3) }); continue; }
  lay.grid.forEach(r => console.log('   ' + r));
  let any = false;
  for (let s = 1; s <= 7; s++) {
    const r = E.solve({ ...lay, startSize: s });
    if (!r.ok) continue;
    any = true;
    console.log(`   시작 ${s} → ${String(r.moves).padStart(2)}수 · 최단해 ${r.shortest}개${r.shortest === 1 ? ' (유일)' : ''} · ${r.path}`);
  }
  if (!any) {
    for (let s = 1; s <= 7; s++) {
      const r = E.solve({ ...lay, startSize: s });
      console.log(`   시작 ${s} → ${r.why}${r.states ? ' (탐색 ' + r.states + ')' : ''}`);
    }
  }
}
