// 🔴 구역 하나를 짓는다 — 설계 연구 2의 결론 + 08-30에 배운 것.
//
//   node tools/region.js [씨앗]
//
// 08-30에 배운 것 (지난 생성기가 못 풀린 이유):
//   🔴 **길이 1인 핵은 한 칸도 못 오른다.** 위로 k칸 = 길이 k+1 이므로 k=1도 길이 2가 필요하다.
//      → 방을 위아래로 놓으면 **아래로만 가는 편도**가 된다. 되돌아올 길이 없다.
//      → 그래서 **방 사이 통로는 전부 바닥 높이(평지)**로 놓는다.
//      → 위아래 이동은 **방 안에서만** 한다. 방 안엔 조각이 있어서 길어질 수 있다.
//
// 그래서 이 구역의 모양:
//   · 방을 한 줄로 늘어놓고 **바닥 높이 통로**로 잇는다 → 핵이 언제나 좌우로 자유롭다
//   · **곁가지**는 위층 방. 입구가 세로라 **그 방에서 길어져야** 올라간다 (= 잠금이 공짜다)
//   · 문벽 하나로 한쪽을 막아, 문을 열어야 나머지 구역이 열리게 한다
const E = require('./engine.js').SlimeEngine;

let seed = (Number(process.argv[2] || 4242) | 0) || 4242;
const rnd = () => {
  seed ^= seed << 13; seed |= 0;
  seed ^= seed >>> 17;
  seed ^= seed << 5;  seed |= 0;
  return ((seed >>> 0) % 1000000) / 1000000;
};
const ri = n => Math.floor(rnd() * n);
const pick = a => a[ri(a.length)];

const RW = 11, RH = 9;        // 방 안쪽 — 🔴 위쪽에 선반을 둘 만큼 높다
const WALL = 1;
// 구역마다 달라지게 — 방 개수와 문 크기를 밖에서 준다
const COLS = Number(process.env.COLS || 3);
const D1 = Number(process.env.D1 || 3);      // 문1 칸 수 (왼쪽 방)
const D2 = Number(process.env.D2 || 4);      // 문2 칸 수 (선반 위)
// 🔴 선반 문은 **문벽 뒤 방**에 둔다. 앞쪽에 두면 문벽을 열어도 빈 방만 나온다.
//    (08-30: 사장님이 "문 열고 오른쪽 갔는데 뭐 없던데?" — 설계 연구 2회차에
//     "막다른 길에는 잠금을 안 건다"고 적어놓고 내가 어겼다)
const LEDGE = Number(process.env.LEDGE || (COLS - 1));
const H = RH + 2 * WALL;
const W = COLS * RW + (COLS + 1) * WALL;

const LOW = WALL;
const bx = i => WALL + i * (RW + WALL);

function build() {
  const g = [];
  for (let y = 0; y < H; y++) g.push(new Array(W).fill('#'));

  // 아래층 방 셋
  for (let i = 0; i < COLS; i++)
    for (let y = 0; y < RH; y++) for (let x = 0; x < RW; x++)
      g[LOW + y][bx(i) + x] = '.';

  // 🔴 통로는 **바닥 높이** — 길이 1로도 걸어서 지난다
  const floorY = LOW + RH - 1;
  for (let i = 0; i + 1 < COLS; i++) {
    const cx = bx(i + 1) - 1;
    g[floorY][cx] = '.';
    g[floorY - 1][cx] = '.';          // 두 칸 높이
  }

  // 🔴 문벽 — 오른쪽 방으로 가는 통로를 막는다. 문1을 열어야 열린다
  const gate = bx(COLS - 1) - 1;
  g[floorY][gate] = '1';
  g[floorY - 1][gate] = '1';

  return { g, floorY };
}

const { g, floorY } = build();
const solid = (y, x) => g[y][x] === '#' || (g[y][x] >= '1' && g[y][x] <= '3');
const ground = (y, x) => g[y][x] === '.' && y + 1 < H && solid(y + 1, x);

