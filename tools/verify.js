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

  //  🔴 **조각 개수 규칙** — 홈이 셋이면 (칸수-1)×셋 만큼 조각이 있어야 한다.
  //     어기면 유니티가 판을 아예 못 읽고 ArgumentException 을 던진다.
  //     09-05 에 구경용 판이 이걸 어겨서 게임이 멈췄다 — 사장님 화면에서 봤다.
  //     솔버까지 안 가고 **여기서** 걸러야 한다.
  {
    const sizes = [['=', '*'], ['-', '%'], ['~', '@']]
      .map(p => l.grid.join('').split('').filter(c => p.includes(c)).length)
      .filter(n => n > 0);
    const need = (l.clear || 'all') === 'all'
      ? sizes.reduce((s, n) => s + n - 1, 0)
      : (sizes[0] || 1) - 1;
    const have = l.grid.join('').split('+').length - 1;
    if (have < need) { say(l.id, '조각이 모자란다 — ' + need + '개가 필요한데 ' + have + '개다'); continue; }
  }

  //  🔴 구경용 판은 **안 깨져도 된다.** 읽히기만 하면 된다 (09-05 사장님).
  if (l.demo) { try { E.parse(def); } catch (e) { say(l.id, '판을 못 읽는다: ' + e.message); } continue; }

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
