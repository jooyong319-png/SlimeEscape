// 판이 **얼마나 커도 읽히나**를 잰다 (2026-09-05).
//
// 🔴 사장님(09-04): "카메라 이동 없이 한 화면에서만 할 수 있으면 좋겠다."
//    그러면 판을 키울 수 있는 한계는 **한 칸이 화면에서 몇 픽셀이냐**로 정해진다.
//
// `SnakeController.AimCamera` 의 식을 그대로 옮겼다. 어긋나면 여기가 거짓말이 된다 —
// 고칠 때 두 군데를 같이 본다.
//
//   node tools/fit.js                  화면별 한계
//   node tools/fit.js levels           지금 판 자료가 어디에 걸리는지
'use strict';
const fs = require('fs');
const path = require('path');

const MARGIN = 0.9;      // AimCamera: 시범이 아닐 때 여백
const FLOOR  = 30;       // AimCamera: 한 칸이 30픽셀 밑이면 읽기를 택한다

/// 한 칸이 화면에서 몇 픽셀인가
function cellPx(w, h, sw, sh) {
  const asp = sw / sh;
  const size = Math.max(h * 0.5 + MARGIN, (w * 0.5 + MARGIN) / asp);
  return sh / (2 * size);
}

const SCREENS = [
  ['itch 창 (작게)',   960,  600],
  ['itch 창 (크게)',  1280,  720],
  ['노트북 전체',      1920, 1080],
  ['폰 세로',           720, 1280],
  ['폰 세로 (큰 것)',   1080, 2400],
  ['태블릿 세로',      1536, 2048],
];

if (process.argv[2] === 'levels') {
  const LP = path.join(__dirname, '../game/Assets/Resources/levels.json');
  const d = JSON.parse(fs.readFileSync(LP, 'utf8'));
  console.log('판 ' + d.levels.length + '개 — 화면마다 한 칸이 몇 픽셀인가 (' + FLOOR + ' 밑이면 🔴)');
  console.log('');
  const head = '판       크기   ' + SCREENS.map(s => s[0].padStart(14)).join('');
  console.log(head);
  console.log('-'.repeat(head.length));
  let worst = null;
  for (const l of d.levels) {
    const w = l.grid[0].length, h = l.grid.length;
    const px = SCREENS.map(s => cellPx(w, h, s[1], s[2]));
    const min = Math.min(...px);
    if (!worst || min < worst.min) worst = { id: l.id, w, h, min };
    if (min >= FLOOR) continue;                  // 걸리는 판만 보여준다
    console.log(l.id.padEnd(8) + (w + 'x' + h).padStart(7) + '   ' +
      px.map(v => v.toFixed(0).padStart(11) + (v < FLOOR ? ' 🔴' : '   ')).join(''));
  }
  console.log('-'.repeat(head.length));
  console.log('제일 빡빡한 판: ' + worst.id + ' (' + worst.w + 'x' + worst.h + ') — ' +
              worst.min.toFixed(0) + '픽셀');
  process.exit(0);
}

// 화면마다 "한 칸 30픽셀"을 지키는 최대 판 크기
console.log('한 칸 ' + FLOOR + '픽셀을 지키는 최대 판 크기');
console.log('');
console.log('화면'.padEnd(18) + '해상도'.padEnd(13) + '가로만 클 때   세로만 클 때   정사각에 가깝게');
console.log('-'.repeat(74));
for (const [name, sw, sh] of SCREENS) {
  //  세로 12칸 고정하고 가로를 늘려본다 / 가로 20칸 고정하고 세로를 늘려본다 / 3:2 로 같이 늘린다
  const grow = (step) => {
    let w = 6, h = 6, last = [w, h];
    for (let i = 0; i < 200; i++) {
      const [nw, nh] = step(w, h);
      if (cellPx(nw, nh, sw, sh) < FLOOR) break;
      w = nw; h = nh; last = [w, h];
    }
    return last[0] + 'x' + last[1];
  };
  const wide = grow((w, h) => [w + 1, 12]);
  const tall = grow((w, h) => [20, h + 1]);
  const even = grow((w, h) => [w + 3, h + 2]);
  console.log(name.padEnd(18) + (sw + 'x' + sh).padEnd(13) +
              wide.padEnd(15) + tall.padEnd(15) + even);
}
console.log('');
console.log('🔴 폰 세로가 제일 빡빡하다 — **가로 칸 수**가 먼저 걸린다.');
console.log('   플레이스토어·앱스토어가 목표라면 판 가로를 여기에 맞춰야 한다.');
