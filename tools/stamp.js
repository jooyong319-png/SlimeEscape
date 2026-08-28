// levels.json을 솔버로 검증하고 표기값(best/sol/lost)을 박는다.
//
//   node tools/stamp.js            검증만 (파일 안 고침)
//   node tools/stamp.js --write    검증 + 표기값 갱신
//
// 🔴 원칙 2 — 이걸 통과 못 한 판은 게임에 안 넣는다.
//    풀린다 · 최단해가 하나뿐이다 · 표기값이 실제와 같다.
// 🔴 원칙 3 — "이미 진 상태" 비율도 같이 잰다. 낮으면 실수해도 회복돼서 퍼즐이 아니다.
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { analyze } = require('./metrics.js');   // 🔴 난이도 지표는 여기 한 벌뿐이다

const FILE = path.join(__dirname, '..', 'game', 'Assets', 'Resources', 'levels.json');
const CAP = 700000;   // 상태가 이보다 많으면 난이도 계산은 건너뛴다

// 🔴 판 채택 기준 (2026-08-28)
//    게임이 "이미 졌다"고 안 알려주기로 했으므로, 헤매는 시간을 **판 설계로** 줄여야 한다.
//    wander = 진 뒤에도 더 돌아다닐 수 있는 걸음 수. 이게 길면 플레이어는 지겨워진다.
const WANDER_MAX = 14;

const doc = JSON.parse(fs.readFileSync(FILE, 'utf8'));
const write = process.argv.includes('--write');
let fail = 0;

console.log(`판 ${doc.levels.length}개 · 중력 ${doc.gravity === false ? '끔' : '켬'}\n`);
for (const lv of doc.levels) {
  const def = { grid: lv.grid, gravity: doc.gravity !== false };
  let r;
  try { r = E.solve(def); }
  catch (e) { console.log(`❌ ${lv.id.padEnd(10)} ${e.message}`); fail++; continue; }
  if (!r.ok) { console.log(`❌ ${lv.id.padEnd(10)} ${r.why}`); fail++; continue; }

  const uniq = r.shortest === 1;
  if (!uniq) fail++;
  const a = analyze(def, CAP);
  const lost = a ? a.lost : null;
  const wander = a ? a.wander : null;
  if (wander !== null && wander > WANDER_MAX) fail++;

  const drift = [];
  if (lv.best !== undefined && lv.best !== r.moves) drift.push(`best ${lv.best}→${r.moves}`);
  if (lv.sol !== undefined && lv.sol !== r.path) drift.push('sol 바뀜');
  if (drift.length && !write) fail++;

  console.log(
    `${uniq ? '✅' : '⚠️ '} ${lv.id.padEnd(10)} ${String(r.moves).padStart(3)}걸음 · ` +
    `최단해 ${r.shortest}개 · 상태 ${String(r.states.toLocaleString()).padStart(9)} · ` +
    `이미 진 상태 ${lost === null ? '  (생략)' : (lost * 100).toFixed(1) + '%'}` +
    (a && a.earliest !== null ? ` · 빠르면 ${a.earliest}걸음에 진다` : '') +
    (wander === null ? '' : ` · ${wander > WANDER_MAX ? '🔴 ' : ''}진 뒤 ${wander}걸음 헤맴`) +
    (drift.length ? `   ← 표기값 불일치: ${drift.join(', ')}` : ''));
  console.log(`   ${r.path}`);

  lv.best = r.moves; lv.sol = r.path;
  if (lost !== null) lv.lost = Math.round(lost * 1000) / 10;   // % 소수 한 자리
  if (wander !== null) lv.wander = wander;
}

if (write) {
  fs.writeFileSync(FILE, JSON.stringify(doc, null, 2) + '\n');
  console.log('\nlevels.json 갱신함');
} else if (fail) {
  console.log(`\n검증 실패. --write로 표기값을 갱신하거나, 헤맴이 ${WANDER_MAX}걸음을 넘는 판은 고칠 것`);
} else {
  console.log('\n전부 통과');
}
process.exit(fail ? 1 : 0);
