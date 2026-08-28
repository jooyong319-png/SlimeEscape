// 🔬 무엇이 이 게임을 어렵게 만드는가 — 가설 검증
//   가설: "몸이 짧고 방이 넓어서" 쉽다. 몸이 길어지고 방이 좁아지면 어려워질 것이다.
const E3 = require('./engine.js').SlimeEngine;

function analyze(def, cap = 900000) {
  const L = E3.parse(def);
  const idOf = new Map(), states = [];
  const id = st => { const k = E3.keyOf(st); let v = idOf.get(k);
    if (v === undefined) { v = states.length; idOf.set(k, v); states.push(st); } return v; };
  id(E3.startState(L));
  const edges = [], win = [];
  for (let i = 0; i < states.length; i++) {
    if (states.length > cap) return { over: true, total: states.length };
    const st = states[i], out = [];
    if (E3.isWin(L, st)) { win.push(i); edges.push(out); continue; }
    for (let d = 0; d < 4; d++) { const ns = E3.step(L, st, d); if (ns) out.push(id(ns)); }
    edges.push(out);
  }
  const rev = states.map(() => []);
  edges.forEach((o, i) => o.forEach(j => rev[j].push(i)));
  const canWin = new Uint8Array(states.length);
  const q = [...win]; win.forEach(i => canWin[i] = 1);
  for (let h = 0; h < q.length; h++) for (const p of rev[q[h]]) if (!canWin[p]) { canWin[p] = 1; q.push(p); }
  let dead = 0; for (let i = 0; i < states.length; i++) if (!canWin[i]) dead++;
  return { total: states.length, dead, lost: dead / states.length, wins: win.length };
}

/// 방 크기·목표 크기를 바꿔가며 잰다. 방은 (w,h) 유효 공간.
function make(rw, rh, tw, th) {
  const W = rw + 2, H = rh + 2;
  const g = [];
  for (let y = 0; y < H; y++) g.push(Array.from({ length: W }, (_, x) =>
    (x === 0 || y === 0 || x === W - 1 || y === H - 1) ? '#' : '.'));
  // 목표: 오른쪽 아래 tw×th
  for (let y = H - 1 - th; y <= H - 2; y++) for (let x = W - 1 - tw; x <= W - 2; x++) g[y][x] = '=';
  g[H - 1 - th][W - 2] = '*';
  g[1][1] = 'S';
  // 조각: 목표 칸 수 − 1 개를 왼쪽 위 영역에 규칙적으로
  const need = tw * th - 1;
  let placed = 0;
  outer:
  for (let y = 1; y <= rh; y += 2) for (let x = 3; x <= rw; x += 2) {
    if (g[y][x] !== '.') continue;
    g[y][x] = '+'; if (++placed >= need) break outer;
  }
  if (placed < need) return null;
  return g.map(r => r.join(''));
}

console.log('방 크기 · 목표(=길이) 별로 "이미 진 상태" 비율\n');
console.log('  방      목표   길이  상태수      이미 진 상태');
for (const [rw, rh, tw, th] of [
  [18,10, 3,2], [18,10, 4,2], [12,8, 3,2], [12,8, 4,2],
  [10,6, 4,2], [8,6, 4,2], [8,6, 3,3], [7,5, 3,3], [6,5, 3,3],
]) {
  const grid = make(rw, rh, tw, th);
  if (!grid) { console.log(`  ${rw}x${rh}  ${tw}x${th}  — 조각 놓을 자리 부족`); continue; }
  const r = E3.solve({ grid });
  if (!r.ok) { console.log(`  ${String(rw+'x'+rh).padEnd(7)} ${tw}x${th}   ${String(tw*th).padEnd(4)} — ${r.why}`); continue; }
  const a = analyze({ grid });
  if (a.over) { console.log(`  ${String(rw+'x'+rh).padEnd(7)} ${tw}x${th}   ${String(tw*th).padEnd(4)} 상태 90만 초과 — 생략`); continue; }
  console.log(`  ${String(rw+'x'+rh).padEnd(7)} ${tw}x${th}   ${String(tw*th).padEnd(4)} ` +
              `${String(a.total.toLocaleString()).padStart(9)}   ${(a.lost*100).toFixed(1)}%` +
              `   (최단 ${r.moves}걸음, 해 ${r.shortest}개)`);
}
