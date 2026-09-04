// 🔴 **홈에 몸을 넣는 방법이 몇 가지인가**를 센다 (2026-09-05 밤, 사장님 지렛대).
//
// 몸은 사슬이다. 홈을 채운다 = **홈 칸을 한 번씩 지나 심에서 끝나는 길**을 걷는 것이다.
// 그런 길이 하나뿐이면 **그 하나를 찾아내야** 하고, 여럿이면 아무렇게나 넣어도 된다.
//
//   일자 4칸  →  넣는 법 1가지지만 **한눈에 보인다** (한 줄로 밀면 끝)
//   ㄷ자 5칸  →  넣는 법 1~2가지인데 **접는 순서를 그려야 한다**
//   2x2 4칸   →  넣는 법 여러 가지 (아무 데서나 감으면 된다) → 쉽다
//
// 그래서 둘을 같이 본다: **넣는 법의 수**(적을수록 어렵다)와 **꺾는 횟수**(많을수록 그리기 어렵다).
//
//   node tools/fit-ways.js            판 자료
//   node tools/fit-ways.js old        옛 17판
//   node tools/fit-ways.js <파일>     못
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

/// 홈 칸을 한 번씩 지나 심에서 끝나는 길을 **모두** 센다 (칸이 적어 그냥 다 헤아린다).
/// 꺾는 횟수도 같이 본다 — 곧게 가는 길보다 접는 길이 그리기 어렵다.
function ways(L, door) {
  const cells = door.cells.slice();
  const core = door.core >= 0 ? door.core : cells[0];
  const set = new Set(cells);
  const nb = c => [c - 1, c + 1, c - L.w, c + L.w].filter(n => set.has(n) &&
                  //  가로 이웃은 같은 줄이어야 한다 (판 끝을 넘어 이어지면 안 된다)
                  ((Math.abs(n - c) === L.w) || (((n / L.w) | 0) === ((c / L.w) | 0))));
  let count = 0, bestTurn = 0;
  //  심에서 거꾸로 걷는다 — 머리가 심에서 끝나므로 몸은 심에서 꼬리 쪽으로 이어진다
  const seen = new Set([core]);
  (function walk(at, n, lastDir, turns) {
    if (n === cells.length) { count++; if (turns > bestTurn) bestTurn = turns; return; }
    for (const m of nb(at)) {
      if (seen.has(m)) continue;
      const dir = m - at;
      seen.add(m);
      walk(m, n + 1, dir, turns + (lastDir !== null && dir !== lastDir ? 1 : 0));
      seen.delete(m);
    }
  })(core, 1, null, 0);
  return { n: cells.length, ways: count, turns: bestTurn };
}

function judge(l) {
  let L; try { L = E.parse({ grid: l.grid, gravity: true, clear: l.clear || 'all', id: 'x' }); }
  catch (e) { return null; }
  const ds = L.doors.map(d => ways(L, d));
  return {
    id: l.id, moves: l.best,
    //  🔴 **제일 까다로운 홈** 하나로 판을 대표한다 — 셋 중 하나만 어려우면 그 판은 어렵다
    hard: ds.reduce((b, s) => (s.ways < b.ways || (s.ways === b.ways && s.turns > b.turns)) ? s : b, ds[0]),
    all: ds.map(s => s.n + '칸 ' + s.ways + '가지/' + s.turns + '꺾음').join(' · '),
  };
}

const arg = process.argv[2];
let list, title;
if (arg === 'old') {
  const o = JSON.parse(fs.readFileSync(path.join(process.env.TEMP || '/tmp', 'old16.json'), 'utf8'));
  list = o.levels.filter(l => !l.tutorial); title = '옛 17판';
} else if (arg) {
  const raw = JSON.parse(fs.readFileSync(arg, 'utf8'));
  list = (raw.levels || raw).filter(l => l.grid && !l.tutorial && !l.demo); title = arg;
} else {
  const d = JSON.parse(fs.readFileSync(path.join(__dirname, '../game/Assets/Resources/levels.json'), 'utf8'));
  list = d.levels.filter(l => !l.tutorial && !l.demo); title = '지금 판 자료';
}

const rows = [];
for (const l of list) { const r = judge(l); if (r) rows.push(r); }
console.log(title + ' — ' + rows.length + '판');
console.log('번호     걸음  제일까다로운홈        홈 전부');
for (const r of rows) {
  const h = r.hard;
  console.log(String(r.id).padEnd(8) + String(r.moves).padStart(4) + '  ' +
              (h.n + '칸 ' + h.ways + '가지 ' + h.turns + '꺾음').padEnd(20) + r.all);
}
const w = rows.map(r => r.hard.ways).sort((a, b) => a - b);
const t = rows.map(r => r.hard.turns).sort((a, b) => a - b);
console.log('-'.repeat(60));
console.log('넣는 법  중앙 ' + w[Math.floor(w.length / 2)] + ' · 제일 적은 것 ' + w[0] + ' · 제일 많은 것 ' + w[w.length - 1]);
console.log('꺾음     중앙 ' + t[Math.floor(t.length / 2)] + ' · 제일 많은 것 ' + t[t.length - 1]);
console.log('넣는 법이 **1가지뿐**인 판: ' + rows.filter(r => r.hard.ways === 1).length + ' / ' + rows.length);
