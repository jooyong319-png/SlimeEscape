// 판 목록을 처음부터 끝까지 훑는다.
//
// 확인하는 것:
//   · 판마다 실제로 풀리는가 (홈을 채우면 끝 — 출구는 연출일 뿐이다)
//   · 판마다 별이 있고, 별을 먹는 길이 그냥 가는 길과 **다른가**
//   · 커트라인이 별 먹는 최단보다 넉넉한가
//   · 앞판이 뒷판보다 쉬운가 (난이도가 뒤집히면 알려준다)
//
// 🔴 이건 "풀 수 있나"만 본다. **재미는 못 잰다.** 두 번 증명했다.
'use strict';
const E = require('./engine.js').SlimeEngine;
const d = require('../game/Assets/Resources/levels.json');

const solve = (l, star) =>
  E.solve({ grid: l.grid, gravity: true, clear: l.clear || 'all', id: l.id },
          star ? { needStar: true } : undefined);

let bad = 0, warn = 0, sumBest = 0, sumStar = 0;
console.log('#   판     최단   별포함  돌아감  커트    별자리');
console.log('─'.repeat(58));

let prev = 0;
d.levels.forEach((l, i) => {
  const hasStar = l.grid.join('').includes('o');
  const r = solve(l, false);
  if (!r.ok) { console.log(String(i + 1).padStart(2) + '. ' + l.id.padEnd(6) + '🔴 ' + r.why); bad++; return; }
  sumBest += r.moves;

  let sr = null;
  if (hasStar) { sr = solve(l, true); if (sr.ok) sumStar += sr.moves; }

  const detour = sr && sr.ok ? sr.moves - r.moves : null;
  console.log(
    String(i + 1).padStart(2) + '. ' + l.id.padEnd(6) +
    String(r.moves).padStart(5) +
    (sr && sr.ok ? String(sr.moves).padStart(8) : '       —') +
    (detour === null ? '       —' : ('     +' + detour).padStart(8)) +
    (l.cut ? String(l.cut).padStart(7) : '      —') +
    (hasStar ? '   O' : '   🔴 없음'));

  if (!hasStar) { console.log('    🔴 ' + l.id + ' — 별이 없다. 별 둘·셋을 못 받는다'); bad++; }
  else if (!sr.ok) { console.log('    🔴 ' + l.id + ' — 별을 먹고는 못 깬다'); bad++; }
  else {
    if (detour === 0) { console.log('    🟡 ' + l.id + ' — 별이 가는 길에 있다. 장식이지 퍼즐이 아니다'); warn++; }
    if (!l.cut || l.cut < sr.moves) { console.log('    🔴 ' + l.id + ' — 커트라인이 별 먹는 최단보다 빡빡하다'); bad++; }
  }
  if (r.moves + 12 < prev) { console.log('    🟡 ' + l.id + ' — 앞판보다 확 쉬워진다 (' + prev + ' → ' + r.moves + ')'); warn++; }
  prev = r.moves;
});

console.log('─'.repeat(58));
console.log('판 ' + d.levels.length + ' · 최단 합계 ' + sumBest + '걸음 · 별까지 ' + sumStar + '걸음');

// 🔴 실측으로 맞춘 어림 (08-30 사장님 기록):
//    1-15 최단 47걸음 → 337초 · 1-16 최단 47걸음 → 214초.
//    긴 판일수록 최단 대비 배수가 커진다 — 47걸음짜리에 5배를 걸으셨다.
//    그래서 판마다 초 = best * (1.5 + best/15) 로 잡는다. 47 → 216초로 맞는다.
let secs = 0;
for (const l of d.levels) secs += l.best * (1.5 + l.best / 15);
console.log('어림 ' + (secs / 60).toFixed(0) + '분 — 목표 60분 (한 묶음)');
console.log('  판마다: ' + d.levels.map(l => Math.round(l.best * (1.5 + l.best / 15) / 60 * 10) / 10 + '분').join(' '));
console.log(bad ? '\n🔴 고칠 것 ' + bad + '개' + (warn ? ' · 봐둘 것 ' + warn + '개' : '')
                : '\n🟢 처음부터 끝까지 풀린다' + (warn ? ' · 봐둘 것 ' + warn + '개' : ''));
process.exit(bad ? 1 : 0);
