// 🔴 판을 **내용대로** 굴에 다시 나눈다 (09-23).
//    생성기는 기믹 없는 판뿐 아니라 얼음·금 간 바닥 판에도 스위치를 얹는다. 그래서 'switch' 로만
//    이름 붙여 굴 9 에 몰아넣었더니, 굴 9(「뒤바뀌는 바닥」) 안에 스위치+얼음이 절반이었다.
//    사장님 구성표: 9 새 기믹만 · 10 기믹+조합 · 11 몸 둘만 · 12 몸 둘+조합.
//
//    · 본편(1-*)은 손대지 않는다.
//    · 굴이 30판을 넘으면 뒤쪽(14~)에 같은 갈래의 굴을 새로 연다.
//    · gim 은 격자에서 **다시 읽는다** — 판 고르기 표(id)는 안 건드린다.
//   node tools/rechapter.js
'use strict';
const fs = require('fs');
const path = require('path');
const P = path.join(__dirname, '..', 'game', 'Assets', 'Resources', 'levels.json');
const src = fs.readFileSync(P, 'utf8');
const eol = src.includes('\r\n') ? '\r\n' : '\n';
const doc = JSON.parse(src);

/// 격자에 실제로 무엇이 들어 있나
function gimOf(grid) {
  const t = grid.join('');
  const on = [];
  if (t.includes('B')) on.push('box');
  if (t.includes('I')) on.push('ice');
  if (t.includes('X')) on.push('crack');
  if (/[PQpq]/.test(t)) on.push('portal');
  const sw = /[!45]/.test(t), tw = t.includes('Z');
  return { base: on, sw, tw, name: (sw ? ['switch'] : []).concat(tw ? ['twin'] : []).concat(on).join('+') || 'plain' };
}

//  기믹 없는/한 가지 판의 집 (사장님 구성표 1~8·13)
const HOME = { '': 1, box: 2, ice: 3, 'box+ice': 4, crack: 5, 'crack+ice': 6, 'box+crack': 6,
               portal: 7, 'box+portal': 8, 'ice+portal': 8, 'crack+portal': 8 };

function homeOf(g) {
  if (g.tw) return g.base.length ? 12 : 11;      // 몸 둘 · 몸 둘+조합
  if (g.sw) return g.base.length ? 10 : 9;       // 새 기믹 · 기믹+조합
  return HOME[g.base.join('+')] !== undefined ? HOME[g.base.join('+')] : 13;
}

const main = doc.levels.filter(l => /^1-/.test(l.id));
const rest = doc.levels.filter(l => !/^1-/.test(l.id));
//  집마다 굴 하나로 시작해 30판씩 채운다
const bucket = {};
main.forEach(l => (bucket[l.chapter] = bucket[l.chapter] || []).push(l));
let extra = 14;
const spill = {};      // 집 -> [넘친 굴들]
let moved = 0;
for (const l of rest.sort((a, b) => a.best - b.best)) {
  const g = gimOf(l.grid);
  if (l.gim !== g.name) l.gim = g.name;
  const home = homeOf(g);
  const chain = [home, ...(spill[home] || [])];
  let dest = chain.find(c => (bucket[c] || []).length < 30);
  if (dest === undefined) { dest = extra++; (spill[home] = spill[home] || []).push(dest); bucket[dest] = []; }
  if (l.chapter !== dest) moved++;
  l.chapter = dest;
  (bucket[dest] = bucket[dest] || []).push(l);
}
const txt = JSON.stringify(doc, null, 2).split('\n').join(eol) + eol;
JSON.parse(txt);
fs.writeFileSync(P, txt);
const count = {};
doc.levels.forEach(l => count[l.chapter] = (count[l.chapter] || 0) + 1);
console.log('굴을 옮긴 판 ' + moved + ' · 굴 ' + Math.max(...doc.levels.map(l => l.chapter)) + '개');
for (const [home, chs] of Object.entries(spill))
  console.log('  굴 ' + home + ' 넘침 → ' + chs.join(', '));
