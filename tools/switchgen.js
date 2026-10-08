// 🔴 **스위치 판**을 짓는다 (09-23) — 굴 9 「뒤바뀌는 바닥」 · 굴 10 재료.
//
// 사장님: "아싸리 슈퍼마리오랑 같은 방식으로 하자. 특정 영역에 머리가 들어가면 작동하는 걸로."
//   '!' 스위치 — 머리가 들어올 때마다 판 전체가 켜짐↔꺼짐
//   '4' 꺼짐일 때 벽 (처음 벽) · '5' 켜짐일 때 벽 (처음 빈칸)
//
// flip.js(홈에 묶인 반전벽, 폐기)와 같은 방식 — **이미 풀리는 판에 얹어 본다.**
//   빈칸 몇 개를 '!' / '5' 로, 속 벽 몇 개를 '4' 로 바꾸고 다시 푼다.
//
// 받는 조건 (도구는 **객관적인 것만** 거른다)
//   · 풀린다 · 최단해가 하나 · 걸음 하한 이상
//   · 🔴 정답이 스위치를 **TOGGLES 번 이상** 누른다 — 안 누르면 장식이다
//   · 🔴 **스위치를 빼면(빈칸으로) 못 풀거나 DIFF 걸음 이상 길어진다** — 누르는 게 퍼즐에 관여한다
//   · 원판 하나에서 PER 판까지 · 격자가 같은 판은 한 번만
//
//   node tools/switchgen.js
//   MIN=60 WANT=150 SEED=5 node tools/switchgen.js
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const MIN = +(process.env.MIN || 40);
const WANT = +(process.env.WANT || 100);
const CAP = +(process.env.SOLVE_CAP || 1500000);
const MOVES = +(process.env.MOVES || 16);
const TRIES = +(process.env.TRIES || 10);
const PER = +(process.env.PER || 2);
const DIFF = +(process.env.DIFF || 3);
const TOGGLES = +(process.env.TOGGLES || 2);
const OUT = process.env.OUT || path.join(__dirname, 'switch-pool.json');

//  🔴 재료 — 재어 둔 못(rated.json) **+ 게임에 들어간 기믹 없는 판**.
//     못만 쓰면 원판이 금세 동나서(PER 만큼 다 뽑으면) 나오는 속도가 뚝 떨어진다 (09-23).
const pool0 = JSON.parse(fs.readFileSync(path.join(__dirname, 'rated.json'), 'utf8'));
try {
  for (const l of require('../game/Assets/Resources/levels.json').levels)
    if (!l.gim || l.gim === 'plain' || /^(box|ice|crack|portal)/.test(l.gim)) pool0.push(l);
} catch (e) { }
const src = pool0
  .filter(p => p.grid && p.best && !/[1-6!&Z]/.test(p.grid.join('')));

function rng(seed) { let s = seed >>> 0 || 1; return () => ((s = (s * 1664525 + 1013904223) >>> 0) / 4294967296); }

/// 🔴 원판 정답이 지나간 칸들 — 여기 근처에 얹어야 퍼즐이 **바뀐다** (09-23).
///    아무 데나 얹으면 2,000판을 풀어 17판밖에 못 건졌다: 대개 정답과 상관없는 구석이라
///    "스위치를 빼도 똑같이 풀린다" 에서 걸러졌다.
function pathCells(p) {
  const out = new Set();
  if (!p.sol) return out;
  const L = E.parse({ grid: p.grid, gravity: true, clear: p.clear || 'all' });
  let st = E.startState(L);
  const D = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };
  for (const c of st.body) out.add(c);
  for (const ch of p.sol) {
    const ns = E.step(L, st, D[ch]);
    if (!ns) break;
    for (const c of ns.body) out.add(c);
    st = ns;
  }
  return out;
}

/// 스위치 1~2개(발 디딜 데가 있는 빈칸), 켜짐 블록 1~3(빈칸), 꺼짐 블록 1~3(속 벽)
function mutate(grid, r, path) {
  const g = grid.map(row => row.split(''));
  const H = g.length, W = g[0].length;
  const floor = [], empty = [], wall = [];
  for (let y = 1; y < H - 1; y++)
    for (let x = 1; x < W - 1; x++) {
      if (g[y][x] === '.') { empty.push([x, y]); if ('#IXB'.includes(g[y + 1][x])) floor.push([x, y]); }
      else if (g[y][x] === '#') wall.push([x, y]);
    }
  if (!floor.length || !empty.length) return null;
  //  🔴 정답이 지나간 칸(과 그 옆)을 **앞세운다** — 길을 실제로 막고 여는 자리라야 퍼즐이 바뀐다
  if (path && path.size) {
    const near = ([x, y]) => path.has(y * W + x) ? 2
      : (path.has((y - 1) * W + x) || path.has((y + 1) * W + x) ||
         path.has(y * W + x - 1) || path.has(y * W + x + 1)) ? 1 : 0;
    const bias = arr => { const hot = arr.filter(c => near(c) > 0); return hot.length ? hot : arr; };
    floor.splice(0, floor.length, ...bias(floor));
    empty.splice(0, empty.length, ...bias(empty));
    wall.splice(0, wall.length, ...bias(wall));
  }
  const pick = (arr, n) => { const out = []; for (let i = 0; i < n && arr.length; i++) out.push(arr.splice(Math.floor(r() * arr.length), 1)[0]); return out; };
  const taken = new Set();
  const put = (cells, ch) => { for (const [x, y] of cells) if (!taken.has(x + ',' + y)) { g[y][x] = ch; taken.add(x + ',' + y); } };
  put(pick(floor, 1 + Math.floor(r() * 2)), '!');
  put(pick(empty.filter(([x, y]) => !taken.has(x + ',' + y)), 1 + Math.floor(r() * 3)), '5');
  put(pick(wall, 1 + Math.floor(r() * 3)), '4');
  return g.map(row => row.join(''));
}

