// 🔴 **어려움으로 고른다** (2026-09-04).
//
// 지금까지는 `best`(걸음 수) 하나로 골랐다 — 난이도 곡선이 사실 **길이 곡선**이었다.
// 이제 **압박(press)** 을 함께 본다.
//
//   node tools/pick.js                   골라서 보여만 준다
//   WRITE=1 node tools/pick.js           levels.json 에 쓴다
//
// ⚠️ 자료를 쓰기 전에 사본을 뜬다 (tools/levels.bak.json).
'use strict';
const fs = require('fs');

const STAT = 'tools/poolstat.json';      // poolstat.js 가 남긴 잰 값
const POOL = 'tools/hard-pool.json';
const OUT  = 'game/Assets/Resources/levels.json';
const N    = +(process.env.N || 40);     // 튜토리얼 뒤로 몇 판

const stat = JSON.parse(fs.readFileSync(STAT, 'utf8'));
const pool = JSON.parse(fs.readFileSync(POOL, 'utf8'));
console.log('잰 판', stat.length, '· 못', pool.length);

// 🔴 **곡선은 여전히 있어야 한다.** 1-1 이 어려우면 아무도 2판까지 못 간다.
//    앞은 쉽고 짧게, 뒤로 갈수록 조이고 길게.
//    두 값을 **함께** 본다 — 하나만 보면 또 엉뚱한 걸 재게 된다.
const rows = stat.map(r => Object.assign({}, r, pool[r.i]))
                 .filter(r => r.grid && r.moves >= 8);

// 띠마다 "이 정도 압박 · 이 정도 길이" 를 목표로 잡고 제일 가까운 것을 집는다
const want = [];
for (let k = 0; k < N; k++) {
  const t = k / (N - 1);                       // 0 → 1
  want.push({
    press: 0.03 + 0.22 * t * t,                // 3% → 25% (뒤에서 가파르게)
    moves: 10 + 55 * t,                        // 10 → 65 걸음
    doors: t < 0.25 ? 1 : t < 0.6 ? 2 : 3,
  });
}

const used = new Set();
const picked = [];
for (const w of want) {
  let best = null, bestD = Infinity;
  for (let i = 0; i < rows.length; i++) {
    if (used.has(i)) continue;
    const r = rows[i];
    //  홈 개수는 되도록 맞추고, 나머지는 거리로
    const dDoor = Math.abs((r.doors || 1) - w.doors) * 0.9;
    const dPress = Math.abs(r.press - w.press) * 4.0;
    const dMoves = Math.abs(r.moves - w.moves) / 60;
    const d = dDoor + dPress + dMoves;
    if (d < bestD) { bestD = d; best = i; }
  }
  if (best === null) break;
  used.add(best);
  picked.push(rows[best]);
}

console.log('\n고른 판', picked.length);
console.log('번호  홈  크기     걸음   압박    되짚기   상태');
console.log('-'.repeat(52));
picked.forEach((r, k) => {
  if (k % 4 !== 0 && k !== picked.length - 1) return;   // 네 판마다 한 줄만
  console.log(String(k + 1).padStart(4) + String(r.doors).padStart(4) +
    ('  ' + r.w + 'x' + r.h).padEnd(9) +
    String(r.moves).padStart(5) +
    (r.press * 100).toFixed(1).padStart(7) + '%' +
    (r.back * 100).toFixed(0).padStart(7) + '%' +
    String(r.states).padStart(9));
});
const avg = f => picked.reduce((s, r) => s + f(r), 0) / picked.length;
console.log('-'.repeat(52));
console.log('평균 압박 ' + (avg(r => r.press) * 100).toFixed(1) + '%' +
            ' · 되짚기 ' + (avg(r => r.back) * 100).toFixed(0) + '%' +
            ' · 걸음 ' + avg(r => r.moves).toFixed(0));

if (process.env.WRITE !== '1') {
  console.log('\n(보여주기만 했다. 쓰려면 WRITE=1)');
  process.exit(0);
}
console.log('\n⚠️ levels.json 에 쓰려면 별·커트라인을 다시 박아야 한다 —');
console.log('   putStar 가 make40.js 안에 있어서 아직 못 쓴다. 다음 걸음.');
