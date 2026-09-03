// 못(pool)에 있는 판의 **어려움**을 재서 흩어진 정도를 본다.
//
// 🔴 왜 이게 필요한가 (2026-09-04):
//    판을 고르는 기준이 `best`(걸음 수) 하나뿐이었다. 난이도 곡선이 사실 **길이 곡선**이다.
//    외길(tight) 로 다시 고르려는데 — **못에 고를 게 있는지부터** 봐야 한다.
//    다 4% 대면 다시 골라도 소용없다. 그때는 생성기를 고쳐야 한다.
//
//    ⚠️ 위키 함정: *"최단해가 유일한 판만 골랐다고 했는데, 고를 게 없어서 유일했다."*
//       고르기 전에 **분포**를 본다.
//
//   node tools/poolstat.js [못파일] [나갈파일]
'use strict';
const fs = require('fs');
const path = require('path');
const HARD = path.join(__dirname, 'hard.js');

// hard.js 는 실행 스크립트라 require 하면 판 전체를 재버린다.
// measure 만 꺼내 쓰려고 소스에서 잘라 온다.
const src = fs.readFileSync(HARD, 'utf8');
const cut = src.indexOf('const fs = require');
const mod = { exports: {} };
new Function('module', 'exports', 'require', '__dirname',
             src.slice(0, cut) + '\nmodule.exports = { measure, winnable };')
  (mod, mod.exports, require, __dirname);
const { measure } = mod.exports;

const file = process.argv[2] || 'tools/make40-pool.json';
//  🔴 나갈 파일을 고를 수 있게 한다. 고정이면 덤 조각 **전** 측정값을
//     덮어써 짝비교를 못 하게 된다 (09-05).
const OUT = process.argv[3] || process.env.OUT || 'tools/poolstat.json';
const raw = JSON.parse(fs.readFileSync(file, 'utf8'));
const pool = Array.isArray(raw) ? raw : (raw.levels || raw.pool || []);
console.log(file, '—', pool.length, '개');

const rows = [];
let skipped = 0;
for (let i = 0; i < pool.length; i++) {
  const p = pool[i];
  const m = measure({ grid: p.grid, gravity: true, clear: p.clear || 'all', id: p.id || ('#' + i) });
  if (!m) { skipped++; continue; }
  rows.push({ i, doors: p.doors, w: p.grid[0].length, h: p.grid.length, ...m });
  //  🔴 **중간에 저장한다.** trapall 이 180/213 에서 오류 한 줄 없이 죽었다 (09-05).
  //     재는 일은 더 비싸니 맨 끕에서 한 번만 쓰면 안 된다.
  if ((i + 1) % 20 === 0) {
    fs.writeFileSync(OUT, JSON.stringify(rows, null, 1) + '\n');
    process.stderr.write('  ' + (i + 1) + '/' + pool.length + '\n');
  }
}
console.log('잰 판', rows.length, '· 못 잰 판', skipped);

const pc = (a, q) => {
  const s = a.slice().sort((x, y) => x - y);
  return s[Math.min(s.length - 1, Math.floor(s.length * q))];
};
const show = (name, get, mul) => {
  const a = rows.map(get).map(v => v * (mul || 1));
  console.log(name.padEnd(8) +
    ' 최소 ' + pc(a, 0).toFixed(1).padStart(6) +
    ' · 중앙 ' + pc(a, 0.5).toFixed(1).padStart(6) +
    ' · 상위25% ' + pc(a, 0.75).toFixed(1).padStart(6) +
    ' · 상위10% ' + pc(a, 0.90).toFixed(1).padStart(6) +
    ' · 최대 ' + pc(a, 1).toFixed(1).padStart(6));
};
show('압박%', r => r.press, 100);   // 🔴 이게 진짜 조이는 정도다
show('외길%', r => r.tight, 100);
show('되짚기%', r => r.back, 100);
show('걸음', r => r.moves);
show('상태', r => r.states);

// 🔴 핵심 질문 — **고를 게 있나**
for (const t of [20, 30, 40, 50, 60]) {
  const n = rows.filter(r => r.press * 100 >= t).length;
  console.log('압박 ' + String(t).padStart(2) + '% 이상: ' + String(n).padStart(3) + '개' +
              (n >= 47 ? '   ← 47판을 채울 수 있다' : ''));
}
fs.writeFileSync(OUT, JSON.stringify(rows, null, 1) + '\n');
console.log('\n' + '본 값을 ' + OUT + '에 남겼다');
