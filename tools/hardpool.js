// 🔴 **조이는 판**을 만들어 못에 쌓는다 (2026-09-04).
//
// 왜 새로 만드나: 기존 못 284개는 압박 20% 이상이 14개뿐이었다. 고르기로는 47판을 못 채운다.
// 실험(tools/sweep.js)이 손잡이를 짚어줬다 — **좁은 굴 + 홈 셋 + 높은 판**.
//
//   node tools/hardpool.js
//   N=200 SEED=1 node tools/hardpool.js       못을 더 크게
//
// 만들기만 한다. 재는 것(압박)은 비싸서 따로 돌린다 → tools/poolstat.js
'use strict';
const fs = require('fs');
const E = require('./engine.js').SlimeEngine;
const { grow, rng, SHAPES } = require('./grow.js');

const OUT = 'tools/hard-pool.json';

// 🔴 **크기를 여러 가지로** (09-04 사장님: "맵 크기도 좀 랜덤으로 다양성 있게").
//    같은 크기만 찍어내면 마흔 판이 다 비슷하게 생긴다.
//    ratio 는 0.24~0.30 — 실험에서 0.26 근처가 제일 조였다.
//    0.22 까지 좁히면 오히려 내려간다 (갈 데가 없으면 고민이 아니라 외길이 된다).
// 🔴 기믹 예산 (rev.10, 2026-09-05) — `zone` 무중력 칸 · `box` 미는 상자.
//    재본 결과: 덤 조각은 압박을 세 배 올리고, 문벽은 중립, 무중력은 **내린다**.
//    그러니 기믹은 **다양성**으로 쓰고, 어려움은 덤 조각에 맡긴다.
//    한 판에 기믹 하나씩만 — 셋을 겹치면 무슨 게임인지 안 읽힌다.
const BANDS = [
  // ── 앞 (짧고 조인다) ──────────────────────────
  { d: 1, W: 11, H: 9,  ratio: 0.30, wmin: 3,  wmax: 8,  lens: [3] },
  { d: 1, W: 13, H: 10, ratio: 0.28, wmin: 6,  wmax: 14, lens: [4] },
  { d: 2, W: 13, H: 10, ratio: 0.29, wmin: 8,  wmax: 18, lens: [3, 3] },
  // ── 가운데 ───────────────────────────────────
  { d: 2, W: 15, H: 11, ratio: 0.31, wmin: 16, wmax: 28, lens: [4, 3] },
  { d: 2, W: 17, H: 12, ratio: 0.33, wmin: 22, wmax: 36, lens: [4, 4] },
  { d: 3, W: 15, H: 11, ratio: 0.31, wmin: 20, wmax: 34, lens: [3, 3, 3] },
  // ── 🔴 뒤 (훨씬 길게) ─────────────────────────
  //  09-05 밤 사장님: *"게임이 너무 쉽다. 전에 1~16까지만 있었을 땐 어렵다 생각했는데."*
  //  재보니 그 판들은 **제일 짧은 게 20걸음, 중앙 34** 였다. 지금 것은 중앙 19 다.
  //  판 하나에 쓰는 시간이 2.5분 → 1.0분으로 줄었다. **길이를 아예 안 보게 만든 게 나였다.**
  //  🔴 길이는 난이도의 전부가 아니지만 **아무것도 아닌 것도 아니다** —
  //     긴 판은 앞을 내다봐야 하고, 그게 사장님이 느끼신 어려움이다.
  { d: 2, W: 19, H: 12, ratio: 0.38, wmin: 34, wmax: 52, lens: [5, 4] },
  { d: 3, W: 19, H: 13, ratio: 0.40, wmin: 38, wmax: 58, lens: [4, 4, 3] },
  { d: 3, W: 21, H: 12, ratio: 0.42, wmin: 44, wmax: 66, lens: [5, 4, 3] },
  { d: 3, W: 22, H: 13, ratio: 0.42, wmin: 50, wmax: 75, lens: [5, 4, 4] },
  { d: 2, W: 14, H: 15, ratio: 0.38, wmin: 36, wmax: 56, lens: [5, 4] },   // 세로로 긴 것
  { d: 3, W: 22, H: 10, ratio: 0.40, wmin: 40, wmax: 60, lens: [4, 4, 3] }, // 가로로 긴 것
  // ── 기믹 판 (한 판에 하나씩만) ────────────────
  { d: 1, W: 13, H: 10, ratio: 0.30, wmin: 8,  wmax: 18, lens: [4],       zone: 2 },
  { d: 2, W: 17, H: 12, ratio: 0.34, wmin: 22, wmax: 38, lens: [4, 3],    zone: 2 },
  { d: 1, W: 11, H: 9,  ratio: 0.30, wmin: 4,  wmax: 12, lens: [3],       box: 1 },
  { d: 2, W: 15, H: 11, ratio: 0.32, wmin: 16, wmax: 30, lens: [3, 3],    box: 1 },
  { d: 2, W: 17, H: 12, ratio: 0.34, wmin: 20, wmax: 36, lens: [3, 3],    port: 1 },
  { d: 3, W: 19, H: 13, ratio: 0.38, wmin: 34, wmax: 54, lens: [4, 3, 3], port: 1 },
];