// 방 안 선반 — 걸어다니는 맛. 🔴 통로 높이는 안 건드린다
function shelves(x0, y0) {
  for (let k = 0; k < 1 + ri(2); k++) {
    const y = y0 + 1 + ri(RH - 3);
    if (y >= floorY - 1) continue;              // 통로 높이는 비워둔다
    const x = x0 + 1 + ri(RW - 4);
    for (let i = 0; i < 2 + ri(3) && x + i < x0 + RW - 1; i++) g[y][x + i] = '#';
  }
}
for (let i = 0; i < COLS; i++) shelves(bx(i), LOW);

// ---- 문과 조각 ----
// 문1 = 왼쪽 방 (문벽을 연다) · 문2 = 곁가지 방 (구역의 끝)
function placeDoor(x0, y0, n, mark) {
  for (let a = 0; a < 300; a++) {
    const spots = [];
    for (let y = y0; y < y0 + RH; y++) for (let x = x0; x < x0 + RW; x++)
      if (g[y][x] === '.') spots.push([y, x]);
    if (!spots.length) return false;
    const [sy0, sx0] = pick(spots);
    const p = [[sy0, sx0]]; const used = new Set([sy0 + ',' + sx0]);
    while (p.length < n) {
      const [cy, cx] = p[p.length - 1];
      const opts = [];
      for (const [dy, dx] of [[-1, 0], [1, 0], [0, -1], [0, 1]]) {
        const ny = cy + dy, nx = cx + dx;
        if (ny < y0 || ny >= y0 + RH || nx < x0 || nx >= x0 + RW) continue;
        if (g[ny][nx] !== '.' || used.has(ny + ',' + nx)) continue;
        opts.push([ny, nx]);
      }
      if (!opts.length) break;
      const nx2 = pick(opts);
      p.push(nx2); used.add(nx2[0] + ',' + nx2[1]);
    }
    if (p.length !== n) continue;
    // 🔴 문은 **지형에 붙어 있어야** 한다. 공중에 뜬 문은 그 방 조각으로 못 닿는다
    //    (08-30: 바닥에서 5칸 위에 놓여 길이 4로는 못 가는 판이 나왔다).
    //    칸마다 아래가 벽이거나, 아래가 같은 문의 칸이어야 한다 — 즉 바닥에 쌓인 모양만 된다.
    const set = new Set(p.map(([y, x]) => y + ',' + x));
    const stacked = p.every(([y, x]) => ground(y, x) || set.has((y + 1) + ',' + x));
    if (!stacked) continue;
    // 🔴 닿을 수 있는 높이여야 한다. 문을 채울 때 길이는 정확히 n이고,
    //    위로 k칸 오르려면 길이 k+1이므로 **바닥에서 n-1칸 위까지**만 된다.
    //    (08-30: 선반 위에 붙어 검사는 통과했는데 그 선반을 못 올라가는 판이 나왔다)
    if (p.some(([y]) => y < floorY - (n - 1))) continue;
    p.forEach(([y, x], k) => { g[y][x] = k === p.length - 1 ? mark[1] : mark[0]; });
    // 조각 n-1개 — 같은 방 바닥에
    let need = n - 1;
    for (let b = 0; b < 500 && need; b++) {
      const y = y0 + ri(RH), x = x0 + ri(RW);
      if (g[y][x] !== '.' || !ground(y, x)) continue;
      g[y][x] = '+'; need--;
    }
    return need === 0;
  }
  return false;
}

/// 정해진 줄(ly)에 가로로 n칸 문을 놓는다. 조각은 그 방 바닥에.
function placeDoorOn(xa, xb, ly, n, mark) {
  for (let x = xa; x + n - 1 <= xb; x++) {
    let free = true;
    for (let i = 0; i < n; i++) if (g[ly][x + i] !== '.') { free = false; break; }
    if (!free) continue;
    for (let i = 0; i < n; i++) g[ly][x + i] = (i === n - 1) ? mark[1] : mark[0];
    let need = n - 1;
    for (let b = 0; b < 600 && need; b++) {
      const y = LOW + ri(RH), xx = bx(LEDGE) + ri(RW);
      if (g[y][xx] !== '.' || !ground(y, xx)) continue;
      g[y][xx] = '+'; need--;
    }
    return need === 0;
  }
  return false;
}

