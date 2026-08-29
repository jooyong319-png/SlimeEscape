// 🔴 한 방에 문을 둘 만든다 — 지금 있는 방을 그대로 쓰면서.
//
//   node tools/doors.js [초]
//
// 사장님 안: "맵 안에 문이 여러 개 있고, 몸을 어떻게 채우냐에 따라 어느 문이 열리는지 정해진다."
// 그러면 갈래가 **메뉴가 아니라 퍼즐 안에** 생긴다. 지도 화면이 필요 없다.
//
// 🔴 제약: 길이 = 목표 칸 수이고 조각 개수는 고정이다.
//    → **두 문은 칸 수가 같아야 한다.** 모양만 달라야 한다.
//
// 하는 일: 벽·조각·시작은 그대로 두고, **두 번째 목표 모양**이 들어갈 자리를 찾는다.
//   · 첫 문과 안 겹칠 것
//   · 통째로 지지될 것 (중력)
//   · 그것만으로도 풀릴 것, 그리고 **최단해가 하나뿐**일 것
//   · 첫 문과 **요구하는 것이 다를 것** (같으면 그냥 변주다)
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const SEC = Number(process.argv[2] || 25);
const FILE = path.join(__dirname, '..', 'game', 'Assets', 'Resources', 'levels.json');
const doc = JSON.parse(fs.readFileSync(FILE, 'utf8'));

let seed = 20260829;
const rnd = () => {
  seed ^= seed << 13; seed |= 0;
  seed ^= seed >>> 17;
  seed ^= seed << 5;  seed |= 0;
  return ((seed >>> 0) % 1000000) / 1000000;
};
const ri = n => Math.floor(rnd() * n);

const DIRS = [[0, -1], [0, 1], [-1, 0], [1, 0]];
const SYM = ['↑', '↓', '←', '→'];

/// 정답 수순이 무엇을 요구하나 (tools/demand.js와 같은 정의)
function demands(grid, sol) {
  const L = E.parse({ grid, gravity: true });
  let st = E.startState(L);
  let fall = 0, eat = 0, tall = 1, back = 0, prev = -1;
  for (const ch of sol) {
    const di = SYM.indexOf(ch);
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
  return { fall, eat, tall, back };
}
const far = (a, b) => Math.abs(a.fall - b.fall) + Math.abs(a.eat - b.eat)
  + Math.abs(a.tall - b.tall) + Math.abs(a.back - b.back);

const t0 = process.hrtime.bigint();
const el = () => Number(process.hrtime.bigint() - t0) / 1e9;
const per = SEC / doc.levels.length;
const found = {};

for (const j of doc.levels) {
  const W = j.grid[0].length, H = j.grid.length;
  const rows = j.grid.map(r => r.split(''));
  const firstTarget = [];
  rows.forEach((r, y) => r.forEach((c, x) => { if (c === '=' || c === '*') firstTarget.push(y * W + x); }));
  const n = firstTarget.length;

  // 첫 문을 지운 판 — 여기에 두 번째 문을 놓는다
  const bare = rows.map(r => r.map(c => (c === '=' || c === '*') ? '.' : c));
  const solid = (y, x) => bare[y][x] === '#';
  const free = (y, x) => bare[y][x] === '.';
  const ground = (y, x) => !solid(y, x) && y + 1 < H && solid(y + 1, x);
  const taken = new Set(firstTarget);

  const d1 = demands(j.grid, j.sol);
  const end = el() + per;
  let tried = 0, ok = 0, best = null;

  while (el() < end) {
    tried++;
    // 지지되는 칸에서 시작해 자기를 안 밟는 경로 n칸
    const starts = [];
    for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++)
      if (free(y, x) && !taken.has(y * W + x)) starts.push([y, x]);
    if (starts.length < n) break;
    const [sy, sx] = starts[ri(starts.length)];
    const p = [sy * W + sx]; const used = new Set(p);
    while (p.length < n) {
      const c = p[p.length - 1], cy = (c / W) | 0, cx = c % W;
      const opts = [];
      for (const [dx, dy] of DIRS.map(d => [d[0], d[1]])) {
        const nx = cx + dx, ny = cy + dy;
        if (ny < 1 || nx < 1 || ny >= H - 1 || nx >= W - 1) continue;
        if (!free(ny, nx) || taken.has(ny * W + nx) || used.has(ny * W + nx)) continue;
        opts.push(ny * W + nx);
      }
      if (!opts.length) break;
      const pick = opts[ri(opts.length)];
      p.push(pick); used.add(pick);
    }
    if (p.length !== n) continue;
    if (!p.some(c => ground((c / W) | 0, c % W))) continue;    // 통째로 안 지지되면 못 채운다

    // 이 모양만 목표로 두고 풀어본다
    const g2 = bare.map(r => r.slice());
    const coreAt = ri(2) ? 0 : p.length - 1;
    p.forEach((c, k) => { g2[(c / W) | 0][c % W] = k === coreAt ? '*' : '='; });
    const grid2 = g2.map(r => r.join(''));

    let r; try { r = E.solve({ grid: grid2, gravity: true }); } catch (e) { continue; }
    if (!r.ok || r.shortest !== 1 || r.moves < 8) continue;
    ok++;
    const d2 = demands(grid2, r.path);
    if (!d2) continue;
    const gap = far(d1, d2);
    if (!best || gap > best.gap) best = { cells: p, moves: r.moves, d: d2, gap, grid: grid2 };
  }

  found[j.id] = best;
  console.log(
    j.id.padEnd(5) + ' 목표 ' + n + '칸 · ' + String(tried).padStart(5) + '개 시도 → ' +
    String(ok).padStart(4) + '개가 두 번째 문이 될 수 있다' +
    (best ? '  🟢 제일 다른 것: ' + best.moves + '걸음 (요구 차이 ' + best.gap + ')' : '  🔴 없음')
  );
  if (best) {
    const d1s = `낙하${d1.fall} 먹기${d1.eat} 세우기${d1.tall} 되짚기${d1.back}`;
    const d2s = `낙하${best.d.fall} 먹기${best.d.eat} 세우기${best.d.tall} 되짚기${best.d.back}`;
    console.log('        문1: ' + j.best + '걸음  ' + d1s);
    console.log('        문2: ' + best.moves + '걸음  ' + d2s);
  }
}

fs.writeFileSync(path.join(__dirname, 'doors.json'),
  JSON.stringify(found, null, 2) + '\n');
console.log('\ntools/doors.json 에 저장');
