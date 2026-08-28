// 🔴 판을 손으로 그리면 틀린다 — 찾게 시킨다.
//    무작위로 판을 뿌리고, 풀리는 것만 남기고, **"이미 진 상태" 비율**로 줄 세운다.
//
//   node tools/gen.js [초] [zone|nozone]      예: node tools/gen.js 30 zone
//
// 이 도구가 답하는 질문 하나: **무중력 구역('~')이 판을 더 어렵게 만드는가?**
//   같은 예산으로 zone / nozone 을 각각 돌려 최고 기록을 비교한다.
const E = require('./engine.js').SlimeEngine;
const check = require('./check.js');
const { analyze } = require('./metrics.js');   // 🔴 난이도 지표는 여기 한 벌뿐이다

// 判 크기를 인자로 받는다 — 작은 판은 헤맸 데가 없다.
//   node tools/gen.js 25 nozone 777 12x9
const SIZE = (process.argv[5] || "20x12").split("x").map(Number);
const W = SIZE[0], H = SIZE[1];
const SEC = Number(process.argv[2] || 20);
const MODE = process.argv[3] || 'zone';           // zone | nozone | zone!
const USE_ZONE = MODE !== 'nozone';
// zone! : 그 구역이 **없으면 안 풀리는** 판만 남긴다 (구역이 진짜 역할을 하는가)
const NEED_ZONE = MODE === 'zone!';
// 🔴 목표 모양: line = 바닥에 붙은 가로 한 줄(옛 방식) / free = 아무 모양이나
//    직선으로만 두면 판이 전부 '몸을 바닥에 눕히기'로 끝난다. 규칙의 절반만 쓰는 셈이다.
const SHAPE = process.argv[6] || 'free';
// 목표 난이도(%) — 주면 그 값에 **가까운** 판을 고른다. 안 주면 제일 어려운 판.
//   초반 방은 제일 어려운 게 아니라 '적당한' 게 필요하다.
const BAND = process.argv[7] === undefined ? null : Number(process.argv[7]) / 100;
// 첫 걸음에 지는 판은 초반에 안 쓴다
const EARLIEST_MIN = Number(process.argv[8] || 0);

// 재현되게 — 시각이 아니라 씨앗으로 돌린다
// 🔴 seed * 1103515245 는 2^53을 넘어 정밀도를 잃는다 — 어떤 씨앗을 줘도 같은 수열이 나왔다.
//    (씨앗을 바꿔 '다시 확인'한 측정이 사실은 같은 표본 하나였다. 2026-08-28)
//    32비트 안에서만 도는 xorshift로 바꾼다.
let seed = (Number(process.argv[4] || 12345) | 0) || 12345;
const rnd = () => {
  seed ^= seed << 13; seed |= 0;
  seed ^= seed >>> 17;
  seed ^= seed << 5;  seed |= 0;
  return ((seed >>> 0) % 1000000) / 1000000;
};
const ri = n => Math.floor(rnd() * n);

const CAP = 120000;   // 이보다 큰 판은 재는 데 너무 걸린다 — 버린다

// 🔴 채택 기준 (2026-08-28: 게임은 진 걸 안 알려준다 → 헤매는 시간을 판으로 줄인다)
// 🔴 헤맴 기준을 느슨하게 푼다. "안 알려준다"를 고른 이상 헤맴은 없앨 값이 아니라
//    감수할 값이고, 14로 묶었더니 **생각할 여지까지 같이 묶였다** (판이 5초에 풀렸다).
const WANDER_MAX = Number(process.env.WANDER_MAX || 40);
const MOVES_MIN = Number(process.env.MOVES_MIN || 18), MOVES_MAX = Number(process.env.MOVES_MAX || 60);

function blank() {
  const g = [];
  for (let y = 0; y < H; y++) {
    let r = '';
    for (let x = 0; x < W; x++) r += (y === 0 || y === H - 1 || x === 0 || x === W - 1) ? '#' : '.';
    g.push(r.split(''));
  }
  return g;
}
const solid = (g, y, x) => g[y][x] === '#';
// 그 칸에 서면 안 떨어지는가 (바로 아래가 벽)
const ground = (g, y, x) => !solid(g, y, x) && y + 1 < H && solid(g, y + 1, x);

