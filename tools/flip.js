// 🔴 **반전벽 판**을 짓는다 (2026-09-22) — 굴 9 「뒤바뀌는 바닥」 · 굴 10 재료.
//
// 사장님: *"홈 하면 못밟던 바닥이 생기는 기믹? 약간 슈퍼마리오의 반전 블럭?"*
//        → *"마리오 P스위치랑 비슷한거 맞아"* → 규칙: **둘 다 뒤바뀐다.**
//
// 엔진(engine.js)의 두 기호:
//   `1` `2` `3` 문벽   — 짝이 되는 홈을 채우면 **사라진다** (원래 있던 것)
//   `4` `5` `6` 반전벽 — 짝이 되는 홈을 채우면 **생긴다**   (09-22 새로 넣음)
//   같은 번호끼리(1↔4) 같은 홈에 묶인다. 한 홈이 두 칸을 동시에 뒤집는다.
//
// 🔴 판 짓는 도구(grow.js)를 뜯어고치지 않는다. **이미 풀리는 판에 얹어 본다** —
//    빈칸 몇 개를 `4` 로, 속 벽 몇 개를 `1` 로 바꾸고 다시 푼다.
//
// 받는 조건 (위키: 도구가 거르는 건 **객관적인 것만**)
//   · 풀린다 · 최단해가 하나 · 걸음 하한 이상
//   · 🔴 **걸음 수가 원판과 달라졌다** — 그대로면 반전벽이 **장식**이다.
//     09-20 에 포탈이 "있기만 하고 안 쓰인" 판이 잔뜩 섞여 들어온 걸 배웠다.
//
//   node tools/flip.js
//   MIN=60 WANT=80 SEED=5 node tools/flip.js
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const MIN = +(process.env.MIN || 40);
const WANT = +(process.env.WANT || 60);
const CAP = +(process.env.SOLVE_CAP || 2000000);
const MOVES = +(process.env.MOVES || 18);
const TRIES = +(process.env.TRIES || 8);           // 판 하나에 몇 가지 배치를 해보나
//  🔴 **원판 하나에서 몇 판까지 받나.** 처음엔 제한이 없어 80판이 **원판 10개**에서 나왔다
//     (원판 하나에 최대 14판) — 반전벽 자리만 바꾼 **사실상 같은 판**이 줄줄이 섞였다 (09-22).
const PER = +(process.env.PER || 2);
//  🔴 **원판과 걸음이 이만큼은 달라야** 반전벽이 퍼즐에 관여한 것이다.
//     처음엔 "다르기만 하면" 받았더니 80판 중 37판이 **1걸음 차이**였다 — 스치고 지나간 정도다.
const DIFF = +(process.env.DIFF || 3);
const OUT = process.env.OUT || path.join(__dirname, 'flip-pool.json');

//  재료 — 이미 재어 둔 못. 첫 홈(= *)이 있어야 반전벽 4 가 묶일 데가 있다
const src = JSON.parse(fs.readFileSync(path.join(__dirname, 'rated.json'), 'utf8'))
  .filter(p => p.grid && p.best && /[=*]/.test(p.grid.join('')));

//  0~1 난수 (씨앗으로 늘 같게)
function rng(seed) { let s = seed >>> 0 || 1; return () => ((s = (s * 1664525 + 1013904223) >>> 0) / 4294967296); }

/// 빈칸 몇 개를 반전벽으로, 속 벽 몇 개를 문벽으로 바꾼 격자
function mutate(grid, r) {
  const g = grid.map(row => row.split(''));
  const H = g.length, W = g[0].length;
  const empty = [], wall = [];
  for (let y = 1; y < H - 1; y++)
    for (let x = 1; x < W - 1; x++) {
      if (g[y][x] === '.') empty.push([x, y]);
      //  🔴 **속 벽만** 문벽으로 — 테두리를 뚫으면 판 밖으로 샌다
      else if (g[y][x] === '#') wall.push([x, y]);
    }
  if (!empty.length) return null;
  const pick = (arr, n) => { const out = []; for (let i = 0; i < n && arr.length; i++) out.push(arr.splice(Math.floor(r() * arr.length), 1)[0]); return out; };
  const nFlip = 1 + Math.floor(r() * 3);                   // 반전벽 1~3 칸
  const nGate = Math.floor(r() * 3);                       // 문벽 0~2 칸
  for (const [x, y] of pick(empty, nFlip)) g[y][x] = '4';
  for (const [x, y] of pick(wall, nGate)) g[y][x] = '1';
  return g.map(row => row.join(''));
}

const out = fs.existsSync(OUT) ? JSON.parse(fs.readFileSync(OUT, 'utf8')) : [];
const seen = new Set(out.map(p => p.grid.join('|')));
//  원판별로 몇 판 받았나 — 반전벽/문벽을 원래 칸으로 되돌린 격자로 원판을 알아본다
const baseKey = g => g.map(row => row.replace(/[4-6]/g, '.').replace(/[1-3]/g, '#')).join('|');
const perBase = new Map();
for (const p of out) perBase.set(baseKey(p.grid), (perBase.get(baseKey(p.grid)) || 0) + 1);
const r = rng(+(process.env.SEED || 7));
console.log('반전벽 판 · 재료 ' + src.length + '판 · 있던 판 ' + out.length + '개에 이어서 · ' + MIN + '분');

const t0 = Date.now();
let tried = 0, solved = 0;
let si = Math.floor(r() * src.length);
while (Date.now() - t0 < MIN * 60000 && out.length < WANT) {
  const base = src[si++ % src.length];
  const bkey = base.grid.join('|');
  for (let k = 0; k < TRIES && out.length < WANT; k++) {
    if ((perBase.get(bkey) || 0) >= PER) break;          // 이 원판은 넉넉히 받았다
    const grid = mutate(base.grid, r);
    if (!grid) break;
    const key = grid.join('|');
    if (seen.has(key)) continue;
    tried++;
    let a;
    try { a = E.solve({ grid, gravity: true, clear: base.clear || 'all', id: 'f' }, { cap: CAP }); }
    catch (e) { continue; }
    if (!a || !a.ok || a.capped || a.shortest !== 1 || a.moves < MOVES) continue;
    solved++;
    //  🔴 원판과 걸음이 **DIFF 이상** 달라야 반전벽이 퍼즐에 관여한 것이다 — 아니면 장식이다
    if (Math.abs(a.moves - base.best) < DIFF) continue;
    seen.add(key);
    perBase.set(bkey, (perBase.get(bkey) || 0) + 1);
    out.push({ grid, clear: base.clear || 'all', best: a.moves, sol: a.path, states: a.states,
               baseBest: base.best, gim: 'flip' });
    fs.writeFileSync(OUT, JSON.stringify(out, null, 1) + '\n');   // 🔴 한 판마다 저장
    console.log('  ★ ' + out.length + '판째 · ' + grid[0].length + 'x' + grid.length + ' · ' + a.moves +
                '걸음 (원판 ' + base.best + ') · ' + ((Date.now() - t0) / 60000).toFixed(1) + '분');
  }
}
console.log('\n해봄 ' + tried + ' · 풀린 판 ' + solved + ' · **받은 판 ' + out.length + '** → ' + OUT);
