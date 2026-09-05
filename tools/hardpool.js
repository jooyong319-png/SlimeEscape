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

const OUT = process.env.OUT || (process.env.CRACK === '1' ? 'tools/crack-pool.json'
                              : process.env.ICE === '1' ? 'tools/ice-pool.json'
                              : process.env.HARD === '1' ? 'tools/brutal-pool.json'
                              : 'tools/hard-pool.json');

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

// 🔴 **어려운 띠** (HARD=1) — 2026-09-05 밤.
//    사장님: *"막 어렵다! 이런 판이 없어. 그냥 조금 고민하면 어지간해선 풀리는 정도."*
//    30판 중 28판이 "괜찮다" 였지만 **아무것도 「막 어렵다」가 아니었다.**
//
//    안 쓴 지렛대가 하나 남아 있었다 — 옛 1-16 의 **구멍 뚫린 홈**이다.
//    홈이 ㄷ자라 가운데가 뚫려 있고, **그 구멍으로 내려가야** 바닥 조각에 닿는다.
//    자기가 채워야 할 홈을 통과해서 갔다 돌아오는 것 — 사람은 이걸 잘 상상 못 한다.
//
//    ⚠️ 수율이 낮다 (씨앗 700개에 쓸 만한 판 5개). 오래 걸린다.
const HARD_BANDS = [
  { d: 3, W: 15, H: 11, ratio: 0.30, wmin: 12, wmax: 28, lens: [6, 4, 3], turn: 3, hole: 1 },
  { d: 3, W: 17, H: 11, ratio: 0.29, wmin: 14, wmax: 30, lens: [6, 5, 3], turn: 3, hole: 1 },
  { d: 3, W: 17, H: 10, ratio: 0.30, wmin: 12, wmax: 28, lens: [7, 4, 3], turn: 3, hole: 1 },
  { d: 2, W: 15, H: 11, ratio: 0.30, wmin: 12, wmax: 26, lens: [7, 4],    turn: 3, hole: 1 },
  { d: 3, W: 17, H: 11, ratio: 0.29, wmin: 14, wmax: 30, lens: [6, 4, 3], turn: 3, hole: 1, port: 1 },
  { d: 3, W: 19, H: 11, ratio: 0.29, wmin: 16, wmax: 32, lens: [6, 5, 4], turn: 3, hole: 1, port: 1 },
];

// 🔴 **얼음 띠** (ICE=1) — 2026-09-05 밤, 사장님이 고르신 기믹.
//    "어려운 기믹 하나를 추가하는건?" → 얼음 + 금 간 바닥.
//
//    얼음이 어려운 까닭: **원하는 칸에 못 선다.** 서려면 먼저 막을 것을 갖다 놔야 하고,
//    그 막을 것이 결국 **자기 몸**이다 — "저기 서려고 여기에 몸을 깔아둔다".
//    한 수 앞이 아니라 세 수 앞을 보게 만드는 유일한 기믹이다.
//
//    ⚠️ 얼음은 **가로로 이어진 빈 칸**이 있어야 놓인다 → 굴을 좀 더 넓게 판다 (ratio 0.34~0.38).
//    ⚠️ 홈은 빈 칸에만 굳으므로 얼음이 몸 밑에 깔리면 판이 버려진다 → 걷기 앞부분에만 놓는다.
const ICE_BANDS = [
  { d: 2, W: 15, H: 11, ratio: 0.34, wmin: 10, wmax: 24, lens: [5, 4],    turn: 2, ice: 8 },
  { d: 2, W: 15, H: 10, ratio: 0.36, wmin: 10, wmax: 24, lens: [5, 4],    turn: 2, ice: 8 },
  { d: 2, W: 17, H: 11, ratio: 0.34, wmin: 12, wmax: 26, lens: [6, 4],    turn: 3, ice: 9 },
  { d: 2, W: 13, H: 10, ratio: 0.38, wmin: 10, wmax: 22, lens: [5, 4],    turn: 2, ice: 7 },
  { d: 3, W: 17, H: 11, ratio: 0.34, wmin: 12, wmax: 28, lens: [5, 4, 3], turn: 3, ice: 9 },
  { d: 3, W: 19, H: 11, ratio: 0.33, wmin: 14, wmax: 30, lens: [6, 4, 3], turn: 3, ice: 10 },
  //  얼음 + 포탈 — 사장님이 재밌다 하신 것과 어렵다 할 것을 겹친다
  { d: 2, W: 17, H: 11, ratio: 0.34, wmin: 12, wmax: 26, lens: [6, 4],    turn: 3, ice: 8, port: 1 },
  { d: 3, W: 19, H: 11, ratio: 0.33, wmin: 14, wmax: 30, lens: [6, 4, 3], turn: 3, ice: 9, port: 1 },
];

