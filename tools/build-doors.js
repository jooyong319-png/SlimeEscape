// 🔴 찾아둔 두 번째 문(tools/doors.json)을 판에 심고, **양쪽 다** 검증한다.
//
//   node tools/build-doors.js            검증만
//   node tools/build-doors.js --write    levels.json에 반영
//
// 문마다 따로 풀어봐야 한다 — 솔버는 한 번에 한 목표만 본다.
// 양쪽 다 "풀린다 + 최단해가 하나" 여야 판에 넣는다.
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const LEVELS = path.join(__dirname, '..', 'game', 'Assets', 'Resources', 'levels.json');
const DOORS = path.join(__dirname, 'doors.json');
const doc = JSON.parse(fs.readFileSync(LEVELS, 'utf8'));
const found = JSON.parse(fs.readFileSync(DOORS, 'utf8'));
const write = process.argv.includes('--write');

/// 그 문 하나만 목표로 둔 판을 만든다 (검증용)
function onlyDoor(grid, which) {
  return grid.map(r => r.split('').map(c => {
    if (which === 0) return (c === '-') ? '.' : (c === '%') ? '.' : c;
    if (c === '=' || c === '*') return '.';
    if (c === '-') return '=';
    if (c === '%') return '*';
    return c;
  }).join(''));
}

let fail = 0;
console.log('판    문1                        문2');
for (const lv of doc.levels) {
  const best = found[lv.id];
  if (!best) { console.log(lv.id.padEnd(5) + ' 두 번째 문 후보가 없다'); fail++; continue; }

  const W = lv.grid[0].length;
  const rows = lv.grid.map(r => r.split(''));
  // 이미 심어져 있으면 그대로 둔다
  const already = lv.grid.join('').includes('-') || lv.grid.join('').includes('%');
  if (!already) {
    best.cells.forEach((c, k) => {
      const y = (c / W) | 0, x = c % W;
      if (rows[y][x] !== '.') { console.log(lv.id + ' 자리가 비어있지 않다'); fail++; return; }
      rows[y][x] = (k === 0 || k === best.cells.length - 1)
        ? rows[y][x] : rows[y][x];
    });
    // 심은 경로의 한쪽 끝에 (doors.js가 고른 것과 같은 규칙)
    best.cells.forEach((c, k) => {
      const y = (c / W) | 0, x = c % W;
      rows[y][x] = '-';
    });
    const cc = best.cells[best.cells.length - 1];
    rows[(cc / W) | 0][cc % W] = '%';
  }
  const grid = rows.map(r => r.join(''));

  const out = [];
  let bad = false;
  for (let i = 0; i < 2; i++) {
    let r;
    try { r = E.solve({ grid: onlyDoor(grid, i), gravity: true }); }
    catch (e) { out.push('X ' + e.message); bad = true; continue; }
    if (!r.ok) { out.push('X ' + r.why); bad = true; continue; }
    if (r.shortest !== 1) { out.push('~ ' + r.moves + '걸음 최단해 ' + r.shortest + '개'); bad = true; continue; }
    out.push(r.moves + '걸음 · 최단해 1개');
    lv['sol' + (i + 1)] = r.path;
    lv['best' + (i + 1)] = r.moves;
  }
  if (bad) fail++;
  else lv.grid = grid;
  console.log(lv.id.padEnd(5) + ' ' + out[0].padEnd(26) + ' ' + out[1]);
}

if (write && !fail) {
  doc._문 = '=/* 는 첫째 문, -/% 는 둘째 문. 문마다 칸 수가 같아야 한다 (길이가 하나뿐이므로).';
  fs.writeFileSync(LEVELS, JSON.stringify(doc, null, 2) + '\n');
  console.log('\nlevels.json 갱신함');
} else if (fail) {
  console.log('\n실패 ' + fail + '개 — 그 판은 두 번째 문을 못 넣는다');
} else {
  console.log('\n전부 통과 (--write 로 반영)');
}
process.exit(fail ? 1 : 0);
