// 🔴 **제일 흉악한 판 하나**를 찾는다 (2026-09-05 밤).
//
// 사장님: *"너가 생각했을 때 이건 진짜 못풀겠따 싶은 정도의 판 하나 만들어봐."*
//
// ⚠️ 내가 잴 수 있는 건 **숫자**지 "사람이 못 푼다" 가 아니다. 그건 사람이 해봐야 안다.
//    그래서 여기서는 **셋을 한꺼번에 밀어붙인 판**을 찾는다:
//      ① 길다        — 앞을 멀리 내다봐야 한다
//      ② 압박이 높다  — 갈림길에서 다른 수가 거의 다 진다
//      ③ 덤 조각이 많다 — 먹으면 안 되는 조각. **틀린 걸 늦게 안다**
//
//    ③ 이 제일 중요하다고 본다. 한 수 틀리고 바로 막히면 되돌리기 한 번이지만,
//    한 수 틀리고 열 걸음을 더 갈 수 있으면 **뭘 잘못했는지 모른다.**
//
//   node tools/brutal.js
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { grow, rng, SHAPES } = require('./grow.js');
const { putStar, cutOf } = require('./star.js');

const src = fs.readFileSync(path.join(__dirname, 'hard.js'), 'utf8');
const mod = { exports: {} };
new Function('module', 'exports', 'require', '__dirname',
             src.slice(0, src.indexOf('const fs = require')) +
             '\nmodule.exports = { measure };')(mod, mod.exports, require, __dirname);
const { measure } = mod.exports;

const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };
const solve = (g, o) => E.solve({ grid: g, gravity: true, clear: 'all', id: 'b' },
                                Object.assign({ cap: 800000 }, o || {}));

/// 정답이 안 지나가는, 정답 길 **바로 옆** 칸에 덤 조각을 최대한 얹는다
function addTraps(grid, want) {
  const base = solve(grid);
  if (!base.ok) return null;
  const L = E.parse({ grid, gravity: true, clear: 'all', id: 't' });
  const walked = new Set();
  let st = E.startState(L);
  for (const c of st.body) walked.add(c);
  for (const ch of base.path) {
    const d = SYM[ch]; if (d === undefined) continue;
    const ns = E.step(L, st, d); if (!ns) break;
    const from = st.body[0], to = ns.body[0];
    if ((to - from) % L.w === 0 && to !== from) {
      const s = to > from ? L.w : -L.w;
      for (let c = from; c !== to; c += s) walked.add(c);
    }
    for (const c of ns.body) walked.add(c);
    st = ns;
  }
  const G = grid.map(r => r.split(''));
  const H = G.length, W = G[0].length;
  const cap = Math.min(800000, base.states * 3 + 40000);
  let put = 0, tried = 0;
  for (let y = 1; y < H - 1 && put < want && tried < 40; y++)
    for (let x = 1; x < W - 1 && put < want && tried < 40; x++) {
      if (G[y][x] !== '.') continue;
      const c = y * W + x;
      if (walked.has(c)) continue;
      let near = false;
      for (const n of [c - 1, c + 1, c - W, c + W]) if (walked.has(n)) near = true;
      if (!near) continue;
      tried++;
      G[y][x] = '+';
      const a = solve(G.map(r => r.join('')), { cap });
      if (a.ok && a.moves === base.moves && a.shortest === 1) put++;
      else G[y][x] = '.';
    }
  return put ? { grid: G.map(r => r.join('')), traps: put } : null;
}

const N = +(process.env.N || 900);
const BANDS = [
  { W: 19, H: 12, ratio: 0.34, wmin: 40, wmax: 62, lens: [4, 4, 3] },
  { W: 21, H: 12, ratio: 0.36, wmin: 46, wmax: 70, lens: [5, 4, 3] },
  { W: 17, H: 13, ratio: 0.32, wmin: 40, wmax: 60, lens: [4, 4, 4] },
  { W: 21, H: 11, ratio: 0.38, wmin: 50, wmax: 74, lens: [5, 4, 4] },
];

let best = null, seen = 0, ok = 0;
for (let seed = 1; seed <= N; seed++) {
  const b = BANDS[seed % BANDS.length];
  const r = rng(seed * 7919 + 31);
  const shape = SHAPES[Math.floor(r() * SHAPES.length)];
  const g0 = grow(r, b.W, b.H, b.lens, b.ratio, b.wmin, b.wmax, shape, 0, 0, 0);
  if (!g0) continue;
  seen++;
  const a0 = solve(g0);
  //  🔴 재려면 상태가 80만 밑이어야 한다. 그 위는 압박을 못 재서 견줄 수가 없다.
  if (!a0.ok || a0.shortest !== 1 || a0.moves < 34) continue;

  const t = addTraps(g0, 6);
  if (!t) continue;
  const a = solve(t.grid);
  if (!a.ok || a.shortest !== 1) continue;
  const m = measure({ grid: t.grid, gravity: true, clear: 'all', id: 'b' });
  if (!m) continue;
  ok++;

  //  흉악함 = 길이 × 압박 × 덤 조각. 셋 다 높아야 높아진다 — 하나만 커서는 안 된다.
  const score = m.moves * (0.05 + m.press) * (1 + t.traps * 0.25);
  if (!best || score > best.score) {
    best = { score, grid: t.grid, traps: t.traps, m, moves: a.moves, sol: a.path };
    process.stderr.write('  후보 ' + m.moves + '걸음 · 압박 ' + (m.press * 100).toFixed(1) +
                         '% · 덤 ' + t.traps + ' · 점수 ' + score.toFixed(1) + '\n');
  }
}

console.log('지은 판 ' + seen + ' · 잰 판 ' + ok);
if (!best) { console.log('🔴 못 찾았다'); process.exit(1); }

//  별을 박는다
const st = putStar(best.grid, best.moves, rng(7), 40, 12,
                   Math.min(800000, best.m.states * 3 + 50000));
const grid = st ? st.grid : best.grid;
const a2 = solve(grid);
const s2 = st ? solve(grid, { needStar: true }) : null;

console.log('');
console.log(grid.join('\n'));
console.log('');
console.log('크기 ' + grid[0].length + 'x' + grid.length +
            ' · 걸음 ' + a2.moves + (s2 && s2.ok ? ' · 별 먹고 ' + s2.moves + ' · 커트 ' + cutOf(s2.moves) : ''));
console.log('압박 ' + (best.m.press * 100).toFixed(1) + '% · 외길 ' + (best.m.tight * 100).toFixed(1) +
            '% · 되짚기 ' + (best.m.back * 100).toFixed(0) + '%');
console.log('덤 조각 ' + best.traps + '개 · 뒤져야 하는 경우의 수 ' + best.m.states.toLocaleString());
const mins = b => b * (1.5 + b / 15) / 60;
console.log('어림 ' + mins(a2.moves).toFixed(1) + '분 (별까지 ' +
            (s2 && s2.ok ? mins(s2.moves).toFixed(1) : '-') + '분)');
fs.writeFileSync('tools/brutal.json', JSON.stringify({
  grid, best: a2.moves, sol: a2.path,
  bestStar: s2 && s2.ok ? s2.moves : a2.moves,
  cut: cutOf(s2 && s2.ok ? s2.moves : a2.moves),
  press: +best.m.press.toFixed(4), traps: best.traps, states: best.m.states,
}, null, 1) + '\n');
console.log('→ tools/brutal.json');
