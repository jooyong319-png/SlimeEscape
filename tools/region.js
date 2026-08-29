// 🔴 구역 하나를 짓는다 — 설계 연구 2의 결론대로.
//
//   node tools/region.js [씨앗]
//
// 연구 결론 (docs/map-study.html):
//   · 모양   : 큰 고리 + 짧은 곁가지                       (2회차)
//   · 잠금   : 통로의 20~30%만. 막다른 길엔 안 건다        (2회차)
//   · 이동   : 방 사이는 **그냥 걸어서** 오간다             (1회차 ①)
//   · 조각   : **문이 있는 방에만.** 통로엔 두지 않는다     (3회차)
//   · 성장   : 방마다 만들 수 있는 최대 길이가 다르다       (3회차 A-5)
//
// 🔴 무작위에 맡기지 않는다. 방을 격자 칸에 놓고 고리로 잇는다 —
//    무작위 그물은 격자처럼 헷갈리기만 한다는 게 2회차 결론이었다.
//
// 내는 것: 판 하나짜리 큰 지도. 문벽('1'~'3')이 방을 가르고,
//          문벽을 다 닫으면 방으로 갈라진다(게임의 카메라가 그걸로 방을 찾는다).
const E = require('./engine.js').SlimeEngine;

let seed = (Number(process.argv[2] || 4242) | 0) || 4242;
const rnd = () => {
  seed ^= seed << 13; seed |= 0;
  seed ^= seed >>> 17;
  seed ^= seed << 5;  seed |= 0;
  return ((seed >>> 0) % 1000000) / 1000000;
};
const ri = n => Math.floor(rnd() * n);

// ---- 방 한 칸의 크기 ----
const RW = 11, RH = 8;          // 방 안쪽
const WALL = 1;                 // 방 사이 벽 두께

/// 방을 격자에 놓는다. 고리 모양 + 곁가지 하나.
///   (0,0)─(1,0)─(2,0)
///     │              │
///   (0,1)          (2,1)──[곁가지]
///     │              │
///   (0,2)─(1,2)─(2,2)
const PLAN = [
  { id: 'a', gx: 0, gy: 0, role: '시작' },
  { id: 'b', gx: 1, gy: 0, role: '길' },
  { id: 'c', gx: 2, gy: 0, role: '문' },
  { id: 'd', gx: 2, gy: 1, role: '길' },
  { id: 'e', gx: 3, gy: 1, role: '곁가지' },
  { id: 'f', gx: 2, gy: 2, role: '문' },
  { id: 'g', gx: 1, gy: 2, role: '길' },
  { id: 'h', gx: 0, gy: 2, role: '문' },
  { id: 'i', gx: 0, gy: 1, role: '길' },
];
// 통로: [방, 방, 잠글까(문 번호) 또는 0]
const LINKS = [
  ['a', 'b', 0], ['b', 'c', 0],
  ['c', 'd', 0], ['d', 'e', 2],     // 🔴 곁가지 입구를 잠근다
  ['d', 'f', 0], ['f', 'g', 0],
  ['g', 'h', 0], ['h', 'i', 1],     // 🔴 고리를 끊는 자리 — 열면 지름길
  ['i', 'a', 0],
];

const COLS = 4, ROWS = 3;
const W = COLS * RW + (COLS + 1) * WALL;
const H = ROWS * RH + (ROWS + 1) * WALL;

const room = id => PLAN.find(r => r.id === id);
const x0 = r => WALL + r.gx * (RW + WALL);
const y0 = r => WALL + r.gy * (RH + WALL);

function build() {
  // 전부 벽으로 채우고 방만 판다
  const g = [];
  for (let y = 0; y < H; y++) g.push(new Array(W).fill('#'));
  for (const r of PLAN)
    for (let y = 0; y < RH; y++) for (let x = 0; x < RW; x++)
      g[y0(r) + y][x0(r) + x] = '.';

  // 통로 뚫기 — 🔴 대부분 그냥 뚫는다. 잠그는 건 일부만
  for (const [A, B, lock] of LINKS) {
    const a = room(A), b = room(B);
    const mark = lock ? String(lock) : '.';
    if (a.gy === b.gy) {                       // 옆으로
      const [l, r] = a.gx < b.gx ? [a, b] : [b, a];
      const cx = x0(r) - 1;
      const cy = y0(l) + RH - 1;               // 바닥 높이로 뚫는다 — 걸어서 지난다
      g[cy][cx] = mark;
      g[cy - 1][cx] = mark;                    // 두 칸 높이 (머리+몸이 지난다)
    } else {                                   // 위아래로
      const [u, d] = a.gy < b.gy ? [a, b] : [b, a];
      const cy = y0(d) - 1;
      const cx = x0(u) + 1 + ri(RW - 3);
      g[cy][cx] = mark;
      g[cy][cx + 1] = mark;
    }
  }
  return g;
}

