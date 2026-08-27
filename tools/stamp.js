// levels/levels.json의 best/sol/back을 솔버 결과로 박는다. 몇 번 돌려도 같은 결과.
// 안 풀리거나 최단해가 여럿이면 0이 아닌 코드로 끝난다 (검사로 쓸 수 있다).
//
//   node tools/stamp.js            검증만 (파일 안 고침)
//   node tools/stamp.js --write    검증 + 표기값 갱신
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const file = path.join(__dirname, '..', 'game', 'Assets', 'Resources', 'levels.json');
const doc = JSON.parse(fs.readFileSync(file, 'utf8'));
const write = process.argv.includes('--write');

let fail = 0, trivial = 0;
for (const lv of doc.levels) {
  const r = E.solve(lv);
  if (!r.ok) { console.log(`❌ ${lv.id.padEnd(10)} ${lv.name} — ${r.why}`); fail++; continue; }
  const back = (r.path.match(/←/g) || []).length;
  const uniq = r.shortest === 1;
  if (!uniq) fail++;
  if (back === 0) trivial++;

  const drift = [];
  if (lv.best !== undefined && lv.best !== r.moves) drift.push(`best ${lv.best}→${r.moves}`);
  if (lv.sol !== undefined && lv.sol !== r.path) drift.push(`sol 바뀜`);
  if (lv.back !== undefined && lv.back !== back) drift.push(`back ${lv.back}→${back}`);
  if (drift.length && !write) fail++;

  console.log(
    `${uniq ? (back ? '✅' : '🟡') : '⚠️ '} ${lv.id.padEnd(10)} ${String(r.moves).padStart(2)}걸음 · ` +
    `되돌아가기 ${back} · 최단해 ${r.shortest}개 · ${r.path}` +
    (drift.length ? `   ← 표기값 불일치: ${drift.join(', ')}` : '')
  );

  lv.best = r.moves; lv.sol = r.path; lv.back = back;
}

console.log(`\n판 ${doc.levels.length}개 · 🟡 되돌아가기 없는 판 ${trivial}개 (= 오른쪽만 누르면 풀린다)`);
if (write) { fs.writeFileSync(file, JSON.stringify(doc, null, 2) + '\n'); console.log('levels.json 갱신함'); }
else if (fail) console.log('표기값이 어긋났다. --write로 갱신하거나 판을 고칠 것');
process.exit(fail ? 1 : 0);
