// 🔴 **구경용 판** — 기믹이 어떻게 생겼는지 보려고 손으로 그린다 (2026-09-05).
//
// 사장님(09-05): *"대충 만들어 안 깨져도 되. 그냥 어떤 기능인지 보려는 거야. 외형이랑."*
//
// ⚠️ 그래서 **안 풀린다.** 브리프 원칙 2(기계가 검증한 것만)의 예외이고,
//    그 표시로 `demo: true` 를 붙인다 — 적합성 검사가 건너뛴다.
//
//   node tools/showcase-grid.js            보여만 준다
//   WRITE=1 node tools/showcase-grid.js    levels.json 의 1-1 자리에 끼운다
'use strict';
const fs = require('fs');
const path = require('path');

//  왼쪽부터 오른쪽으로 하나씩 —
//    조각 + · 무중력 : · 상자 B · 문벽 1 2 · 홈 셋(= * / - % / ~ @) · 별 o
const GRID = [
  '######################',
  '#....................#',
  '#..:...........o.....#',
  '#..:.......###########',
  '#..:..+..1.#....~~@..#',
  '#S....B..1.#....######',
  '#####.####.#.++......#',
  '#...#.#..#.#.#########',
  '#.+.#.#.++.#..--%....#',
  '#####.#####.#.########',
  '#..++.....2.#..==*.+.#',
  '######################',
];

if (require.main !== module) { module.exports = { GRID }; }
else {
  const W = GRID[0].length;
  for (const r of GRID) if (r.length !== W) { console.log('🔴 줄 길이가 다르다:', r.length, '≠', W); process.exit(1); }
  console.log(GRID.join('\n'));
  const count = ch => GRID.join('').split(ch).length - 1;
  console.log('');
  console.log('조각 + : ' + count('+') + '   무중력 : : ' + count(':') + '   상자 B : ' + count('B'));
  console.log('문벽 1 : ' + count('1') + '   문벽 2 : ' + count('2') + '   별 o : ' + count('o'));
  console.log('홈1 = * : ' + (count('=') + count('*')) + '   홈2 - % : ' + (count('-') + count('%')) +
              '   홈3 ~ @ : ' + (count('~') + count('@')));

  if (process.env.WRITE !== '1') { console.log('\n(보여주기만 했다. 쓰려면 WRITE=1)'); process.exit(0); }

  const LP = path.join(__dirname, '../game/Assets/Resources/levels.json');
  const d = JSON.parse(fs.readFileSync(LP, 'utf8'));
  fs.writeFileSync(path.join(__dirname, 'levels.bak.json'), fs.readFileSync(LP));
  const tut = d.levels.find(l => l.tutorial);
  const rest = d.levels.filter(l => !l.tutorial && !l.demo);
  const show = {
    id: '1-1', demo: true, grid: GRID, clear: 'all',
    best: 0, sol: '', bestStar: 0, cut: 0,
    lost: 0, tight: 0, backtrack: 0, states: 0,
  };
  //  🔴 있던 판은 하나도 안 버린다 — 뒤로 한 칸씩 민다
  d.levels = [tut, show, ...rest.map((l, i) => Object.assign({}, l, { id: '1-' + (i + 2) }))];
  fs.writeFileSync(LP, JSON.stringify(d, null, 2) + '\n');
  console.log('\n1-1 자리에 끼웠다 (구경용) · 판 ' + (d.levels.length - 1) + '개 (+튜토리얼)');
}
