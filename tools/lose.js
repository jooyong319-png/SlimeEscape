// 🔴 "언제 지는지, 그리고 진 걸 얼마나 오래 모르는지"를 판마다 훑어본다.
//
//   node tools/lose.js
//
// 게임은 진 걸 알려주지 않기로 했다(2026-08-28). 그래서 wander(진 뒤 헤맬 수 있는 걸음)가
// 곧 플레이어가 헛되이 쓰는 시간이고, 그건 **판 설계로만** 줄일 수 있다.
// 지표 계산은 tools/metrics.js 한 벌만 쓴다.
const fs = require('fs');
const path = require('path');
const { analyze } = require('./metrics.js');

const FILE = path.join(__dirname, '..', 'game', 'Assets', 'Resources', 'levels.json');
const doc = JSON.parse(fs.readFileSync(FILE, 'utf8'));

for (const j of doc.levels) {
  const a = analyze({ grid: j.grid, gravity: doc.gravity !== false });
  if (!a) { console.log(j.id.padEnd(10) + " 너무 커서 못 잼"); continue; }
  console.log(
    j.id.padEnd(10) +
    ' 최단 ' + String(j.best).padStart(3) + '걸음' +
    ' · 이미 진 상태 ' + (a.lost * 100).toFixed(1).padStart(5) + '%' +
    ' · 빠르면 ' + String(a.earliest === null ? '-' : a.earliest).padStart(3) + '걸음에 진다' +
    ' · 진 뒤 ' + String(a.wander).padStart(3) + '걸음 헤맴' + (a.wander > 14 ? '  🔴' : '')
  );
}
