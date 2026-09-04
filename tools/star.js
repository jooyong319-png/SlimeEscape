// 별 자리를 박고 커트라인을 잡는다.
//
// 🔴 왜 따로 뺐나 (2026-09-05):
//    `putStar` 가 make40.js 안에 갇혀 있어서 pick.js 가 판을 골라놓고도
//    별을 못 박았다. 못 만들기(make40)와 판 고르기(pick)가 **같은 규칙**을
//    써야 하니 한 군데에 둔다.
'use strict';
const E = require('./engine.js').SlimeEngine;

/// 별 자리 — 칸마다 다시 푸는 건 너무 느리다. 몇 개만 뽑아 보고 충분하면 멈춘다.
/// grid 에 'o' 를 하나 얹은 판과, 별을 먹고 도는 최단(moves), 돌아가는 값(det)을 준다.
function putStar(grid, base, r, tries, goodEnough, cap) {
  const G = grid.map(row => row.split(''));
  const H = G.length, W = G[0].length;
  const cells = [];
  for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++)
    if (G[y][x] === '.') cells.push([x, y]);
  for (let i = cells.length - 1; i > 0; i--) {
    const j = Math.floor(r() * (i + 1)); [cells[i], cells[j]] = [cells[j], cells[i]];
  }
  let best = null;
  for (const [x, y] of cells.slice(0, tries)) {
    const g2 = grid.map(row => row.split(''));
    g2[y][x] = 'o';
    const gg = g2.map(row => row.join(''));
    //  🔴 **상한을 건다** (09-05 밤). 별을 못 가는 자리에 놓으면 솔버가 판 전체를
    //     다 뒤진 뒤에야 "안 된다"고 한다 — 긴 판에선 그게 수십 초다.
    //     마흔 자리를 시험하니 한 판에 몇 분이 갔다. 넘으면 그냥 다음 자리로 간다.
    const s = E.solve({ grid: gg, gravity: true, clear: 'all', id: 's' },
                      cap ? { needStar: true, cap } : { needStar: true });
    if (!s.ok) continue;
    const det = s.moves - base;
    if (!best || det > best.det) best = { grid: gg, det, moves: s.moves };
    if (best.det >= goodEnough) break;
  }
  return best;
}

// 🔴 커트는 **별을 먹은 최단(bestStar)** 기준이다. 별 없는 최단(best)으로
//    잡으면 별을 주우러 도는 걸음이 빠져서 3별이 아예 불가능해진다
//    (2026-09-04: 그렇게 잡았다가 47판 중 34판이 불가능해졌다).
function cutOf(bestStar) {
  return bestStar + Math.max(2, Math.round(bestStar * 0.10));
}

module.exports = { putStar, cutOf };
