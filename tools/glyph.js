// 🔬 목표 모양을 **문양으로 고정**해도 어려운 판이 나오나?
//
//   node tools/glyph.js [초] [씨앗] [WxH]
//
// 사장님 안(2026-08-28): 각 스테이지의 클리어 모양이 문양 한 획이고,
// 다 모으면 마지막 문을 여는 표식이 된다.
// 새 규칙은 0개다 — 목표 모양은 이미 규칙 안에 있다. 다만 대가가 있다:
//   🔴 목표를 고정하면 생성기가 벽·조각·시작만 뒤질 수 있다. 탐색 여지가 확 준다.
//      그래도 "이미 진 상태"가 나오는지 여기서 확인한다.
//
// 🔴 몸은 사슬이라 문양 위에 **해밀턴 경로**가 있어야 한다.
//    획이 갈라지면(+, ㅗ) 가운데를 두 번 밟아야 해서 불가능하다.
const E = require('./engine.js').SlimeEngine;
const check = require('./check.js');
const { analyze } = require('./metrics.js');

const SEC = Number(process.argv[2] || 20);
let seed = (Number(process.argv[3] || 12345) | 0) || 12345;
const SIZE = (process.argv[4] || '14x10').split('x').map(Number);
// 목표 난이도(%) — 주면 그 값에 가까운 판을 고른다. 새 개념을 가르치는 판은
// 제일 어려운 게 아니라 '배울 수 있는' 게 필요하다.
const BAND = process.argv[5] === undefined ? null : Number(process.argv[5]) / 100;
const W = SIZE[0], H = SIZE[1];

const rnd = () => {
  seed ^= seed << 13; seed |= 0;
  seed ^= seed >>> 17;
  seed ^= seed << 5;  seed |= 0;
  return ((seed >>> 0) % 1000000) / 1000000;
};
const ri = n => Math.floor(rnd() * n);

// 문양 — [행, 열] 오프셋. 심(*)은 경로의 끝이어야 한다.
const GLYPHS = {
  '一 곧은획': [[0, 0], [0, 1], [0, 2], [0, 3]],
  'ㄱ 꺾인획': [[0, 0], [0, 1], [0, 2], [1, 2]],
  'ㄴ 받침획': [[0, 0], [1, 0], [1, 1], [1, 2]],
  'ㄷ 감싼획': [[0, 0], [0, 1], [1, 0], [2, 0], [2, 1]],
  'ㅁ 닫힌획': [[0, 0], [0, 1], [1, 1], [1, 0]],
  'ㄹ 겹친획': [[0, 0], [0, 1], [0, 2], [1, 2], [2, 2], [2, 1], [2, 0]],
  'ㅅ 갈래획': [[0, 1], [1, 0], [1, 1], [1, 2]],        // 갈라진다 — 안 될 것이다
};

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

/// 문양 칸들 위에 한붓그리기가 되나 (해밀턴 경로). 되면 그 순서를, 아니면 null
function traceable(cells) {
  const set = new Set(cells.map(c => c[0] + ',' + c[1]));
  const key = c => c[0] + ',' + c[1];
  const nb = c => [[c[0] - 1, c[1]], [c[0] + 1, c[1]], [c[0], c[1] - 1], [c[0], c[1] + 1]]
    .filter(k => set.has(key(k)));
  for (const start of cells) {
    const seen = new Set([key(start)]);
    const path = [start];
    const walk = () => {
      if (path.length === cells.length) return true;
      for (const n of nb(path[path.length - 1])) {
        if (seen.has(key(n))) continue;
        seen.add(key(n)); path.push(n);
        if (walk()) return true;
        seen.delete(key(n)); path.pop();
      }
      return false;
    };
    if (walk()) return path;
  }
  return null;
}

function make(glyph) {
  const g = blank();

  const shelves = 2 + ri(Math.max(1, Math.round(W / 6)));
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

  // 문양을 놓을 자리 — 빈칸 위에, 그리고 통째로 지지돼야 한다
  const gh = Math.max(...glyph.map(c => c[0])) + 1;
  const gw = Math.max(...glyph.map(c => c[1])) + 1;
  const spots = [];
  for (let y = 1; y + gh <= H - 1; y++) for (let x = 1; x + gw <= W - 1; x++) {
    let ok = true, held = false;
    for (const [dy, dx] of glyph) {
      if (solid(g, y + dy, x + dx)) { ok = false; break; }
      if (ground(g, y + dy, x + dx)) held = true;
    }
    if (ok && held) spots.push([y, x]);
  }
  if (!spots.length) return null;
  const [ty, tx] = spots[ri(spots.length)];

  const order = traceable(glyph);
  if (!order) return null;
  const endAt = ri(2) ? order[0] : order[order.length - 1];
  const tset = new Set();
  for (const [dy, dx] of glyph) {
    const isCore = dy === endAt[0] && dx === endAt[1];
    g[ty + dy][tx + dx] = isCore ? '*' : '=';
    tset.add((ty + dy) * W + (tx + dx));
  }

  const stand = [];
  for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++)
    if (!solid(g, y, x) && ground(g, y, x) && !tset.has(y * W + x)) stand.push([y, x]);
  const n = glyph.length;
  if (stand.length < n + 2) return null;
  for (let i = stand.length - 1; i > 0; i--) { const j = ri(i + 1); [stand[i], stand[j]] = [stand[j], stand[i]]; }
  g[stand[0][0]][stand[0][1]] = 'S';
  for (let i = 1; i < n; i++) g[stand[i][0]][stand[i][1]] = '+';

  return g.map(r => r.join(''));
}

const WANDER_MAX = 14;
const t0 = process.hrtime.bigint();
const per = SEC / Object.keys(GLYPHS).length;

console.log(W + 'x' + H + ' · 문양마다 ' + per.toFixed(0) + '초씩\n');
for (const [name, glyph] of Object.entries(GLYPHS)) {
  const path = traceable(glyph);
  if (!path) { console.log(name.padEnd(10) + ' 🔴 한붓그리기 불가 — 몸이 사슬이라 못 채운다'); continue; }

  const end = Number(process.hrtime.bigint() - t0) / 1e9 + per;
  let tried = 0, solvable = 0, tooLong = 0, best = null;
  while (Number(process.hrtime.bigint() - t0) / 1e9 < end) {
    const grid = make(glyph);
    tried++;
    if (!grid) continue;
    if (!check(grid).ok) continue;
    let r; try { r = E.solve({ grid, gravity: true }); } catch (e) { continue; }
    if (!r.ok || r.moves < 10 || r.moves > 30 || r.shortest !== 1) continue;
    solvable++;
    const a = analyze({ grid, gravity: true }, 120000);
    if (!a) continue;
    if (a.wander > WANDER_MAX) { tooLong++; continue; }
    const better = !best ? true
      : BAND === null ? a.lost > best.a.lost
        : Math.abs(a.lost - BAND) < Math.abs(best.a.lost - BAND);
    if (better) best = { grid, a, r };
  }
  console.log(name.padEnd(10) + ' ' + glyph.length + '칸 · ' +
    String(tried).padStart(6) + '판 시도 → ' + String(solvable).padStart(4) + '판 통과 (헤맴초과 ' + tooLong + ')' +
    (best ? ' · 🟢 최고 ' + (best.a.lost * 100).toFixed(1) + '% (' + best.r.moves + '걸음, 헤맴 ' + best.a.wander + ')'
      : ' · 🔴 건진 판 없음'));
  if (best) best.grid.forEach(r => console.log('   ' + r));
}
