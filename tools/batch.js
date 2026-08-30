// 1묶음 16판을 뽑는다. 오래 걸리니 뒤에서 돌린다.
//
// 🔴 사장님 지시 (08-31): **가로로 더 길게**, **판마다 기믹을 더**.
//    - 가로로 길게: 26x12 로 짓고 쓰는 만큼 잘라낸다 (화면비 2 근처)
//    - 기믹 더:     홈을 둘이 아니라 **셋**까지 · 먹으면 안 되는 조각 · 꺾인 홈 · 별
//
// 홈이 셋이면 "몸을 세 번 두고 간다" — 두고 온 몸이 디딤돌이 되는 게 두 번 일어난다.
'use strict';
const fs = require('fs');
const { execFileSync } = require('child_process');
const path = require('path');

const OUT = path.join(__dirname, 'batch.txt');
fs.writeFileSync(OUT, '');

// 난이도 띠 — 앞은 작고 쉽게, 뒤는 크고 무겁게
const BANDS = [
  { tag: 'A', W: 20, H: 10, D1: 4, D2: 3, D3: 0, EXTRA: 0, CARVE: 0.34, LO: 18, HI: 34, want: 3 },
  { tag: 'B', W: 22, H: 11, D1: 5, D2: 3, D3: 0, EXTRA: 1, CARVE: 0.34, LO: 28, HI: 48, want: 4 },
  { tag: 'C', W: 24, H: 11, D1: 5, D2: 4, D3: 0, EXTRA: 2, CARVE: 0.33, LO: 40, HI: 70, want: 4 },
  { tag: 'D', W: 26, H: 12, D1: 5, D2: 3, D3: 3, EXTRA: 1, CARVE: 0.33, LO: 45, HI: 90, want: 3 },
  { tag: 'E', W: 26, H: 12, D1: 6, D2: 4, D3: 0, EXTRA: 2, CARVE: 0.32, LO: 55, HI: 110, want: 2 },
];

let total = 0;
for (const b of BANDS) {
  let got = 0;
  for (let seed = 1; seed <= 40 && got < b.want; seed++) {
    const env = Object.assign({}, process.env, {
      W: b.W, H: b.H, D1: b.D1, D2: b.D2, D3: b.D3, EXTRA: b.EXTRA,
      CARVE: b.CARVE, MINDEN: '0.27', MINW: '16', MINH: '8',
      LO: b.LO, HI: b.HI, N: '260', SEED: String(seed * 137), SHOW: '3',
    });
    let out = '';
    try {
      out = execFileSync(process.execPath, ['--max-old-space-size=6144',
        path.join(__dirname, 'dense.js')], { env, encoding: 'utf8', timeout: 120000 });
    } catch (e) { out = (e.stdout || '') + ''; }
    const grids = out.split('\n').filter(l => l.startsWith('["#'));
    for (const g of grids) {
      if (got >= b.want) break;
      fs.appendFileSync(OUT, b.tag + ' ' + g + '\n');
      got++; total++;
    }
    process.stdout.write(`[${b.tag}] seed ${seed} → 모은 판 ${got}/${b.want}\n`);
  }
}
console.log('\n모두 ' + total + '판 → tools/batch.txt');
