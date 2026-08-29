// 🔴 21방을 짜는 도구. 판을 "요구 프로필"로 주문한다.
//
//   node tools/plan.js <초> <씨앗> <WxH> <주문>
//   예) node tools/plan.js 40 7 12x9 "fall=2,tall<=3,back=0,eat=0"
//
// 왜 이렇게 하나 (2026-08-29):
//   b1이 어려웠던 건 한 가지가 어려워서가 아니라 **다섯 개를 한꺼번에 요구해서**였다.
//   그래서 원칙을 세웠다 — 🔴 **판 하나에 새 요구는 하나씩.**
//   그러려면 판을 "어려운 순"이 아니라 **"이 요구를 이만큼 쓰는 것"**으로 골라야 한다.
//
// 요구 (tools/demand.js와 같은 정의)
//   fall  떨어지는 걸음 수
//   eat   떨어지면서 조각을 먹는 횟수
//   tall  몸이 세로로 서는 최대 칸 수
//   back  갔던 방향을 뒤집는 걸음 수 (사람이 제일 안 떠올리는 수)
//   bend  목표 모양이 꺾이는 횟수 (덩어리면 99)
//   moves 최단 걸음 수
//
// 🔴 "이미 진 상태"는 안 쓴다. 사람이 5초에 푼 판이 그 지표에서 48%였다.
//    비싸기만 하고 안 맞는다 (2026-08-28~29).
const E = require('./engine.js').SlimeEngine;
const check = require('./check.js');

const SEC = Number(process.argv[2] || 30);
let seed = (Number(process.argv[3] || 12345) | 0) || 12345;
const SIZE = (process.argv[4] || '12x9').split('x').map(Number);
const ORDER = process.argv[5] || '';
const W = SIZE[0], H = SIZE[1];

const rnd = () => {
  seed ^= seed << 13; seed |= 0;
  seed ^= seed >>> 17;
  seed ^= seed << 5;  seed |= 0;
  return ((seed >>> 0) % 1000000) / 1000000;
};
const ri = n => Math.floor(rnd() * n);

// ---- 주문 해석 : "fall=2,tall<=3,moves>=20" ----
const WANT = ORDER.split(',').map(s => s.trim()).filter(Boolean).map(s => {
  const m = s.match(/^(\w+)\s*(<=|>=|=)\s*(\d+)$/);
  if (!m) { console.error('주문을 못 읽겠다: ' + s); process.exit(1); }
  return { key: m[1], op: m[2], val: Number(m[3]) };
});
const fits = d => WANT.every(w => {
  const v = d[w.key];
  if (v === undefined) { console.error('모르는 요구: ' + w.key); process.exit(1); }
  return w.op === '=' ? v === w.val : w.op === '<=' ? v <= w.val : v >= w.val;
});

// ---- 판 만들기 ----
const blank = () => {
  const g = [];
  for (let y = 0; y < H; y++) {
    let r = '';
    for (let x = 0; x < W; x++) r += (y === 0 || y === H - 1 || x === 0 || x === W - 1) ? '#' : '.';
    g.push(r.split(''));
  }
  return g;
};
const solid = (g, y, x) => g[y][x] === '#';
const ground = (g, y, x) => !solid(g, y, x) && y + 1 < H && solid(g, y + 1, x);

function make(nMin, nMax) {
  const g = blank();
  const shelves = 1 + ri(Math.max(1, Math.round(W / 5)));
  for (let i = 0; i < shelves; i++) {
    const y = 2 + ri(Math.max(1, H - 4));
    const x = 1 + ri(Math.max(1, W - 4));
    const len = 2 + ri(Math.max(1, Math.round(W / 3)));
    for (let k = 0; k < len && x + k < W - 1; k++) g[y][x + k] = '#';
  }
  for (let i = 0; i < ri(3); i++) {
    const x = 2 + ri(Math.max(1, W - 4)), y0 = 2 + ri(Math.max(1, H - 5));
    const len = 2 + ri(Math.max(1, Math.round(H / 3)));
    for (let k = 0; k < len && y0 + k < H - 1; k++) g[y0 + k][x] = '#';
  }

  const stand = [];
  for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++)
    if (ground(g, y, x)) stand.push([y, x]);
  if (stand.length < 10) return null;

  // 목표 = 지지되는 칸에서 뻗는, 자기를 안 밟는 경로
  const n = nMin + ri(Math.max(1, nMax - nMin + 1));
  let path = null;
  for (let a = 0; a < 25 && !path; a++) {
    const [sy, sx] = stand[ri(stand.length)];
    const p = [sy * W + sx]; const used = new Set(p);
    while (p.length < n) {
      const c = p[p.length - 1], cy = (c / W) | 0, cx = c % W;
      const opts = [];
      for (const [dy, dx] of [[-1, 0], [1, 0], [0, -1], [0, 1]]) {
        const ny = cy + dy, nx = cx + dx;
        if (ny < 1 || nx < 1 || ny >= H - 1 || nx >= W - 1) continue;
        if (solid(g, ny, nx) || used.has(ny * W + nx)) continue;
        opts.push(ny * W + nx);
      }
      if (!opts.length) break;
      const pick = opts[ri(opts.length)];
      p.push(pick); used.add(pick);
    }
    if (p.length === n && p.some(c => ground(g, (c / W) | 0, c % W))) path = p;
  }
  if (!path) return null;

  const tset = new Set(path);
  const coreAt = ri(2) ? 0 : path.length - 1;
  path.forEach((c, k) => { g[(c / W) | 0][c % W] = k === coreAt ? '*' : '='; });

  const free = stand.filter(([y, x]) => !tset.has(y * W + x));
  if (free.length < n + 1) return null;
  for (let i = free.length - 1; i > 0; i--) { const j = ri(i + 1); [free[i], free[j]] = [free[j], free[i]]; }
  g[free[0][0]][free[0][1]] = 'S';
  for (let i = 1; i < n; i++) g[free[i][0]][free[i][1]] = '+';
  return g.map(r => r.join(''));
}

