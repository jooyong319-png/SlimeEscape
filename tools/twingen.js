// 🔴 **슬라임 둘 판**을 짓는다 (09-23) — 굴 11 「둘이서」 · 굴 12 재료.
//
// 사장님: "서로 몸은 올라타지는 것 · 겹치는 건 안 됨 · 교체는 키보드로 할지 맵 버튼으로 할지 고민"
//   → 두 방식 다 뽑는다. MODE=key (교체 칸 없음 · ⇄ 한 걸음) / MODE=tile ('&' 교체 칸 둘)
//
// 이미 풀리는 판에 **둘째 슬라임 'Z'** (발 디딜 데가 있는 빈칸)를 얹고, tile 이면 '&' 둘을 더 얹는다.
//
// 받는 조건 (도구는 **객관적인 것만** 거른다)
//   · 풀린다 · 최단해가 하나 · 걸음 하한 이상
//   · 🔴 정답에서 조종이 **SWAPS 번 이상** 넘어간다 — 안 넘어가면 둘째는 그냥 벽이다
//   · 🔴 **둘째를 그 자리 벽으로 박으면 못 풀거나 DIFF 걸음 이상 길어진다** — 둘째를 움직여야 풀린다
//   · 원판 하나에서 PER 판까지 · 격자가 같은 판은 한 번만
//
//   MODE=key MIN=60 WANT=100 node tools/twingen.js
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const MODE = process.env.MODE || 'key';
const MIN = +(process.env.MIN || 40);
const WANT = +(process.env.WANT || 80);
const CAP = +(process.env.SOLVE_CAP || 1500000);
const MOVES = +(process.env.MOVES || 14);
const TRIES = +(process.env.TRIES || 12);
const PER = +(process.env.PER || 2);
const DIFF = +(process.env.DIFF || 3);
const SWAPS = +(process.env.SWAPS || 2);
const OUT = process.env.OUT || path.join(__dirname, 'twin-' + MODE + '-pool.json');

//  🔴 작은 판만 — 슬라임 둘은 상태가 제곱으로 는다
//  🔴 재료 — 재어 둔 못(rated.json) **+ 게임에 들어간 기믹 없는 판**.
//     못만 쓰면 원판이 금세 동나서(PER 만큼 다 뽑으면) 나오는 속도가 뚝 떨어진다 (09-23).
const pool0 = JSON.parse(fs.readFileSync(path.join(__dirname, 'rated.json'), 'utf8'));
try {
  for (const l of require('../game/Assets/Resources/levels.json').levels)
    if (!l.gim || l.gim === 'plain' || /^(box|ice|crack|portal)/.test(l.gim)) pool0.push(l);
} catch (e) { }
const src = pool0
  .filter(p => p.grid && p.best && !/[1-6!&Z]/.test(p.grid.join('')) && p.grid.length * p.grid[0].length <= 150);

function rng(seed) { let s = seed >>> 0 || 1; return () => ((s = (s * 1664525 + 1013904223) >>> 0) / 4294967296); }

function mutate(grid, r) {
  const g = grid.map(row => row.split(''));
  const H = g.length, W = g[0].length;
  const floor = [];
  for (let y = 1; y < H - 1; y++)
    for (let x = 1; x < W - 1; x++)
      if (g[y][x] === '.' && '#IXB'.includes(g[y + 1][x])) floor.push([x, y]);
  const need = MODE === 'tile' ? 3 : 1;
  if (floor.length < need) return null;
  const pick = () => floor.splice(Math.floor(r() * floor.length), 1)[0];
  const [zx, zy] = pick(); g[zy][zx] = 'Z';
  if (MODE === 'tile') for (let i = 0; i < 2; i++) { const [x, y] = pick(); g[y][x] = '&'; }
  return g.map(row => row.join(''));
}

/// 정답에서 조종이 몇 번 넘어가나
function swaps(grid, clear, sol) {
  const L = E.parse({ grid, gravity: true, clear });
  let st = E.startState(L), n = 0;
  const D = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4, '⇄': 5 };
  for (const ch of sol) {
    const ns = E.step(L, st, D[ch]);
    if (!ns) return -1;
    if (ns._sw2) n++;
    st = ns;
  }
  return n;
}

