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
  // 🔴 09-05 밤 다시 짬 — 사장님이 짚어주신 것으로 다시 세웠다.
  //
  //   쓰는 것 : 홈 셋 · 포탈 · **홈 꺾음 4 이상** · 좁게(빽빽하게)
  //   빼는 것 : 무중력("진짜 별로였음") · 상자(4판 중 1판만 괜찮았음) · **걸음 수 목표**
  //
  //   ⚠️ 판 크기는 **19x11 이하**. 카메라가 묶음에서 제일 큰 판에 맞춰지니,
  //      큰 판을 하나 넣으면 옛 17판(최대 19x11)이 전부 작아 보인다.
  //   ⚠️ `걸음 수는 난이도가 아니다` — wander 를 목표로 안 쓴다. 좁게 파는 것으로 밀도를 낸다.
  { d: 3, W: 13, H: 10, ratio: 0.30, wmin: 8,  wmax: 20, lens: [4, 3, 3], turn: 3 },
  { d: 3, W: 15, H: 10, ratio: 0.29, wmin: 10, wmax: 24, lens: [5, 4, 3], turn: 4 },
  { d: 3, W: 15, H: 11, ratio: 0.29, wmin: 12, wmax: 26, lens: [5, 4, 4], turn: 4 },
  { d: 3, W: 17, H: 10, ratio: 0.28, wmin: 12, wmax: 28, lens: [6, 4, 3], turn: 4 },
  { d: 3, W: 17, H: 11, ratio: 0.28, wmin: 14, wmax: 30, lens: [6, 5, 3], turn: 4 },
  { d: 3, W: 19, H: 11, ratio: 0.27, wmin: 16, wmax: 34, lens: [6, 5, 4], turn: 4 },
  { d: 2, W: 13, H: 10, ratio: 0.31, wmin: 8,  wmax: 20, lens: [6, 4],    turn: 4 },
  { d: 2, W: 15, H: 11, ratio: 0.29, wmin: 10, wmax: 24, lens: [6, 5],    turn: 4 },
  // ── 포탈 (사장님: 재미의 원인) ────────────────
  { d: 2, W: 15, H: 10, ratio: 0.30, wmin: 10, wmax: 24, lens: [5, 4],    turn: 3, port: 1 },
  { d: 3, W: 17, H: 11, ratio: 0.28, wmin: 12, wmax: 28, lens: [5, 4, 3], turn: 4, port: 1 },
  { d: 3, W: 19, H: 11, ratio: 0.28, wmin: 14, wmax: 32, lens: [6, 4, 3], turn: 4, port: 1 },
];

//  🔴 재는 게 너무 비싼 판은 안 받는다 — 어려운 것과 재기 비싼 것은 다르다
const MAXST = +(process.env.MAXST || 800000);




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
                        0, 0, b.port || 0, b.turn || 0);
      if (!grid) continue;
      //  🔴 기믹을 시켰는데 안 들어간 판은 안 받는다 — 그 띠가 있는 뜻이 없어진다
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
      //  🔴 **포탈은 없으면 못 푸는 것만** (09-05 사장님: "포털이 꼭 필요하게 써야한다").
      if (b.port) {
        const strip = grid.map(x => x.replace(/[PQpq]/g, '.'));
        const c = E.solve({ grid: strip, gravity: true, clear: 'all', id: 'x' });
        if (c.ok) continue;
      }
      seen.add(key);
      pool.push({
        doors: b.d, shape, grid, best: a.moves, sol: a.path,
        W: b.W, H: b.H, ratio: b.ratio,
        port: b.port || 0, turn: b.turn || 0,
        gim: b.port ? 'portal' : 'none',
      });
      got++;
    }
  }
  const tag = b.port ? ' 포탈' : '';
  console.log(`  ${b.d}홈 ${b.W}x${b.H} ratio ${b.ratio}${tag} → ${got}개`);
}
fs.writeFileSync(OUT, JSON.stringify(pool, null, 1) + '\n');
console.log(`\n못 ${pool.length}개 · ${((Date.now() - t0) / 1000).toFixed(0)}초 → ${OUT}`);
console.log('다음: CAP=900000 node tools/poolstat.js tools/hard-pool.json');
