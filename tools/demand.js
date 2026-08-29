// 🔴 이 판이 플레이어에게 **무엇을 요구하나**를 센다.
//
//   node tools/demand.js
//
// "어렵다"는 한 덩어리 말이라 쓸모가 없다. 갈라야 고칠 수 있다.
// 정답 수순을 재생하면서 각 요구가 몇 번 나오는지 센다.
//
//   낙하      떨어지는 걸음이 몇 번인가
//   낙하먹기  떨어지면서 조각을 먹는가 (2026-08-29에 생긴 규칙)
//   세우기    몸이 세로로 몇 칸까지 서는가  ← 이 게임의 제일 큰 동사
//   되짚기    갔던 방향을 뒤집는 걸음 (앞으로만 가면 0)
//   꺾임      목표 모양이 몇 번 꺾이나 (0이면 '바닥에 일자로 눕기')
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const FILE = path.join(__dirname, '..', 'game', 'Assets', 'Resources', 'levels.json');
const doc = JSON.parse(fs.readFileSync(FILE, 'utf8'));
const DIRS = { '↑': [0, -1], '↓': [0, 1], '←': [-1, 0], '→': [1, 0] };

/// 목표 칸을 사슬 순서로 잇고 몇 번 꺾이는지
function bends(L) {
  const set = new Set(L.target);
  const nb = c => [c - L.w, c + L.w, c - 1, c + 1].filter(k => set.has(k));
  const ends = L.target.filter(c => nb(c).length === 1);
  if (ends.length !== 2) return null;              // 단순한 줄이 아니다 (덩어리)
  const out = [ends[0]]; const seen = new Set(out);
  while (out.length < L.target.length) {
    const n = nb(out[out.length - 1]).find(k => !seen.has(k));
    if (n === undefined) return null;
    out.push(n); seen.add(n);
  }
  let b = 0;
  for (let i = 1; i + 1 < out.length; i++)
    if (out[i] - out[i - 1] !== out[i + 1] - out[i]) b++;
  return b;
}

console.log('판    걸음  낙하  낙하먹기  세우기  되짚기  꺾임');
for (const j of doc.levels) {
  const L = E.parse({ grid: j.grid, gravity: doc.gravity !== false });
  let st = E.startState(L);
  let falls = 0, fallEats = 0, tallest = 1, turns = 0, prev = null;

  for (const ch of j.sol) {
    const d = DIRS[ch];
    if (!d) continue;
    const di = ['↑', '↓', '←', '→'].indexOf(ch);
    const before = st;
    const stepped = before.body[0] + d[0] + d[1] * L.w;
    const ns = E.step(L, st, di);
    if (!ns) break;

    // 떨어졌나 — 걸음만 했을 때보다 아래에 있으면 낙하
    const fell = ((ns.body[0] - stepped) / L.w) | 0;
    if (fell > 0) falls++;
    // 낙하 중에 먹었나 — 머리가 들어간 칸이 아닌 조각이 새로 켜졌으면
    const newly = ns.fm & ~before.fm;
    if (newly) {
      const headFi = L.foodIdx.get(stepped);
      const headBit = headFi === undefined ? 0 : (1 << headFi);
      if (newly & ~headBit) fallEats++;
    }
    // 되짚기 — 방향을 정반대로 뒤집었나
    if (prev !== null && d[0] === -DIRS[prev][0] && d[1] === -DIRS[prev][1]) turns++;
    prev = ch;

    st = ns;
    const ys = st.body.map(c => (c / L.w) | 0);
    const h = Math.max(...ys) - Math.min(...ys) + 1;
    if (h > tallest) tallest = h;
  }

  const b = bends(L);
  console.log(
    j.id.padEnd(5) + String(j.best).padStart(5) + String(falls).padStart(6) +
    String(fallEats).padStart(9) + String(tallest).padStart(8) +
    String(turns).padStart(7) + String(b === null ? '덩어리' : b).padStart(7)
  );
}