function make() {
  const g = blank();

  // 선반 2~4개 — 높이 차이를 만든다
  const shelves = 2 + ri(Math.max(1, Math.round(W / 7)));
  for (let i = 0; i < shelves; i++) {
    const y = 3 + ri(Math.max(1, H - 6));
    const x = 2 + ri(Math.max(1, W - 6));
    const len = 2 + ri(Math.max(1, Math.round(W / 3)));
    for (let k = 0; k < len && x + k < W - 1; k++) g[y][x + k] = '#';
  }
  // 세로 벽 — 가로 선반만 있으면 판이 전부 층계처럼 생긴다
  if (SHAPE === 'free') {
    const bars = ri(3);
    for (let i = 0; i < bars; i++) {
      const x = 2 + ri(Math.max(1, W - 4));
      const y0 = 2 + ri(Math.max(1, H - 5));
      const len = 2 + ri(Math.max(1, Math.round(H / 3)));
      for (let k = 0; k < len && y0 + k < H - 1; k++) g[y0 + k][x] = '#';
    }
    // 튀어나온 덩어리 — 천장에 매달리거나 모서리를 깎는다
    const blobs = ri(3);
    for (let i = 0; i < blobs; i++) {
      const x = 2 + ri(Math.max(1, W - 4)), y = 1 + ri(Math.max(1, H - 3));
      for (let dy = 0; dy < 1 + ri(2); dy++)
        for (let dx = 0; dx < 1 + ri(2); dx++)
          if (y + dy < H - 1 && x + dx < W - 1) g[y + dy][x + dx] = '#';
    }
  }

  // 구덩이 — 떨어지면 못 나오는 곳이 "이미 진 상태"를 만든다
  if (ri(2) && W >= 9) {
    const x = 3 + ri(Math.max(1, W - 8)), d = 2 + ri(2);
    for (let y = H - 1 - d; y < H - 1; y++) {
      if (x - 1 > 0) g[y][x - 1] = '#';
      if (x + 2 < W - 1) g[y][x + 2] = '#';
    }
  }

  // 무중력 구역 — 기둥 하나 또는 방 하나
  const zone = new Set();
  if (USE_ZONE) {
    if (ri(2)) {
      const x = 2 + ri(Math.max(1, W - 4)), y0 = 2 + ri(Math.max(1, H - 6)), len = 3 + ri(5);
      for (let y = y0; y < y0 + len && y < H - 1; y++) if (!solid(g, y, x)) zone.add(y * W + x);
    } else {
      const x0 = 2 + ri(Math.max(1, W - 8)), y0 = 2 + ri(Math.max(1, H - 6)), w = 2 + ri(3), h = 2 + ri(3);
      for (let y = y0; y < y0 + h && y < H - 1; y++)
        for (let x = x0; x < x0 + w && x < W - 1; x++) if (!solid(g, y, x)) zone.add(y * W + x);
    }
  }

  // 설 수 있는 칸 (무중력 구역 안도 설 수 있다)
  const stand = [];
  for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++)
    if (!solid(g, y, x) && (ground(g, y, x) || zone.has(y * W + x))) stand.push([y, x]);
  if (stand.length < Math.max(6, W)) return null;

  // 목표 칸 수
  // 🔴 목표 칸 수 = 몸 길이 = 조각+1. 짧으면 순서가 몇 가지 안 돼서 딱 보면 풀린다.
  //    (2026-08-28: 길이 3~4짜리 판 다섯을 사람이 평균 5초에 풀었다)
  //    상한은 솔버가 정한다 — 한 칸마다 상태가 3.6배씩 늘어 10칸이면 290만이다.
  const NMIN = Number(process.env.NMIN || 4), NMAX = Number(process.env.NMAX || 6);
  const nMax = Math.max(NMIN, Math.min(NMAX, W - 6));
  const n = NMIN + ri(Math.max(1, nMax - NMIN + 1));

  // 🔴 목표는 '몸이 그대로 눕는 모양'이다. 몸은 사슬이므로 목표도 **한 줄로 이어진 경로**여야 한다.
  //    직선만 쓰면 판이 전부 똑같이 끝난다 — 꺾인 모양이면 마무리 자체가 퍼즐이 된다.
  //    단, 중력이 있으므로 그 모양이 **통째로 지지돼야** 한다 (어느 칸이든 아래가 벽).
  const cells = [];
  if (SHAPE === 'line') {
    const runs = [];
    for (let y = 1; y < H - 1; y++) for (let x = 1; x + n <= W - 1; x++) {
      let ok = true;
      for (let k = 0; k < n; k++) if (!ground(g, y, x + k) || zone.has(y * W + (x + k))) { ok = false; break; }
      if (ok) runs.push([y, x]);
    }
    if (!runs.length) return null;
    const [ty, tx] = runs[ri(runs.length)];
    for (let k = 0; k < n; k++) cells.push(ty * W + tx + k);
  } else {
    // 지지되는 칸에서 출발해 자기를 안 밟는 경로를 n칸 뻗는다
    let found = null;
    for (let attempt = 0; attempt < 30 && !found; attempt++) {
      const [sy0, sx0] = stand[ri(stand.length)];
      const path = [sy0 * W + sx0];
      const used = new Set(path);
      while (path.length < n) {
        const c = path[path.length - 1], cy = (c / W) | 0, cx = c % W;
        const opts = [];
        for (const [dy, dx] of [[-1, 0], [1, 0], [0, -1], [0, 1]]) {
          const ny = cy + dy, nx = cx + dx;
          if (ny < 1 || nx < 1 || ny >= H - 1 || nx >= W - 1) continue;
          if (solid(g, ny, nx) || zone.has(ny * W + nx)) continue;
          if (used.has(ny * W + nx)) continue;
          opts.push(ny * W + nx);
        }
        if (!opts.length) break;
        const pick = opts[ri(opts.length)];
        path.push(pick); used.add(pick);
      }
      if (path.length !== n) continue;
      // 통째로 지지되는가 — 한 칸이라도 아래가 벽이면 버틴다
      if (!path.some(c => ground(g, (c / W) | 0, c % W))) continue;
      found = path;
    }
    if (!found) return null;
    cells.push(...found);
  }

  // 심은 경로의 **끝**에 둔다 — 몸은 사슬이라 머리는 끝에만 올 수 있다
  const tset = new Set(cells);
  const coreAt = ri(2) ? 0 : cells.length - 1;
  cells.forEach((c, k) => { g[(c / W) | 0][c % W] = k === coreAt ? '*' : '='; });

  // 조각 n-1개 + 시작 1개, 서로 안 겹치게
  const free = stand.filter(([y, x]) => !tset.has(y * W + x));
  if (free.length < n + 2) return null;
  for (let i = free.length - 1; i > 0; i--) { const j = ri(i + 1); [free[i], free[j]] = [free[j], free[i]]; }
  const [sy, sx] = free[0];
  g[sy][sx] = 'S';
  for (let i = 1; i < n; i++) { const [y, x] = free[i]; g[y][x] = '+'; }

  const grid = g.map((r, y) => r.map((c, x) => (c === '.' && zone.has(y * W + x)) ? '~' : c).join(''));
  return grid;
}

