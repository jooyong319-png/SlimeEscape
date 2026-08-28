// 🔴 판마다 "무엇이 실제로 일하고 있나"를 진단한다.
//
//   node tools/theme.js
//
// 월드를 테마로 나누려면(1-x는 이 성질, 2-x는 저 성질) 먼저 **그 성질이 판에서
// 진짜 제약으로 작동하는지** 알아야 한다. 이름만 붙는 테마는 테마가 아니다.
//
// 방법은 무중력 구역을 잴 때와 같다 — **빼보고 판이 깨지는지 본다.**
//   심   : 심을 목표의 반대쪽 끝으로 옮겨도 그대로 풀리면 → 심은 장식이다
//   중력 : 중력을 끄고도 같은 걸음 수로 풀리면 → 중력은 장식이다
//   꺾임 : 목표 경로가 몇 번 꺾이나 (0이면 '바닥에 일자로 눕기')
//   낙하 : 시작 높이와 목표 높이의 차 — 떨어져야만 하는 판인가
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const FILE = path.join(__dirname, '..', 'game', 'Assets', 'Resources', 'levels.json');
const doc = JSON.parse(fs.readFileSync(FILE, 'utf8'));
const GRAV = doc.gravity !== false;

/// 목표 칸들을 사슬 순서로 잇는다 (몸이 눕는 순서). 못 이으면 null
function chain(cells, W) {
  const set = new Set(cells);
  const nb = c => [c - W, c + W, c - 1, c + 1].filter(k => set.has(k));
  const ends = cells.filter(c => nb(c).length === 1);
  if (ends.length !== 2) return null;                 // 갈래가 있거나 고리다
  const out = [ends[0]]; const seen = new Set(out);
  while (out.length < cells.length) {
    const next = nb(out[out.length - 1]).find(k => !seen.has(k));
    if (next === undefined) return null;
    out.push(next); seen.add(next);
  }
  return out;
}

const solve = grid => { try { return E.solve({ grid, gravity: GRAV }); } catch (e) { return { ok: false }; } };

for (const j of doc.levels) {
  const W = j.grid[0].length;
  const flat = j.grid.join('');
  const cells = [];
  for (let i = 0; i < flat.length; i++) if (flat[i] === '=' || flat[i] === '*') cells.push(i - Math.floor(i / (W + 0)) * 0 );
  // 문자열을 줄 단위로 다시 훑는 게 안전하다
  cells.length = 0;
  j.grid.forEach((row, y) => row.split('').forEach((ch, x) => { if (ch === '=' || ch === '*') cells.push(y * W + x); }));
  const core = (() => { let c = -1; j.grid.forEach((row, y) => row.split('').forEach((ch, x) => { if (ch === '*') c = y * W + x; })); return c; })();
  const startY = (() => { let v = -1; j.grid.forEach((row, y) => { if (row.includes('S')) v = y; }); return v; })();

  const order = chain(cells, W);
  let turns = 0;
  if (order) for (let i = 1; i + 1 < order.length; i++) {
    const a = order[i] - order[i - 1], b = order[i + 1] - order[i];
    if (a !== b) turns++;
  }

  // 심을 반대쪽 끝으로 옮겨본다
  let coreWorks = '?';
  if (order && order.length > 1) {
    const other = order[0] === core ? order[order.length - 1] : order[0];
    if (other !== core) {
      const g2 = j.grid.map(r => r.split(''));
      g2[Math.floor(core / W)][core % W] = '=';
      g2[Math.floor(other / W)][other % W] = '*';
      const r2 = solve(g2.map(r => r.join('')));
      coreWorks = !r2.ok ? '일한다(반대쪽은 못 푼다)'
        : r2.moves !== j.best ? `일한다(반대쪽은 ${r2.moves}걸음)`
          : '🔴 장식(반대쪽도 똑같다)';
    }
  }

  // 중력을 꺼본다
  let gravWorks = '-';
  if (GRAV) {
    let r3; try { r3 = E.solve({ grid: j.grid, gravity: false }); } catch (e) { r3 = { ok: false }; }
    gravWorks = !r3.ok ? '일한다(끄면 못 푼다)'
      : r3.moves !== j.best ? `일한다(끄면 ${r3.moves}걸음)`
        : '🔴 장식(꺼도 똑같다)';
  }

  const targetY = Math.floor(cells[0] / W);
  console.log(
    j.id.padEnd(7) + ' ' + j.grid[0].length + 'x' + j.grid.length +
    ' · 꺾임 ' + (order ? turns : '?') + '번' +
    ' · 낙하 ' + (targetY - startY) + '층' +
    ' · 심 ' + coreWorks +
    ' · 중력 ' + gravWorks
  );
}