const out = fs.existsSync(OUT) ? JSON.parse(fs.readFileSync(OUT, 'utf8')) : [];
const seen = new Set(out.map(p => p.grid.join('|')));
try { for (const l of require('../game/Assets/Resources/levels.json').levels) seen.add(l.grid.join('|')); } catch (e) { }
const baseKey = g => g.map(row => row.replace(/[Z&]/g, '.')).join('|');
const perBase = new Map();
for (const p of out) perBase.set(baseKey(p.grid), (perBase.get(baseKey(p.grid)) || 0) + 1);
//  🔴 **게임에 이미 들어간 판도 센다** (09-23). 안 그러면 씨앗만 바꿔 다시 돌릴 때
//     같은 원판에서 또 PER 판씩 뽑아 놓고, 넣을 때 통째로 중복으로 버려진다
//     (2회차에 248판 중 246판이 그렇게 버려졌다).
try {
  for (const l of require('../game/Assets/Resources/levels.json').levels) {
    //  🔴 **같은 기믹 판만** 센다 — 원판 그 자체(기믹 없는 판)는 세면 안 된다
    if (!(l.gim || '').startsWith('twin')) continue;
    const k = baseKey(l.grid);
    perBase.set(k, (perBase.get(k) || 0) + 1);
  }
} catch (e) { }
const r = rng(+(process.env.SEED || 23));
console.log('슬라임 둘(' + MODE + ') · 재료 ' + src.length + '판 · 있던 판 ' + out.length + '개에 이어서 · ' + MIN + '분');

const t0 = Date.now();
let tried = 0, solved = 0;
let si = Math.floor(r() * src.length);
while (Date.now() - t0 < MIN * 60000 && out.length < WANT) {
  const base = src[si++ % src.length];
  const bkey = base.grid.join('|');
  const clear = base.clear || 'all';
  for (let k = 0; k < TRIES && out.length < WANT; k++) {
    if ((perBase.get(bkey) || 0) >= PER) break;
    const grid = mutate(base.grid, r);
    if (!grid) break;
    const key = grid.join('|');
    if (seen.has(key)) continue;
    seen.add(key);
    tried++;
    let a;
    try { a = E.solve({ grid, gravity: true, clear, id: 't' }, { cap: CAP }); } catch (e) { continue; }
    if (!a || !a.ok || a.capped || a.shortest !== 1 || a.moves < MOVES) continue;
    solved++;
    const sw = swaps(grid, clear, a.path);
    if (sw < SWAPS) continue;
    //  🔴 둘째를 **움직이는 게** 퍼즐이어야 한다 — 둘째를 그 자리 **벽**('#')으로 박아도
    //     똑같이 풀리면, 둘째는 장애물일 뿐이고 둘일 까닭이 없다
    let w;
    try { w = E.solve({ grid: grid.map(x => x.replace('Z', '#').replace(/&/g, '.')), gravity: true, clear, id: 'tw' }, { cap: CAP }); } catch (e) { w = null; }
    if (w && w.ok && !w.capped && w.moves - a.moves < DIFF) continue;
    perBase.set(bkey, (perBase.get(bkey) || 0) + 1);
    out.push({ grid, clear, best: a.moves, sol: a.path, states: a.states, swaps: sw,
               baseBest: base.best, asWall: w && w.ok ? w.moves : null, gim: 'twin-' + MODE });
    fs.writeFileSync(OUT, JSON.stringify(out, null, 1) + '\n');
    console.log('  ★ ' + out.length + '판째 · ' + grid[0].length + 'x' + grid.length + ' · ' + a.moves +
                '걸음 (원판 ' + base.best + ') · 교체 ' + sw + ' · ' + ((Date.now() - t0) / 60000).toFixed(1) + '분');
  }
}
console.log('\n해봄 ' + tried + ' · 풀린 판 ' + solved + ' · **받은 판 ' + out.length + '** → ' + OUT);