//  🔴 상한을 크게 잡는다. 긴 판은 상태가 크다 — 09-05 낮에 30만으로 묶었더니
//     **긴 판이 통째로 걸러졌다.** 재는 게 비싼 것과 **판이 안 좋은 것은 다르다.**
const MAXST = +(process.env.MAXST || 1200000);



const PER = +(process.env.PER || 22);
const SEED0 = +(process.env.SEED || 1);

const pool = fs.existsSync(OUT) ? JSON.parse(fs.readFileSync(OUT, 'utf8')) : [];
const seen = new Set(pool.map(p => p.grid.join('')));
console.log('있던 판', pool.length, '개에 이어서 만든다');

const t0 = Date.now();
for (const b of BANDS) {
  let got = 0;
  for (let seed = SEED0; seed <= SEED0 + 900 && got < PER; seed++) {
    const r = rng(seed * 7919 + b.W * 131 + b.H * 17 + Math.round(b.ratio * 1000));
    for (let n = 0; n < 120 && got < PER; n++) {
      const shape = SHAPES[Math.floor(r() * SHAPES.length)];
      const grid = grow(r, b.W, b.H, b.lens, b.ratio, b.wmin, b.wmax, shape,
                        b.zone || 0, b.box || 0, b.port || 0);
      if (!grid) continue;
      //  🔴 기믹을 시켰는데 안 들어간 판은 안 받는다 — 그 띠가 있는 뜻이 없어진다
      if (b.zone && !grid.join('').includes(':')) continue;
      if (b.box && !grid.join('').includes('B')) continue;
      if (b.port && !grid.join('').includes('P')) continue;
      const key = grid.join('');
      if (seen.has(key)) continue;
      const a = E.solve({ grid, gravity: true, clear: 'all', id: 'g' });
      //  최단해가 유일한 것만. 🔴 이것만으로는 안 어렵다는 걸 09-04 에 알았지만,
      //  여러 답이 다 최단이면 "정답을 찾았다" 가 흐려지긴 한다. 그대로 둔다.
      if (!a.ok || a.shortest !== 1 || a.moves < 8) continue;
      if (a.states > MAXST) continue;
      //  🔴 **기믹이 진짜 쓰이는지** 본다. 없애도 똑같이 풀리면 그건 장식이다 —
      //     상자는 17판 중 4판, 포탈은 134판 중 19판이 그랬다 (09-05 측정).
      if (b.zone || b.box || b.port) {
        const strip = grid.map(x => x.replace(/[:BPQpq]/g, '.'));
        const c = E.solve({ grid: strip, gravity: true, clear: 'all', id: 'x' });
        //  🔴 **포탈은 없으면 못 푸는 것만** (09-05 사장님: "포털이 꼭 필요하게 써야한다").
        //     걸어서도 갈 수 있으면 포탈이 거기 있을 이유가 없다.
        if (b.port ? c.ok : (c.ok && c.moves === a.moves)) continue;
      }
      seen.add(key);
      pool.push({
        doors: b.d, shape, grid, best: a.moves, sol: a.path,
        W: b.W, H: b.H, ratio: b.ratio,
        zone: b.zone || 0, box: b.box || 0, port: b.port || 0,
        gim: b.box ? 'box' : b.port ? 'portal' : b.zone ? 'zone' : 'none',
      });
      got++;
    }
  }
  const tag = b.box ? ' 상자' : b.port ? ' 포탈' : b.zone ? ' 무중력' : '';
  console.log(`  ${b.d}홈 ${b.W}x${b.H} ratio ${b.ratio}${tag} → ${got}개`);
}
fs.writeFileSync(OUT, JSON.stringify(pool, null, 1) + '\n');
console.log(`\n못 ${pool.length}개 · ${((Date.now() - t0) / 1000).toFixed(0)}초 → ${OUT}`);
console.log('다음: CAP=900000 node tools/poolstat.js tools/hard-pool.json');
