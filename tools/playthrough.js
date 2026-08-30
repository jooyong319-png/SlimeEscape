// 🔴 처음부터 끝까지 이어지는가 — 한 판 통째로 걸어본다.
//
//   node tools/playthrough.js
//
// 판 하나씩 검증하는 것(stamp)만으로는 **길이 끊긴 것**을 못 잡는다.
// next1/next2 를 따라가면서 확인한다:
//   · 그 방이 풀리는가 · 최단해가 하나인가
//   · 획이 제때 모이는가 · needMarks 가 있는 문 앞에서 막히지 않는가
//   · 마지막 방에 실제로 닿는가 · 고리가 있어 무한히 도는 건 아닌가
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const FILE = path.join(__dirname, '..', 'game', 'Assets', 'Resources', 'levels.json');
const doc = JSON.parse(fs.readFileSync(FILE, 'utf8'));
const byId = new Map(doc.levels.map(l => [l.id, l]));

let cur = doc.levels[0];
const seen = new Set();
let marks = 0, totalMoves = 0, rooms = 0, steps = 0;
const trail = [];
let fail = 0;

console.log('방   구역          걸음   방수  획');
while (cur) {
  if (seen.has(cur.id)) { console.log('🔴 고리다 — ' + cur.id + ' 를 다시 만났다'); fail++; break; }
  seen.add(cur.id);
  trail.push(cur.id);

  const def = { grid: cur.grid, gravity: doc.gravity !== false, clear: cur.clear || 'any', id: cur.id };
  let r;
  try { r = E.solve(def); } catch (e) { console.log('🔴 ' + cur.id + ': ' + e.message); fail++; break; }
  if (!r.ok) { console.log('🔴 ' + cur.id + ': ' + r.why); fail++; break; }
  if (r.shortest !== 1) { console.log('⚠️  ' + cur.id + ': 최단해가 ' + r.shortest + '개'); fail++; }

  const n = cur.id === 'fin' ? 1 : Math.round((cur.grid[0].length - 1) / 12);
  rooms += n; totalMoves += r.moves; steps++;
  if (cur.mark) marks++;

  console.log(String(steps).padStart(2) + '   ' + cur.id.padEnd(5) + (cur.name || '').padEnd(9) +
              String(r.moves).padStart(5) + '걸음' + String(n).padStart(5) + '방' + String(marks).padStart(4));

  const nextId = cur.next1 || cur.next2 || '';
  if (!nextId) break;
  const nx = byId.get(nextId);
  if (!nx) { console.log('🔴 ' + cur.id + ' 의 다음 방 "' + nextId + '" 이 없다'); fail++; break; }

  // 🔴 획이 모자라면 그 문은 안 열린다 — 여기서 막히면 끝까지 못 간다
  if (nx.needMarks && marks < nx.needMarks) {
    console.log('🔴 ' + nx.id + ' 앞에서 막힌다 — 획이 ' + marks + '개인데 ' + nx.needMarks + '개가 필요하다');
    fail++; break;
  }
  cur = nx;
}

console.log('');
console.log('지나온 길: ' + trail.join(' › '));
console.log('합계 ' + rooms + '방 · ' + totalMoves + '걸음 · 획 ' + marks + '개');

// 🔴 시간 어림 — 어제 사장님 기록에서 뽑은 값이다. 사람이 해봐야 정확해진다
const perMove = 0.9;          // 걸음당 초 (생각하는 시간 포함, b판 기록에서)
const flounder = 2.5;         // 실제 걸음 / 최단 걸음 (b판 기록 1.3~4.0의 가운데)
const est = totalMoves * flounder * perMove / 60;
console.log('어림 ' + est.toFixed(0) + '분 (걸음당 ' + perMove + '초 · 최단의 ' + flounder + '배로 잡고)');
console.log('🔴 이건 어림이다. 처음 하는 사람은 더 걸리고, 아는 사람은 덜 걸린다');

if (fail) { console.log('\n🔴 끊긴 데가 ' + fail + '군데 있다'); process.exit(1); }
console.log('\n🟢 처음부터 끝까지 이어진다');
