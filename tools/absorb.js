// 🔴 생성기가 뽑아 둔 판 무더기를 게임 판(levels.json)에 **겹치는 것 없이** 넣는다 (09-23).
//    굴은 기믹별 집(스위치 9 · 둘 11)에서 시작해 30판씩 채우고, 넘치면 뒤쪽 굴을 새로 연다.
//    이미 있는 판과 격자가 같으면 안 넣는다. 여러 번 돌려도 된다(생성기가 도는 중에도).
//   node tools/absorb.js
'use strict';
const fs = require('fs');
const path = require('path');
const P = path.join(__dirname, '..', 'game', 'Assets', 'Resources', 'levels.json');
const src = fs.readFileSync(P, 'utf8');
const eol = src.includes('\r\n') ? '\r\n' : '\n';
const doc = JSON.parse(src);
const norm = g => g.join('|');
const known = new Set(doc.levels.map(l => norm(l.grid)));
const bucket = {};
doc.levels.forEach(l => (bucket[l.chapter] = bucket[l.chapter] || []).push(l));
let extra = Math.max(...doc.levels.map(l => l.chapter)) + 1;
let serial = 1 + Math.max(100, ...doc.levels.map(l => { const m = /-(\d+)$/.exec(l.id); return m ? +m[1] : 0; }));
//  기믹 -> 첫 굴 · 그 기믹의 무더기 파일들
//  🔴 파일 이름을 손으로 적지 않는다 — 씨앗마다 새 파일이 늘어나는데
//     목록을 안 고쳐서 새 판 20개를 통째로 빠뜨린 적이 있다 (09-23).
const pick = re => fs.readdirSync(__dirname).filter(f => re.test(f));
const jobs = [
  ['switch', 9, pick(/^switch-pool\d*\.json$/)],
  ['twin', 11, pick(/^twin-.*pool\d*\.json$/)],
];
const added = {};
let dup = 0;
for (const [kind, home, files] of jobs) {
  const pool = [];
  for (const f of files) {
    const fp = path.join(__dirname, f);
    if (fs.existsSync(fp)) { try { pool.push(...JSON.parse(fs.readFileSync(fp, 'utf8'))); } catch (e) { } }
  }
  for (const p of pool.sort((a, b) => a.best - b.best)) {
    if (known.has(norm(p.grid))) { dup++; continue; }
    known.add(norm(p.grid));
    //  같은 기믹이 이미 쓰는 굴 중 자리가 남은 데로
    const homes = [home, ...Object.keys(bucket).map(Number)
      .filter(c => c > 13 && bucket[c][0] && (bucket[c][0].gim || '').startsWith(kind))];
    let dest = homes.find(c => (bucket[c] || []).length < 30);
    if (dest === undefined) { dest = extra++; bucket[dest] = []; }
    const lv = { id: kind + '-' + (serial++), chapter: dest, grid: p.grid, clear: p.clear,
                 best: p.best, sol: p.sol, bestStar: p.best, cut: p.best + 3,
                 lost: 0, tight: 0, backtrack: 0, states: p.states || 0, press: -1, traps: 0,
                 gim: p.gim || kind, review: true };
    (bucket[dest] = bucket[dest] || []).push(lv);
    doc.levels.push(lv);
    added[lv.gim] = (added[lv.gim] || 0) + 1;
  }
}
const txt = JSON.stringify(doc, null, 2).split('\n').join(eol) + eol;
JSON.parse(txt);
fs.writeFileSync(P, txt);
console.log('겹쳐서 뺌 ' + dup + '판');
Object.entries(added).forEach(([k, v]) => console.log('  ' + k.padEnd(12) + v));
console.log('합계 ' + doc.levels.length + '판 · 굴 ' + Math.max(...doc.levels.map(l => l.chapter)));
//  🔴 넣고 나면 **내용대로 굴을 다시 나눈다** — 스위치+얼음 판이 굴 9(스위치만)에 섞이면 안 된다
require('child_process').execFileSync(process.execPath, [path.join(__dirname, 'rechapter.js')],
                                      { stdio: 'inherit' });
