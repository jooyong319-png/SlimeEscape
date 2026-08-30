// 🔴 **거꾸로 만든다.** 아무렇게나 짓고 풀리길 바라지 않는다.
//
// 지금까지는 "지형을 아무렇게나 깔고 → 풀리는지 본다"였다. 홈이 늘수록 확률이 곤두박질쳐서
// 홈 셋은 65판을 지어도 0판이 풀렸다 (08-31).
//
// 대신 **풀리는 과정을 먼저 만들고, 그걸 판으로 굳힌다**:
//   1. 굴을 판다
//   2. 슬라임을 아무렇게나 걷게 한다
//   3. 몸이 길어져야 하면 → **지금 들어가려는 칸에 조각을 놓는다**
//   4. 몸이 알맞은 길이가 되면 → **지금 몸이 덮은 자리를 홈으로 굳힌다**
//   5. 홈 개수만큼 되풀이
//
// 이렇게 하면 만들어진 순간 이미 풀린다. 솔버는 "얼마나 어려운가"만 본다.
'use strict';
const E = require('./engine.js').SlimeEngine;

function rng(seed) {
  let s = (seed | 0) || 1;
  return () => { s ^= s << 13; s |= 0; s ^= s >>> 17; s ^= s << 5; s |= 0;
                 return ((s >>> 0) % 100000) / 100000; };
}

const GLY = [['=', '*'], ['-', '%'], ['~', '@']];

// 🔴 지형 모양은 **여러 가지**여야 한다 (08-31 사장님).
//    다 같은 방식으로 파면 판이 마흔 개라도 다 비슷하게 생긴다.
//    아래 다섯 가지를 골고루 쓴다 — 굴 · 선반 · 기둥 · 방 · 계단.
const SHAPES = ['cave', 'shelf', 'pillar', 'rooms', 'stair'];

function blank(W, H) {
  const g = [];
  for (let y = 0; y < H; y++) g.push(new Array(W).fill('#'));
  return g;
}
const dig = (g, open, W, x, y) => {
  if (g[y][x] === '#') { g[y][x] = '.'; open.add(y * W + x); }
};

/// 굴 — 아무렇게나 헤집는다. 울퉁불퉁하고 자연스럽다.
function shapeCave(g, open, r, W, H, want) {
  const starts = 3 + Math.floor(r() * 3);
  for (let s = 0; s < starts; s++) {
    let cx = 1 + Math.floor(((s + 0.5) / starts + (r() - 0.5) * 0.25) * (W - 2));
    let cy = 1 + Math.floor(r() * (H - 2));
    cx = Math.max(1, Math.min(W - 2, cx));
    for (let k = 0; k < want * 8 && open.size < want; k++) {
      dig(g, open, W, cx, cy);
      const u = r();
      const nx = cx + (u < 0.26 ? -1 : u < 0.52 ? 1 : 0);
      const ny = cy + (u < 0.52 ? 0 : u < 0.76 ? -1 : 1);
      if (nx > 0 && nx < W - 1 && ny > 0 && ny < H - 1) { cx = nx; cy = ny; }
    }
  }
}

/// 선반 — 가로로 긴 층이 여러 겹. 오르내림이 뚜렷하다.
function shapeShelf(g, open, r, W, H, want) {
  const rows = [];
  for (let y = H - 2; y >= 2; y -= 2) rows.push(y);
  for (const y of rows) {
    const x0 = 1 + Math.floor(r() * Math.max(1, (W - 2) * 0.35));
    const len = Math.floor((W - 2) * (0.45 + r() * 0.45));
    for (let x = x0; x < Math.min(W - 1, x0 + len); x++) { dig(g, open, W, x, y); dig(g, open, W, x, y - 1); }
  }
  // 층끼리 잇는 구멍
  for (let i = 0; i + 1 < rows.length; i++) {
    const x = 1 + Math.floor(r() * (W - 2));
    for (let y = rows[i + 1]; y <= rows[i]; y++) dig(g, open, W, x, y);
  }
  while (open.size < want * 0.8) {
    const x = 1 + Math.floor(r() * (W - 2)), y = 1 + Math.floor(r() * (H - 2));
    dig(g, open, W, x, y);
  }
}

/// 기둥 — 세로 돌기둥 사이를 다닌다. 좁고 답답한 맛.
function shapePillar(g, open, r, W, H, want) {
  for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) dig(g, open, W, x, y);
  const cols = 2 + Math.floor(r() * Math.max(1, (W - 4) / 3));
  for (let i = 0; i < cols; i++) {
    const x = 2 + Math.floor(r() * (W - 4));
    const top = 1 + Math.floor(r() * (H - 4));
    const len = 2 + Math.floor(r() * (H - top - 2));
    for (let y = top; y < Math.min(H - 1, top + len); y++) {
      if (g[y][x] === '.') { g[y][x] = '#'; open.delete(y * W + x); }
    }
  }
}

