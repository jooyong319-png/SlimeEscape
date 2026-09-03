// 🔴 **구경용 판** — 기믹이 어떻게 생겼는지 보려고 손으로 그린다 (2026-09-05).
//
// 사장님(09-05): *"대충 만들어 안 깨져도 되. 그냥 어떤 기능인지 보려는 거야. 외형이랑."*
//
// ⚠️ 그래서 **안 깨진다.** 브리프 원칙 2(기계가 검증한 것만)의 예외이고,
//    그 표시로 `demo: true` 를 붙인다 — 적합성 검사가 건너뛴다.
//
// 🔴 첫 판은 상자까지 **걸어갈 수가 없었다** — 가는 길 밑에 구멍이 있어 떨어졌다
//    (사장님: *"밀리는 돌이 뭐야?"*). 구경용 판은 **닿을 수 있어야** 구경이 된다.
//
// 지금 판에서 해볼 수 있는 것:
//   · S 에서 오른쪽 두 칸 → 상자 B 가 밀린다 (가로로만 밀린다)
//   · S 에서 오른쪽 한 칸 뒤 위 → 무중력 칸 ':' 에 **길이 1로도 떠 오른다**
//   · 조각 둘을 먹고 홈(= = *)을 채우면 → 문벽 '1' 이 흐려지며 사라진다
//   · 문 너머에 둘째 홈(- - %) · 셋째 홈(~ ~ @) · 별(o) 이 보인다
'use strict';
const fs = require('fs');
const path = require('path');

const GRID = [
  '######################',
  '#....................#',
  '#....................#',
  '#.:..................#',
  '#.:........1..+.+....#',
  '#.:........1..+.+....#',
  '#S.B..++==*1.--%.~~@o#',
  '######################',
];

module.exports = { GRID };

if (require.main === module) {
  const W = GRID[0].length, H = GRID.length;
  const at = (x, y) => (y < 0 || y >= H || x < 0 || x >= W) ? '#' : GRID[y][x];

  for (const r of GRID) if (r.length !== W) { console.log('🔴 줄 길이가 다르다'); process.exit(1); }

  // 🔴 **닿을 수 있나**를 확인한다. 첫 판이 이걸 안 봐서 못 쓰게 됐다.
  let sx = -1, sy = -1;
  for (let y = 0; y < H; y++) { const x = GRID[y].indexOf('S'); if (x >= 0) { sx = x; sy = y; } }
  if (sx < 0) { console.log('🔴 S 가 없다'); process.exit(1); }
  const bad = [];
  for (let x = sx; x < W - 1; x++) {
    const c = at(x, sy);
    if (c === '#' || c === '1' || c === '2' || c === '3') break;   // 벽·문벽까지만 본다
    if (at(x, sy + 1) === '.') bad.push(x);                        // 발밑이 뚫렸다 = 떨어진다
  }
  console.log(GRID.join('\n'));
  console.log('');
  if (bad.length) console.log('🔴 시작 줄 ' + bad.join(',') + '번 칸은 발밑이 뚫렸다 — 걸어가다 떨어진다');
  else console.log('🟢 시작점에서 문벽까지 발밑이 이어진다 — 걸어서 상자에 닿는다');

  //  🔴 **조각 개수 규칙을 여기서 센다** (09-05).
  //     홈이 셀이면 조각이 (칸수-1)×셀 만큼 있어야 한다.
  //     어기면 유니티가 판을 아예 못 읽고 ArgumentException 을 던진다 —
  //     그러면 구경용이 아니라 고장이 된다. 사장님 화면에서 그걸 봤다.
  {
    const doorSizes = [['=', '*'], ['-', '%'], ['~', '@']]
      .map(p => GRID.join('').split('').filter(c => p.includes(c)).length)
      .filter(n => n > 0);
    const need = doorSizes.reduce((s, n) => s + n - 1, 0);
    const have = GRID.join('').split('+').length - 1;
    if (have < need) {
      console.log('🔴 조각이 모자란다 — ' + need + '개가 필요한데 ' + have + '개다');
      process.exit(1);
    }
    console.log('🟢 조각 ' + have + '개 / 필요 ' + need + '개');
  }

  const count = ch => GRID.join('').split(ch).length - 1;
  console.log('조각 + : ' + count('+') + '   무중력 : : ' + count(':') + '   상자 B : ' + count('B') +
              '   문벽 1 : ' + count('1') + '   별 o : ' + count('o'));
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
