// 🔴 연결된 맵을 만든다 — 문이 길을 여는 판.
//
//   node tools/mapgen.js [초] [씨앗] [칸방수]
//
// 구조를 무작위에 맡기지 않는다. **칸방(chamber)을 문벽으로 나눈다.**
//
//   칸방0 │문벽1│ 칸방1 │문벽2│ 칸방2
//
//   칸방 i 안에 : 문 i+1 의 홈  +  그 문을 채우는 데 필요한 조각
//   문 i+1 을 열면 문벽 i+1 이 사라져 다음 칸방으로 간다
//
// 이러면 순서가 저절로 강제된다 — 다음 칸방의 조각엔 손이 안 닿으니까.
// 조각 수는 규칙이 정한다: 문마다 (칸 수 - 1).
const E = require('./engine.js').SlimeEngine;

const SEC = Number(process.argv[2] || 30);
let seed = (Number(process.argv[3] || 12345) | 0) || 12345;
const ROOMS = Number(process.argv[4] || 3);

const rnd = () => {
  seed ^= seed << 13; seed |= 0;
  seed ^= seed >>> 17;
  seed ^= seed << 5;  seed |= 0;
  return ((seed >>> 0) % 1000000) / 1000000;
};
const ri = n => Math.floor(rnd() * n);
const pick = a => a[ri(a.length)];

const H = 9;                       // 높이는 고정 — 세우기가 살아있을 만큼
const RW = 9;                      // 칸방 하나의 폭

function make() {
  const W = ROOMS * RW + (ROOMS - 1) + 2;      // 칸방들 + 문벽들 + 바깥벽
  const g = [];
  for (let y = 0; y < H; y++) {
    let r = '';
    for (let x = 0; x < W; x++) r += (y === 0 || y === H - 1 || x === 0 || x === W - 1) ? '#' : '.';
    g.push(r.split(''));
  }
  // 칸방 경계에 문벽 세우기
  const bounds = [];               // [x0, x1] 각 칸방의 안쪽 범위
  let x = 1;
  for (let i = 0; i < ROOMS; i++) {
    bounds.push([x, x + RW - 1]);
    x += RW;
    if (i < ROOMS - 1) {
      for (let y = 1; y < H - 1; y++) g[y][x] = String(i + 1);   // 문 i+1 의 벽
      x += 1;
    }
  }

  // 칸방마다 선반 몇 개
  for (const [x0, x1] of bounds) {
    for (let k = 0; k < 1 + ri(2); k++) {
      const y = 3 + ri(H - 5);
      const sx = x0 + ri(Math.max(1, RW - 3));
      const len = 2 + ri(4);
      for (let i = 0; i < len && sx + i <= x1; i++) g[y][sx + i] = '#';
    }
  }

  const solid = (y, xx) => g[y][xx] === '#' || (g[y][xx] >= '1' && g[y][xx] <= '9');
  const ground = (y, xx) => !solid(y, xx) && y + 1 < H && solid(y + 1, xx);

  // 칸방 i 안에 문 i+1 의 홈을 놓고, 그 문에 필요한 조각도 같은 칸방에
  const sizes = [];
  for (let i = 0; i < ROOMS; i++) sizes.push(3 + ri(2));       // 문 3~4칸

  for (let i = 0; i < ROOMS; i++) {
    const [x0, x1] = bounds[i];
    const n = sizes[i];
    // 홈 = 지지되는 칸에서 뻗는 경로
    let path = null;
    for (let a = 0; a < 40 && !path; a++) {
      const spots = [];
      for (let y = 1; y < H - 1; y++) for (let xx = x0; xx <= x1; xx++)
        if (g[y][xx] === '.') spots.push([y, xx]);
      if (!spots.length) return null;
      const [sy, sx] = pick(spots);
      const p = [[sy, sx]]; const used = new Set([sy + ',' + sx]);
      while (p.length < n) {
        const [cy, cx] = p[p.length - 1];
        const opts = [];
        for (const [dy, dx] of [[-1,0],[1,0],[0,-1],[0,1]]) {
          const ny = cy + dy, nx = cx + dx;
          if (ny < 1 || ny >= H - 1 || nx < x0 || nx > x1) continue;
          if (g[ny][nx] !== '.' || used.has(ny + ',' + nx)) continue;
          opts.push([ny, nx]);
        }
        if (!opts.length) break;
        const nxt = pick(opts);
        p.push(nxt); used.add(nxt[0] + ',' + nxt[1]);
      }
      if (p.length === n && p.some(([y, xx]) => ground(y, xx))) path = p;
    }
    if (!path) return null;
    const marks = i === 0 ? ['=', '*'] : ['-', '%'];
    if (i > 1) return null;                        // 지금 기호는 문 두 개까지
    path.forEach(([y, xx], k) => { g[y][xx] = (k === path.length - 1) ? marks[1] : marks[0]; });

    // 조각 n-1개 — 같은 칸방 바닥에
    let need = n - 1;
    for (let a = 0; a < 300 && need; a++) {
      const y = 1 + ri(H - 2), xx = x0 + ri(RW);
      if (xx > x1 || g[y][xx] !== '.' || !ground(y, xx)) continue;
      g[y][xx] = '+'; need--;
    }
    if (need) return null;
  }

  // 시작 — 첫 칸방 바닥
  for (let a = 0; a < 300; a++) {
    const y = 1 + ri(H - 2), xx = bounds[0][0] + ri(RW);
    if (g[y][xx] === '.' && ground(y, xx)) { g[y][xx] = 'S'; return g.map(r => r.join('')); }
  }
  return null;
}

const t0 = process.hrtime.bigint();
const el = () => Number(process.hrtime.bigint() - t0) / 1e9;
let tried = 0, ok = 0;
const hits = [];
while (el() < SEC && hits.length < 5) {
  const grid = make();
  tried++;
  if (!grid) continue;
  const def = { grid, gravity: true, clear: 'all', id: 'm' };
  let r;
  try { r = E.solve(def); } catch (e) { continue; }
  if (!r.ok) continue;
  ok++;
  if (r.shortest !== 1 || r.moves < 20) continue;
  hits.push({ grid, r });
}

console.log(ROOMS + '칸방 · ' + el().toFixed(0) + '초 · ' + tried + '개 시도 → ' + ok + '개 풀림 → 건진 것 ' + hits.length + '개\n');
hits.forEach((h, i) => {
  console.log('--- ' + (i + 1) + '  ' + h.r.moves + '걸음 · 최단해 1개 · 상태 ' + h.r.states.toLocaleString());
  h.grid.forEach(r => console.log('   ' + r));
});
if (!hits.length) console.log('건진 게 없다.');