/// 방 — 네모난 방 몇 개를 복도로 잇는다. 사람이 지은 것 같은 느낌.
function shapeRooms(g, open, r, W, H, want) {
  const boxes = [];
  const n = 2 + Math.floor(r() * 3);
  for (let i = 0; i < n; i++) {
    const rw = 3 + Math.floor(r() * 5), rh = 2 + Math.floor(r() * 3);
    const x0 = 1 + Math.floor(r() * Math.max(1, W - 2 - rw));
    const y0 = 1 + Math.floor(r() * Math.max(1, H - 2 - rh));
    for (let y = y0; y < Math.min(H - 1, y0 + rh); y++)
      for (let x = x0; x < Math.min(W - 1, x0 + rw); x++) dig(g, open, W, x, y);
    boxes.push([x0 + (rw >> 1), y0 + (rh >> 1)]);
  }
  for (let i = 0; i + 1 < boxes.length; i++) {
    let [x, y] = boxes[i]; const [tx, ty] = boxes[i + 1];
    while (x !== tx) { x += x < tx ? 1 : -1; dig(g, open, W, x, y); }
    while (y !== ty) { y += y < ty ? 1 : -1; dig(g, open, W, x, y); }
  }
}

/// 계단 — 한쪽에서 반대쪽으로 층층이 올라간다.
function shapeStair(g, open, r, W, H, want) {
  const leftUp = r() < 0.5;
  const steps = Math.max(2, Math.min(H - 3, Math.floor((W - 2) / 3)));
  for (let i = 0; i < steps; i++) {
    const y = H - 2 - i;
    if (y < 1) break;
    const wide = Math.floor((W - 2) * (1 - i / (steps + 1)));
    const x0 = leftUp ? 1 : Math.max(1, W - 1 - wide);
    for (let x = x0; x < Math.min(W - 1, x0 + wide); x++) { dig(g, open, W, x, y); dig(g, open, W, x, y - 1); }
  }
}

/// 굴을 판다 — 모양을 골라서
function carve(r, W, H, ratio, shape) {
  const g = blank(W, H);
  const want = Math.floor((W - 2) * (H - 2) * ratio);
  const open = new Set();
  const pick = shape || SHAPES[Math.floor(r() * SHAPES.length)];
  ({ cave: shapeCave, shelf: shapeShelf, pillar: shapePillar,
     rooms: shapeRooms, stair: shapeStair }[pick])(g, open, r, W, H, want);
  if (open.size < 12) return null;
  // 시작점에서 못 닿는 주머니는 메운다
  const first = [...open][0];
  const seen = new Set([first]), q = [first];
  while (q.length) {
    const c = q.pop(), x = c % W, y = (c / W) | 0;
    for (const [nx, ny] of [[x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]]) {
      const n = ny * W + nx;
      if (nx > 0 && nx < W - 1 && ny > 0 && ny < H - 1 && g[ny][nx] !== '#' && !seen.has(n))
        { seen.add(n); q.push(n); }
    }
  }
  for (let y = 1; y < H - 1; y++)
    for (let x = 1; x < W - 1; x++)
      if (g[y][x] !== '#' && !seen.has(y * W + x)) g[y][x] = '#';
  return g;
}

/// 바깥의 통돌을 잘라낸다 — 벽 한 겹만 남긴다
function crop(rows) {
  const H = rows.length, W = rows[0].length;
  let x0 = W, x1 = -1, y0 = H, y1 = -1;
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++)
    if (rows[y][x] !== '#') {
      if (x < x0) x0 = x; if (x > x1) x1 = x;
      if (y < y0) y0 = y; if (y > y1) y1 = y;
    }
  if (x1 < 0) return rows;
  x0 = Math.max(0, x0 - 1); x1 = Math.min(W - 1, x1 + 1);
  y0 = Math.max(0, y0 - 1); y1 = Math.min(H - 1, y1 + 1);
  const out = [];
  for (let y = y0; y <= y1; y++) out.push(rows[y].slice(x0, x1 + 1));
  return out;
}

/// 지금 격자로 판을 다시 읽고, "놓은 조각은 다 먹은 것"으로 상태를 맞춘다
function relevel(rows, body, dm, exceptCell) {
  const L = E.parse({ grid: rows, gravity: true, clear: 'all', id: 'g' });
  let fm = 0;
  for (let i = 0; i < L.foods.length; i++) if (L.foods[i] !== exceptCell) fm |= 1 << i;
  return { L, st: { body: body.slice(), fm, pg: 0, dm, sc: 0, pm: 0 } };
}