// 🔴 **금 간 바닥 띠** (CRACK=1) — 2026-09-05 밤.
//    지나온 길이 무너져 **두 번 못 쓴다.** 그래서 순서를 처음부터 정해야 한다.
//    죽는 게 없으니 벌이 아니라 **계획 문제**다.
//    ⚠️ 부서진 자리가 상태에 들어간다 (2^n) — 한 판에 넷을 넘기지 않는다.
//    ⚠️ 얼음과 겹쳐 쓴다. 둘 다 "되돌릴 수 없다"는 같은 말을 하므로 결이 맞는다.
const CRACK_BANDS = [
  { d: 2, W: 15, H: 11, ratio: 0.34, wmin: 10, wmax: 24, lens: [5, 4],    turn: 2, crack: 4 },
  { d: 2, W: 15, H: 10, ratio: 0.35, wmin: 10, wmax: 24, lens: [6, 4],    turn: 3, crack: 4 },
  { d: 2, W: 17, H: 11, ratio: 0.33, wmin: 12, wmax: 26, lens: [6, 4],    turn: 3, crack: 4 },
  { d: 3, W: 17, H: 11, ratio: 0.33, wmin: 12, wmax: 28, lens: [5, 4, 3], turn: 3, crack: 3 },
  //  얼음 + 금 간 바닥 — 둘 다 "되돌릴 수 없다"
  { d: 2, W: 15, H: 11, ratio: 0.35, wmin: 10, wmax: 24, lens: [5, 4],    turn: 2, crack: 3, ice: 6 },
  { d: 2, W: 17, H: 11, ratio: 0.34, wmin: 12, wmax: 26, lens: [6, 4],    turn: 3, crack: 3, ice: 7 },
  { d: 3, W: 17, H: 11, ratio: 0.34, wmin: 12, wmax: 28, lens: [5, 4, 3], turn: 3, crack: 3, ice: 7 },
  { d: 3, W: 19, H: 11, ratio: 0.33, wmin: 14, wmax: 30, lens: [6, 4, 3], turn: 3, crack: 4, ice: 8 },
];

//  🔴 재는 게 너무 비싼 판은 안 받는다 — 어려운 것과 재기 비싼 것은 다르다
const MAXST = +(process.env.MAXST || 800000);




/// 🔴 이 정답이 그 판에서 **그대로 통하는가.** 통하면 지형을 바꾼 뜻이 없다.
function replays(grid, path) {
  const L = E.parse({ grid, gravity: true, clear: 'all', id: 'r' });
  let st = E.startState(L);
  const D = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };
  for (const ch of path) { const n = E.step(L, st, D[ch]); if (!n) return false; st = n; }
  return E.isWin(L, st, false);
}

const PER = +(process.env.PER || 22);
const SEED0 = +(process.env.SEED || 1);

const pool = fs.existsSync(OUT) ? JSON.parse(fs.readFileSync(OUT, 'utf8')) : [];
const seen = new Set(pool.map(p => p.grid.join('')));
console.log('있던 판', pool.length, '개에 이어서 만든다');

