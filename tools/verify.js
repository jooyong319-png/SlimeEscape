// 판 자료를 **엔진으로 되짚어** 검사한다 (2026-09-05).
//
// 유니티 ConformanceCheck 는 에디터를 켜야 한다. 그 전에 여기서 걸러낸다.
//   node tools/verify.js
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const LP = path.join(__dirname, '../game/Assets/Resources/levels.json');
const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };

const d = JSON.parse(fs.readFileSync(LP, 'utf8'));
let bad = 0, warn = 0;
const say = (id, msg) => { bad++; console.log('🔴 ' + id + ' — ' + msg); };
const hmm = (id, msg) => { warn++; console.log('⚠️ ' + id + ' — ' + msg); };

for (const l of d.levels) {
  const def = { grid: l.grid, gravity: true, clear: l.clear || 'all', id: l.id };

  // 판이 네모난가
  const w = l.grid[0].length;
  if (l.grid.some(r => r.length !== w)) { say(l.id, '줄 길이가 들쭉날쭉'); continue; }

  // 적어둔 수순이 진짜 이기는가
  let st, ok = true;
  try {
    const L = E.parse(def);
    st = E.startState(L);
    for (const ch of (l.sol || '')) {
      const dir = SYM[ch];
      if (dir === undefined) continue;
      const ns = E.step(L, st, dir);
      if (!ns) { ok = false; break; }
      st = ns;
    }
    if (!ok) say(l.id, '적어둔 수순이 도중에 막힌다');
    else if (!E.isWin(L, st)) say(l.id, '수순을 다 따라가도 안 이긴다');
  } catch (e) { say(l.id, '되짚다 터졌다: ' + e.message); continue; }

  // 최단이 맞는가 · 유일한가
  const a = E.solve(def);
  if (!a.ok) { say(l.id, '풀리지 않는다'); continue; }
  if (a.moves !== l.best) say(l.id, 'best 가 ' + l.best + ' 인데 실제 최단은 ' + a.moves);
  if (a.shortest !== 1) hmm(l.id, '최단해가 여럿이다 (' + a.shortest + ')');

  // 별을 먹는 최단 · 3별 커트라인
  const hasStar = l.grid.some(r => r.includes('o'));
  if (!hasStar) { hmm(l.id, '별이 없다'); continue; }
  const s = E.solve(def, { needStar: true });
  if (!s.ok) { say(l.id, '별을 먹고는 못 깬다'); continue; }
  if (s.moves !== l.bestStar) say(l.id, 'bestStar 가 ' + l.bestStar + ' 인데 실제는 ' + s.moves);
  //  🔴 3별은 **별을 먹고** 커트라인 안에 들어야 한다. 09-04 에 이걸 best 로 잡았다가
  //     47판 중 34판이 3별 불가능해졌다.
  if (l.cut < s.moves) say(l.id, '3별 불가능 — 커트 ' + l.cut + ' < 별최단 ' + s.moves);
}
console.log('\n판 ' + d.levels.length + '개 · 🔴 ' + bad + '건 · ⚠️ ' + warn + '건');
process.exit(bad ? 1 : 0);