/// 판 한 장을 **자라게** 만든다
function grow(r, W, H, doorLens, ratio, wanderMin, wanderMax, shape) {
  const g = carve(r, W, H, ratio, shape);
  if (!g) return null;
  const rows = () => g.map(row => row.join(''));

  // 시작점 — 바닥에 붙은 칸
  const floors = [];
  for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++)
    if (g[y][x] === '.' && g[y + 1][x] === '#') floors.push(y * W + x);
  if (floors.length < 6) return null;
  const start = floors[Math.floor(r() * floors.length)];
  g[(start / W) | 0][start % W] = 'S';

  let cur = relevel(rows(), [start], 0, -1);
  let body = cur.st.body, dm = 0;

  for (let di = 0; di < doorLens.length; di++) {
    const want = doorLens[di];
    const wander = wanderMin + Math.floor(r() * (wanderMax - wanderMin + 1));
    let steps = 0, guard = 0;

    // 몸을 want 칸까지 키우면서 굴 속을 돌아다닌다
    while (guard++ < 900) {
      const needGrow = body.length < want;
      const doneWander = steps >= wander;
      if (!needGrow && doneWander) break;

      // 갈 수 있는 방향을 모은다
      const opts = [];
      for (let d = 0; d < 4; d++) {
        const ns = E.step(cur.L, cur.st, d);
        if (ns) opts.push([d, ns]);
      }
      if (!opts.length) return null;

      if (needGrow && r() < 0.8) {
        // 🔴 들어가려는 칸에 조각을 놓는다 — 그 자리에서 몸이 자란다.
        //    방향을 섞어 다 훑어본다. 한 번 실패하고 포기하면 완성률이 확 떨어진다.
        const order = opts.slice();
        for (let k = order.length - 1; k > 0; k--) {
          const j = Math.floor(r() * (k + 1)); [order[k], order[j]] = [order[j], order[k]];
        }
        let grew = false;
        for (const [d] of order) {
          const [dx, dy] = E.DIRS[d];
          const hx = body[0] % W + dx, hy = ((body[0] / W) | 0) + dy;
          if (hx <= 0 || hy <= 0 || hx >= W - 1 || hy >= H - 1) continue;
          if (g[hy][hx] !== '.') continue;
          g[hy][hx] = '+';
          const cell = hy * W + hx;
          const trial = relevel(rows(), body, dm, cell);
          const ns = E.step(trial.L, trial.st, d);
          if (!ns) { g[hy][hx] = '.'; continue; }
          cur = trial; cur.st = ns; body = ns.body; steps++; grew = true; break;
        }
        if (grew) continue;
        // 어디에도 못 놓겠으면 그냥 한 걸음 걷는다
        const [, ns] = opts[Math.floor(r() * opts.length)];
        cur.st = ns; body = ns.body; steps++;
      } else {
        const [, ns] = opts[Math.floor(r() * opts.length)];
        cur.st = ns; body = ns.body; steps++;
      }
      if (body.length > want) return null;      // 너무 자랐다
    }
    if (body.length !== want) return null;

    // 🔴 지금 몸이 덮은 자리를 홈으로 굳힌다 — 이 순간 이 홈은 반드시 채울 수 있다
    const [wall, core] = GLY[di];
    for (const c of body) {
      const x = c % W, y = (c / W) | 0;
      if (g[y][x] !== '.') return null;
      g[y][x] = wall;
    }
    g[(body[0] / W) | 0][body[0] % W] = core;

    dm |= 1 << di;
    body = [body[0]];
    cur = relevel(rows(), body, dm, -1);
    // 홈이 열리면 발밑이 바뀔 수 있다 — 떨어뜨려 자리를 맞춘다
    const settled = E.settle(cur.L, body.slice(), cur.st.fm, 0, dm, 0, 0);
    if (!settled) return null;
    body = settled.body;
    cur.st.body = body;
  }
  return crop(rows());
}

module.exports = { grow, carve, crop, rng, SHAPES };

// 혼자 돌릴 때만 아래를 실행한다 (다른 도구가 require 해 쓸 수 있게)
if (require.main === module) {
  // ---- 돌리기 ----
  const W = +(process.env.W || 17), H = +(process.env.H || 10);
  const LENS = (process.env.LENS || '4,3').split(',').map(Number);
  const RATIO = +(process.env.RATIO || 0.42);
  const WMIN = +(process.env.WMIN || 4), WMAX = +(process.env.WMAX || 14);
  const LO = +(process.env.LO || 14), HI = +(process.env.HI || 200);
  const N = +(process.env.N || 300), SEED = +(process.env.SEED || 1);
  const SHOW = +(process.env.SHOW || 2);

  const r = rng(SEED);
  const out = [];
  let built = 0, solved = 0;
  for (let i = 0; i < N; i++) {
    const grid = grow(r, W, H, LENS, RATIO, WMIN, WMAX);
    if (!grid) continue;
    built++;
    const a = E.solve({ grid, gravity: true, clear: 'all', id: 'g' + i });
    if (!a.ok) continue;                       // 굳힌 뒤 지형이 바뀌어 막힐 수도 있다
    solved++;
    if (a.moves < LO || a.moves > HI) continue;
    if (a.shortest !== 1) continue;
    out.push({ grid, moves: a.moves, states: a.states });
  }
  out.sort((a, b) => b.moves - a.moves);
  console.log(`지은 판 ${built} · 풀림 ${solved} · 쓸 만함 ${out.length}  (${N}번 시도)`);
  for (const f of out.slice(0, SHOW)) {
    console.log('─'.repeat(36));
    console.log(`${f.grid[0].length}x${f.grid.length} · ${f.moves}걸음 · 상태 ${f.states}`);
    console.log(f.grid.join('\n'));
    console.log(JSON.stringify(f.grid));
  }

}