/// 정답이 스위치를 몇 번 누르나
function toggles(grid, clear, sol) {
  const L = E.parse({ grid, gravity: true, clear });
  let st = E.startState(L), n = 0;
  const D = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };
  for (const ch of sol) {
    const ns = E.step(L, st, D[ch]);
    if (!ns) return -1;
    if (((ns.dm || 0) ^ (st.dm || 0)) & 8) n++;
    st = ns;
  }
  return n;
}

const out = fs.existsSync(OUT) ? JSON.parse(fs.readFileSync(OUT, 'utf8')) : [];
const seen = new Set(out.map(p => p.grid.join('|')));
//  🔴 중복 — 게임에 이미 있는 판과 같은 격자면 안 받는다
try {
  for (const l of require('../game/Assets/Resources/levels.json').levels) seen.add(l.grid.join('|'));
} catch (e) { /* 없어도 된다 */ }
const baseKey = g => g.map(row => row.replace(/[!5]/g, '.').replace(/4/g, '#')).join('|');
const perBase = new Map();
for (const p of out) perBase.set(baseKey(p.grid), (perBase.get(baseKey(p.grid)) || 0) + 1);
//  🔴 **게임에 이미 들어간 판도 센다** (09-23). 안 그러면 씨앗만 바꿔 다시 돌릴 때
//     같은 원판에서 또 PER 판씩 뽑아 놓고, 넣을 때 통째로 중복으로 버려진다
//     (2회차에 248판 중 246판이 그렇게 버려졌다).
try {
  for (const l of require('../game/Assets/Resources/levels.json').levels) {
    //  🔴 **같은 기믹 판만** 센다 — 원판 그 자체(기믹 없는 판)는 세면 안 된다
    if (!(l.gim || '').startsWith('switch')) continue;
    const k = baseKey(l.grid);
    perBase.set(k, (perBase.get(k) || 0) + 1);
  }
} catch (e) { }
const r = rng(+(process.env.SEED || 11));
console.log('스위치 판 · 재료 ' + src.length + '판 · 있던 판 ' + out.length + '개에 이어서 · ' + MIN + '분');

const t0 = Date.now();
let tried = 0, solved = 0;
const rej = { nosolve: 0, capped: 0, multi: 0, short: 0, notoggle: 0, nodiff: 0 };
let si = Math.floor(r() * src.length);
while (Date.now() - t0 < MIN * 60000 && out.length < WANT) {
  const base = src[si++ % src.length];
  const bkey = base.grid.join('|');
  const clear = base.clear || 'all';
  let basePath = null;
  try { basePath = pathCells(base); } catch (e) { basePath = null; }
  for (let k = 0; k < TRIES && out.length < WANT; k++) {
    if ((perBase.get(bkey) || 0) >= PER) break;
    const grid = mutate(base.grid, r, basePath);
    if (!grid) break;
    const key = grid.join('|');
    if (seen.has(key)) continue;
    seen.add(key);
    tried++;
    let a;
    try { a = E.solve({ grid, gravity: true, clear, id: 's' }, { cap: CAP }); } catch (e) { continue; }
    if (!a || !a.ok) { rej.nosolve++; continue; }
    if (a.capped) { rej.capped++; continue; }
    if (a.shortest !== 1) { rej.multi++; continue; }
    if (a.moves < MOVES) { rej.short++; continue; }
    solved++;
    const tg = toggles(grid, clear, a.path);
    if (tg < TOGGLES) { rej.notoggle++; continue; }
    //  🔴 스위치를 빼도 똑같이 풀리면 누르는 건 장식이다
    let b;
    try { b = E.solve({ grid: grid.map(x => x.replace(/!/g, '.')), gravity: true, clear, id: 's0' }, { cap: CAP }); } catch (e) { b = null; }
    if (b && b.ok && !b.capped && b.moves - a.moves < DIFF) { rej.nodiff++; continue; }
    perBase.set(bkey, (perBase.get(bkey) || 0) + 1);
    out.push({ grid, clear, best: a.moves, sol: a.path, states: a.states, toggles: tg,
               without: b && b.ok ? b.moves : null, gim: 'switch' });
    fs.writeFileSync(OUT, JSON.stringify(out, null, 1) + '\n');   // 🔴 한 판마다 저장
    console.log('  ★ ' + out.length + '판째 · ' + grid[0].length + 'x' + grid.length + ' · ' + a.moves +
                '걸음 · 누름 ' + tg + ' · 스위치 없으면 ' + (b && b.ok ? b.moves : '못 풂') + ' · ' +
                ((Date.now() - t0) / 60000).toFixed(1) + '분');
  }
}
console.log('\n해봄 ' + tried + ' · 풀린 판 ' + solved + ' · **받은 판 ' + out.length + '** → ' + OUT);