const t0 = process.hrtime.bigint();
const elapsed = () => Number(process.hrtime.bigint() - t0) / 1e9;

let tried = 0, solvable = 0, loadBearing = 0, tooLong = 0;
const best = [];
while (elapsed() < SEC) {
  const grid = make();
  tried++;
  if (!grid) continue;
  const k = check(grid);
  if (!k.ok) continue;
  let r;
  try { r = E.solve({ grid, gravity: true }); } catch (e) { continue; }
  if (!r.ok || r.moves < MOVES_MIN || r.moves > MOVES_MAX) continue;
  solvable++;
  if (NEED_ZONE) {
    if (!k.zone) continue;
    const flat = grid.map(r => r.split('~').join('.'));   // 구역을 그냥 빈칸으로
    let r2; try { r2 = E.solve({ grid: flat, gravity: true }); } catch (e) { continue; }
    if (r2.ok) continue;                                   // 없어도 풀리면 장식일 뿐이다
    loadBearing++;
  }
  const a = analyze({ grid, gravity: true }, CAP);
  if (!a) continue;
  if (a.wander > WANDER_MAX) { tooLong++; continue; }   // 오래 헤매는 판은 안 쓴다
  if (a.earliest !== null && a.earliest < EARLIEST_MIN) continue;
  if (best.some(p => p.grid.join('') === grid.join(''))) continue;
  best.push({ grid, ratio: a.lost, wander: a.wander, earliest: a.earliest, moves: r.moves, shortest: r.shortest, states: a.states, zone: k.zone, path: r.path });
  best.sort((p, q) => BAND === null ? q.ratio - p.ratio
    : Math.abs(p.ratio - BAND) - Math.abs(q.ratio - BAND));
  if (best.length > 12) best.length = 12;
}

console.log(W + 'x' + H + '  ·  ' + MODE + '  ·  ' + elapsed().toFixed(0) + '초  ·  ' + tried + '판 시도 → ' + solvable + '판 풀림' +
  (NEED_ZONE ? ' → ' + loadBearing + '판이 구역에 기대고 있음' : '') +
  ' → ' + tooLong + '판은 너무 오래 헤매서 버림');
console.log('');
best.forEach((b, i) => {
  console.log('--- ' + (i + 1) + '위 · 이미 진 상태 ' + (b.ratio * 100).toFixed(1) + '%' +
    ' · ' + b.moves + '걸음 · 빠르면 ' + b.earliest + '걸음에 진다 · 진 뒤 ' + b.wander + '걸음 헤맴' +
    ' · 최단해 ' + b.shortest + '개' + (b.zone ? ' · 무중력 ' + b.zone + '칸' : ''));
  b.grid.forEach(r => console.log('  ' + r));
});
if (!best.length) console.log('건진 게 없다.');

const NL = String.fromCharCode(10);
// 🔴 2단 거르기. 여기(생성기)는 값싼 것만 본다 — 풀리나 · 최단해가 하나인가 · 길이.
//    사람이 느끼는 난이도(통하는 순서 비율)는 비싸서 여기서 못 돌린다.
//    후보를 파일로 내보내고 `node tools/human.js tools/cands.json` 으로 2차로 거른다.
try {
  require('fs').writeFileSync(
    require('path').join(__dirname, 'cands.json'),
    JSON.stringify({ gravity: true, levels: best.map((b, i) => ({
      id: 'c' + (i + 1), best: b.moves, lost: Math.round(b.ratio * 1000) / 10,
      wander: b.wander, grid: b.grid,
    })) }, null, 2) + NL);
  console.log(NL + 'tools/cands.json 에 ' + best.length + '판 저장 - 다음: node tools/human.js tools/cands.json');
} catch (e) { console.log('후보 저장 실패: ' + e.message); }