// ---- 요구 세기 : 정답 수순을 재생하면서 ----
const DIRS = [[0, -1], [0, 1], [-1, 0], [1, 0]];
function demands(grid, sol) {
  const L = E.parse({ grid, gravity: true });
  let st = E.startState(L);
  let fall = 0, eat = 0, tall = 1, back = 0, prev = -1;
  const syms = ['↑', '↓', '←', '→'];
  for (const ch of sol) {
    const di = syms.indexOf(ch);
    if (di < 0) continue;
    const stepped = st.body[0] + DIRS[di][0] + DIRS[di][1] * L.w;
    const before = st;
    const ns = E.step(L, st, di);
    if (!ns) return null;
    if (((ns.body[0] - stepped) / L.w) | 0) fall++;
    const newly = ns.fm & ~before.fm;
    if (newly) {
      const hf = L.foodIdx.get(stepped);
      if (newly & ~(hf === undefined ? 0 : (1 << hf))) eat++;
    }
    if (prev >= 0 && DIRS[di][0] === -DIRS[prev][0] && DIRS[di][1] === -DIRS[prev][1]) back++;
    prev = di;
    st = ns;
    const ys = st.body.map(c => (c / L.w) | 0);
    tall = Math.max(tall, Math.max(...ys) - Math.min(...ys) + 1);
  }
  // 목표 꺾임
  const set = new Set(L.target);
  const nb = c => [c - L.w, c + L.w, c - 1, c + 1].filter(k => set.has(k));
  const ends = L.target.filter(c => nb(c).length === 1);
  let bend = 99;
  if (ends.length === 2) {
    const o = [ends[0]]; const seen = new Set(o);
    while (o.length < L.target.length) {
      const k = nb(o[o.length - 1]).find(v => !seen.has(v));
      if (k === undefined) break;
      o.push(k); seen.add(k);
    }
    if (o.length === L.target.length) {
      bend = 0;
      for (let i = 1; i + 1 < o.length; i++) if (o[i] - o[i - 1] !== o[i + 1] - o[i]) bend++;
    }
  }
  return { fall, eat, tall, back, bend };
}

// ---- 돌리기 ----
const NMIN = Number(process.env.NMIN || 4), NMAX = Number(process.env.NMAX || 6);
const t0 = process.hrtime.bigint();
const el = () => Number(process.hrtime.bigint() - t0) / 1e9;

let tried = 0, solved = 0;
const hits = [];
while (el() < SEC) {
  const grid = make(NMIN, NMAX);
  tried++;
  if (!grid || !check(grid).ok) continue;
  let r; try { r = E.solve({ grid, gravity: true }); } catch (e) { continue; }
  if (!r.ok || r.shortest !== 1) continue;            // 정답은 하나뿐이어야 한다
  solved++;
  const d = demands(grid, r.path);
  if (!d) continue;
  d.moves = r.moves;
  if (!fits(d)) continue;
  if (hits.some(h => h.grid.join('') === grid.join(''))) continue;
  hits.push({ grid, d, path: r.path });
  if (hits.length >= 8) break;
}

console.log(`${W}x${H} · ${el().toFixed(0)}초 · ${tried}판 시도 → ${solved}판 풀림 → 주문에 맞는 것 ${hits.length}개`);
console.log(`주문: ${ORDER || '(없음 — 아무거나)'}\n`);
hits.forEach((h, i) => {
  const d = h.d;
  console.log(`--- ${i + 1}  ${d.moves}걸음 · 낙하 ${d.fall} · 낙하먹기 ${d.eat} · 세우기 ${d.tall} · 되짚기 ${d.back} · 꺾임 ${d.bend === 99 ? '덩어리' : d.bend}`);
  h.grid.forEach(r => console.log('   ' + r));
});
if (!hits.length) console.log('없다. 주문을 느슨하게 하거나 판을 키울 것.');
