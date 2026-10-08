// 🔴 슬라임 둘 규칙 단위 시험 (09-23) — node tools/twin-test.js
'use strict';
const E = require('./engine.js').SlimeEngine;
const D = { '↑': 0, '↓': 1, '←': 2, '→': 3, '⇄': 5 };
let ok = 0, fail = 0;
function run(name, grid, moves, want, clear = 'all') {
  const L = E.parse({ grid, gravity: true, clear });
  let st = E.startState(L);
  for (const m of moves) { const ns = E.step(L, st, D[m]); if (!ns) { st = null; break; } st = ns; }
  const got = st ? E.render(L, st).join('\n') : 'null';
  const pass = want === null ? st === null : got === want.join('\n');
  if (pass) ok++; else { fail++; console.log('✗ ' + name + '\n' + got + '\n--- 기대\n' + (want ? want.join('\n') : 'null')); }
}
// 1) 겹칠 수 없다 — 옆 슬라임 쪽으로 못 간다
run('겹치기 금지', ['#####', '#SZ.#', '#####'], '→', null);
// 2) 올라탄다 — 둘째 위로 올라서기 (길이 2 라야 한 칸 오른다)
run('올라타기', ['######', '#....#', '#S+Z.#', '######'], '→↑→', [
  '######', '#·oO·#', '#··Z·#', '######']);
// 3) 키보드 교체 — ⇄ 뒤엔 둘째가 움직인다
run('키보드 교체', ['#######', '#S...Z#', '#######'], '⇄←', [
  '#######', '#Z··O·#', '#######']);
// 4) 발판이 비켜나면 떨어진다 — 첫째 위에 선 둘째가, 첫째가 빠지면 내려온다
run('발판 빠지면 낙하', ['#####', '#.Z.#', '#.S.#', '##.##', '#####'], '↓', null);
//    (한 걸음째엔 꼬리가 아직 둘째 밑에 있다 — 두 걸음째에 빠진다)
run('발판 빠지면 낙하2', ['######', '#.Z..#', '#.S+.#', '####.#', '######'], '→→', [
  '######', '#····#', '#·ZoO#', '####·#', '######']);
// 5) 교체 칸 — 머리가 & 에 들어오면 조종이 넘어간다
run('교체 칸', ['#######', '#S&..Z#', '#######'], '→←', [
  '#######', '#·Z·O·#', '#######']);
console.log('통과 ' + ok + ' · 실패 ' + fail);