const t0 = Date.now();
const SET = process.env.CRACK === '1' ? CRACK_BANDS
          : process.env.ICE === '1' ? ICE_BANDS
          : process.env.HARD === '1' ? HARD_BANDS : BANDS;
for (const b of SET) {
  let got = 0;
  for (let seed = SEED0; seed <= SEED0 + 900 && got < PER; seed++) {
    const r = rng(seed * 7919 + b.W * 131 + b.H * 17 + Math.round(b.ratio * 1000));
    for (let n = 0; n < 120 && got < PER; n++) {
      const shape = SHAPES[Math.floor(r() * SHAPES.length)];
      const grid = grow(r, b.W, b.H, b.lens, b.ratio, b.wmin, b.wmax, shape,
                        0, 0, b.port || 0, b.turn || 0, b.hole || 0, b.ice || 0, b.crack || 0);
      if (!grid) continue;
      //  🔴 기믹을 시켰는데 안 들어간 판은 안 받는다 — 그 띠가 있는 뜻이 없어진다
      if (b.port && !grid.join('').includes('P')) continue;
      if (b.ice && !grid.join('').includes('I')) continue;
      if (b.crack && !grid.join('').includes('X')) continue;
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
      //  🔴 얼음도 **없으면 못 풀거나, 있어서 더 걸려야** 뜻이 있다.
      //     얼음을 그냥 바닥으로 바꿔도 같은 걸음 수로 풀리면 그건 장식이다.
      //  🔴 얼음을 **그냥 벽으로** 굳혀도 같은 걸음 수로 풀리면 미끄러진 뜻이 없다 (rev.12-a)
      if (b.ice) {
        const flat = grid.map(x => x.replace(/I/g, '#'));
        const c = E.solve({ grid: flat, gravity: true, clear: 'all', id: 'x' });
        if (c.ok && c.moves >= a.moves) continue;
      }
      //  🔴 금 간 바닥도 **안 무너져도 똑같이 풀리면 장식**이다.
      //     X 를 그냥 벽('#')으로 굳혀 푼 답이 **이 판에서도 통하면** 무너뜨린 뜻이 없다.
      //     (X→# 는 한 쪽으로만 쉬워지므로, 통하면 그건 원래 안 밟고도 되는 길이었다는 뜻)
      if (b.crack) {
        const hard = grid.map(x => x.replace(/X/g, '#'));
        const c = E.solve({ grid: hard, gravity: true, clear: 'all', id: 'x' });
        if (c.ok && replays(grid, c.path)) continue;
      }
      seen.add(key);
      pool.push({
        doors: b.d, shape, grid, best: a.moves, sol: a.path,
        W: b.W, H: b.H, ratio: b.ratio,
        port: b.port || 0, turn: b.turn || 0, ice: b.ice || 0, crack: b.crack || 0,
        gim: [b.ice ? 'ice' : '', b.crack ? 'crack' : '', b.port ? 'portal' : '']
               .filter(Boolean).join('+') || 'none',
      });
      got++;
    }
  }
  //  🔴 **띄마다 저장한다.** 맨 끕에서 한 번만 쓰면 도중에 끊겼을 때
  //     몇백 분치가 통째로 날아간다 (09-05 에 그럴 뻔했다).
  fs.writeFileSync(OUT, JSON.stringify(pool, null, 1) + '\n');
  const tag = (b.ice ? ' 얼음' : '') + (b.crack ? ' 금간바닥' : '') + (b.port ? ' 포탈' : '');
  console.log(`  ${b.d}홈 ${b.W}x${b.H} ratio ${b.ratio}${tag} → ${got}개`);
}
fs.writeFileSync(OUT, JSON.stringify(pool, null, 1) + '\n');
console.log(`\n못 ${pool.length}개 · ${((Date.now() - t0) / 1000).toFixed(0)}초 → ${OUT}`);
console.log('다음: CAP=900000 node tools/poolstat.js tools/hard-pool.json');