let ok = placeDoor(bx(0), LOW, D1, ['=', '*']);      // 문1 — 왼쪽 방
// 🔴 문2 — 가운데 방의 **높은 선반** 위. 조각은 같은 방 바닥에 있으니
//    길어진 채로 올라가야 한다. 오르는 건 길이가 필요하고 내려오는 건 공짜다.
{
  const x0 = bx(LEDGE), ly = floorY - (D2 - 1);              // 바닥에서 3칸 위 = 길이 4로 오른다
  // 🔴 선반은 문보다 **양쪽으로 한 칸씩 넓어야** 한다.
  //    문의 심이 오른쪽 끝이라 왼쪽에서 걸어 들어가는데, 그 자리에 발 디딜 데가 없으면 못 채운다
  //    (08-30: COLS=4가 하나도 안 풀린 이유가 이것이었다).
  //    장식 선반이 이 자리를 덮었을 수도 있으니 문 줄은 먼저 비운다.
  for (let x = x0 + 1; x < x0 + 9; x++) { g[ly][x] = '.'; g[ly + 1][x] = '#'; }
  ok = placeDoorOn(x0 + 2, x0 + 7, ly, D2, ['-', '%']) && ok;
}

// 시작 — 가운데 방 (좌우로 다 갈 수 있게)
{
  let placed = false;
  for (let a = 0; a < 500 && !placed; a++) {
    const y = LOW + ri(RH), x = bx(1) + ri(RW);
    if (g[y][x] === '.' && ground(y, x)) { g[y][x] = 'S'; placed = true; }
  }
  if (!placed) ok = false;
}

// 🔴 출구 — 맵을 넘는 자리. 문벽 뒤 방의 **양 끝**에 둔다.
//    왼쪽 출구는 들어온 자리(이전 맵에서 오면 여기서 나온다),
//    오른쪽 출구는 나가는 자리. 둘 다 **바닥 높이**라 길이 1로도 드나든다.
{
  const back = bx(COLS - 1);
  if (g[floorY][back + RW - 1] === '.') g[floorY][back + RW - 1] = '>';
  if (g[floorY][bx(0)] === '.') g[floorY][bx(0)] = '<';
}

// 🔴 문벽 뒤 방에 뭔가 있는지 확인한다. 빈 방으로 이어지면 문을 여는 뜻이 없다
{
  const back = bx(COLS - 1);
  let has = false;
  for (let y = LOW; y < LOW + RH; y++) for (let x = back; x < back + RW; x++)
    if ('=*-%+'.includes(g[y][x])) has = true;
  if (!has) { console.log('🔴 문벽 뒤 방이 비어 있다'); ok = false; }
}

const grid = g.map(r => r.join(''));
console.log(W + 'x' + H + ' · 방 ' + COLS + '개 · ' + (ok ? '배치 성공' : '🔴 배치 실패'));
grid.forEach(r => console.log('  ' + r));

if (!ok) process.exit(1);
let r;
try { r = E.solve({ grid, gravity: true, clear: 'all', id: 'region' }); }
catch (e) { console.log('\n🔴 ' + e.message); process.exit(1); }
console.log('\n' + (r.ok
  ? r.moves + '걸음 · 최단해 ' + r.shortest + '개 · 상태 ' + r.states.toLocaleString()
  : '🔴 ' + r.why));
// 🔴 못 풀리면 종료코드로 알린다. 안 그러면 부르는 쪽이 **이전 판을 그대로 복사**한다
//    (08-30: r2가 r3로, r4가 r5로 베껴졌다)
if (!r.ok || r.shortest !== 1) process.exit(1);
if (r.ok) {
  require('fs').writeFileSync(require('path').join(__dirname, 'region.json'),
    JSON.stringify({ id: 'r1', name: '첫 구역', clear: 'all', best: r.moves, sol: r.path, grid }, null, 2) + '\n');
  console.log('tools/region.json 에 저장');
}