const g = build();
const solid = (y, x) => g[y][x] === '#';
const ground = (y, x) => g[y][x] === '.' && y + 1 < H && (g[y + 1][x] === '#' || /[1-3]/.test(g[y + 1][x]));

// ---- 방 안 꾸미기 ----
for (const r of PLAN) {
  const bx = x0(r), by = y0(r);
  // 선반 한둘 — 걸어다니는 맛
  for (let k = 0; k < 1 + ri(2); k++) {
    const y = by + 2 + ri(RH - 4);
    const x = bx + 1 + ri(RW - 5);
    for (let i = 0; i < 2 + ri(4) && x + i < bx + RW - 1; i++) g[y][x + i] = '#';
  }
}

// ---- 문과 조각 ----
// 🔴 문이 있는 방에만 조각을 둔다 (3회차). 문 크기 = 그 방이 요구하는 길이
const DOORS = [
  { room: 'h', n: 3, mark: ['=', '*'] },     // 문 1 → 고리를 끊는 문벽을 연다
  { room: 'c', n: 4, mark: ['-', '%'] },     // 문 2 → 곁가지 입구를 연다
];

function placeDoor(d) {
  const r = room(d.room), bx = x0(r), by = y0(r);
  for (let a = 0; a < 200; a++) {
    const spots = [];
    for (let y = by; y < by + RH; y++) for (let x = bx; x < bx + RW; x++)
      if (g[y][x] === '.') spots.push([y, x]);
    if (!spots.length) return false;
    const [sy, sx] = spots[ri(spots.length)];
    const p = [[sy, sx]]; const used = new Set([sy + ',' + sx]);
    while (p.length < d.n) {
      const [cy, cx] = p[p.length - 1];
      const opts = [];
      for (const [dy, dx] of [[-1, 0], [1, 0], [0, -1], [0, 1]]) {
        const ny = cy + dy, nx = cx + dx;
        if (ny < by || ny >= by + RH || nx < bx || nx >= bx + RW) continue;
        if (g[ny][nx] !== '.' || used.has(ny + ',' + nx)) continue;
        opts.push([ny, nx]);
      }
      if (!opts.length) break;
      const nx2 = opts[ri(opts.length)];
      p.push(nx2); used.add(nx2[0] + ',' + nx2[1]);
    }
    if (p.length !== d.n) continue;
    if (!p.some(([y, x]) => ground(y, x))) continue;
    p.forEach(([y, x], k) => { g[y][x] = k === p.length - 1 ? d.mark[1] : d.mark[0]; });
    // 조각 n-1개 — 같은 방 바닥에
    let need = d.n - 1;
    for (let b = 0; b < 400 && need; b++) {
      const y = by + ri(RH), x = bx + ri(RW);
      if (g[y][x] !== '.' || !ground(y, x)) continue;
      g[y][x] = '+'; need--;
    }
    return need === 0;
  }
  return false;
}

let ok = true;
for (const d of DOORS) if (!placeDoor(d)) ok = false;

// ---- 시작 자리 ----
{
  const r = room('a'), bx = x0(r), by = y0(r);
  let placed = false;
  for (let a = 0; a < 400 && !placed; a++) {
    const y = by + ri(RH), x = bx + ri(RW);
    if (g[y][x] === '.' && ground(y, x)) { g[y][x] = 'S'; placed = true; }
  }
  if (!placed) ok = false;
}

const grid = g.map(r => r.join(''));
console.log(W + 'x' + H + ' · 방 ' + PLAN.length + '개 · 통로 ' + LINKS.length +
            '개 (잠긴 것 ' + LINKS.filter(l => l[2]).length + '개) · ' + (ok ? '배치 성공' : '🔴 배치 실패'));
console.log('');
grid.forEach(r => console.log('  ' + r));

if (ok) {
  console.log('');
  let r;
  try { r = E.solve({ grid, gravity: true, clear: 'all', id: 'region' }); }
  catch (e) { console.log('🔴 ' + e.message); process.exit(1); }
  console.log(r.ok
    ? r.moves + '걸음 · 최단해 ' + r.shortest + '개 · 상태 ' + r.states.toLocaleString()
    : '🔴 ' + r.why);
  if (r.ok) {
    require('fs').writeFileSync(require('path').join(__dirname, 'region.json'),
      JSON.stringify({ id: 'r1', name: '첫 구역', clear: 'all', best: r.moves, sol: r.path, grid }, null, 2) + '\n');
    console.log('tools/region.json 에 저장');
  }
}
