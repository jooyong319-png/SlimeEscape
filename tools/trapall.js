// 🔴 못 전체에 **덤 조각**을 얹는다 (2026-09-04).
//
// 짝비교에서 압박이 6.5% → 24.5% 로 올랐다. 손잡이로는 3~5% 를 못 벗어났는데
// 덤 조각 셋이 네 배를 만들었다 — **처음으로 진짜 틀린 수를 만들기 때문**이다.
//
//   node tools/trapall.js
//   TRAPS=4 node tools/trapall.js
//
// 판 모양은 안 건드린다. 정답이 **안 지나가는 칸**에만 얹고,
// 얹은 뒤 최단해가 그대로인지 · 여전히 하나뿐인지 확인한다.
'use strict';
const fs = require('fs');
const E = require('./engine.js').SlimeEngine;

const IN  = process.argv[2] || 'tools/hard-pool.json';
const OUT = process.argv[3] || 'tools/trap-pool.json';
const WANT = +(process.env.TRAPS || 3);
const SYM = { '↑': 0, '↓': 1, '←': 2, '→': 3, '↧': 4 };

function headPath(L, sol) {
  const seen = new Set();
  let st = E.startState(L);
  seen.add(st.body[0]);
  for (const ch of sol) {
    const d = SYM[ch];
    if (d === undefined) continue;
    const ns = E.step(L, st, d);
    if (!ns) break;
    const from = st.body[0], to = ns.body[0];
    seen.add(to);
    if ((to - from) % L.w === 0) {              // 같은 세로줄 = 떨어졌다
      const step = to > from ? L.w : -L.w;
      for (let c = from; c !== to; c += step) seen.add(c);
    }
    st = ns;
  }
  return seen;
}

function addTraps(grid, want) {
  const def = { grid, gravity: true, clear: 'all', id: 't' };
  const base = E.solve(def);
  if (!base.ok) return null;
  //  🔴 **시험 풀이는 일찍 끊는다** (09-05 밤). 긴 판이 들어오면서 한 판에 3분씩 걸렸다 —
  //     후보 칸마다 판 전체를 상한 120만까지 다시 풀고 있었다.
  //     조각을 하나 얹어 상태가 원래의 세 배를 넘으면 그건 **다른 판이 된 것**이라
  //     어차피 안 받는다. 그러니 거기서 끊는 게 맞다.
  const trialCap = Math.min(1200000, base.states * 3 + 40000);
  const walked = headPath(E.parse(def), base.path);
  const G = grid.map(r => r.split(''));
  const H = G.length, W = G[0].length;

  const cand = [];
  for (let y = 1; y < H - 1; y++)
    for (let x = 1; x < W - 1; x++) {
      const c = y * W + x;
      if (G[y][x] !== '.' || walked.has(c)) continue;
      //  정답 길 **바로 옆**만. 멀면 눈에 안 들어와 함정이 아니다
      let near = false;
      for (const n of [c - 1, c + 1, c - W, c + W]) if (walked.has(n)) near = true;
      if (near) cand.push([x, y]);
    }

  //  🔴 후보를 다 훑지 않는다. 긴 판은 빈 칸이 백 개가 넘어 그것만으로 몇 분이 간다.
  //     섞어서 앞에서부터 스물다섯 개만 본다 — 세 개 얹는 데 그만하면 넉넉하다.
  for (let i = cand.length - 1; i > 0; i--) {
    const j = (i * 2654435761 + cand.length) % (i + 1);
    const tmp = cand[i]; cand[i] = cand[j]; cand[j] = tmp;
  }
  let put = 0, tried = 0;
  for (const [x, y] of cand) {
    //  🔴 열 번만 본다. 세 개 얹는 데 그만하면 넉넉하고, 스물다섯 번은 두 배 반 느리다.
    if (put >= want || tried >= 10) break;
    tried++;
    G[y][x] = '+';
    const g2 = G.map(r => r.join(''));
    const a = E.solve({ grid: g2, gravity: true, clear: 'all', id: 't' }, { cap: trialCap });
    if (a.ok && a.moves === base.moves && a.shortest === 1) put++;
    else G[y][x] = '.';
  }
  return put ? { grid: G.map(r => r.join('')), traps: put } : null;
}

const pool = JSON.parse(fs.readFileSync(IN, 'utf8'));
//  🔴 **이어서 한다.** 09-05 에 340판 중 140판에서 조용히 죽었다 (솔버 상한을
//     크게 잡아 메모리를 먹은 것으로 본다). 몇십 분치를 다시 돌리면 안 된다.
const out = (process.env.RESUME === '1' && fs.existsSync(OUT))
  ? JSON.parse(fs.readFileSync(OUT, 'utf8')) : [];
const start = out.length;
console.log(IN, '—', pool.length, '개 ·', WANT, '개씩 얹는다' +
            (start ? ' · ' + start + '개는 이미 했다' : ''));
let none = 0;
//  🔴 **긴 판에는 안 얹는다** (09-05 밤). 긴 판은 바탕 풀이부터 무거워
//     한 판에 3분씩 걸렸다 — 360판이면 열 시간이다.
//     그리고 긴 판은 **이미 길이로 어렵다.** 덤 조각이 제일 값어치 있는 자리는
//     짧은 판이다 (거기엔 길이 말고 어려울 거리가 없다).
const LONG = +(process.env.LONG || 30);
let skipped = 0;
for (let i = start; i < pool.length; i++) {
  const p = pool[i];
  if (p.best > LONG) {
    skipped++;
    out.push(Object.assign({}, p, { traps: 0 }));
    if ((i + 1) % 20 === 0) {
      fs.writeFileSync(OUT, JSON.stringify(out, null, 1) + '\n');
      process.stderr.write('  ' + (i + 1) + '/' + pool.length + ' (저장함)\n');
    }
    continue;
  }
  const r = addTraps(p.grid, WANT);
  if (!r) { none++; out.push(Object.assign({}, p, { traps: 0 })); continue; }
  //  🔴 정답 수순은 그대로다 (최단해가 안 바뀌는 것만 받았다). 그래도 다시 풀어 못박는다.
  const a = E.solve({ grid: r.grid, gravity: true, clear: 'all', id: 'g' });
  out.push(Object.assign({}, p, { grid: r.grid, traps: r.traps, best: a.moves, sol: a.path }));
  //  🔴 **중간에 저장한다.** 맨 끝에서 한 번만 쓰면 도중에 죽을 때
  //     몇십 분치가 통째로 날아간다 (09-04 에 hardpool.js 로 그럴 뻔했다).
  if ((i + 1) % 20 === 0) {
    fs.writeFileSync(OUT, JSON.stringify(out, null, 1) + '\n');
    process.stderr.write('  ' + (i + 1) + '/' + pool.length + ' (저장함)\n');
  }
}
fs.writeFileSync(OUT, JSON.stringify(out, null, 1) + '\n');
const avg = out.reduce((s, p) => s + p.traps, 0) / out.length;
console.log('덤 조각 평균 ' + avg.toFixed(1) + '개 · 하나도 못 얹은 판 ' + none + ' → ' + OUT);
