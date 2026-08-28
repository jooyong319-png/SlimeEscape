// 🔴 판의 난이도 지표 — **여기 한 벌만 있다.**
//    stamp.js · gen.js · design.js · lose.js 가 전부 이걸 부른다.
//    (규칙 엔진이 두 벌이라 어긋났던 적이 있다. 지표까지 네 벌로 흩어놓지 않는다.)
//
// 재는 것
//   lost     이미 진 상태 비율 — 갈 수 있는 상태 중 이기는 길이 남지 않은 것
//              낮으면 실수해도 저절로 회복된다 = 퍼즐이 아니다
//   earliest 제일 이른 패착 — 몇 걸음 만에 질 수 있나
//   wander   🔴 진 뒤에도 더 돌아다닐 수 있는 걸음 수
//              게임이 "졌다"고 안 알려주기로 했으므로(2026-08-28 결정),
//              이 숫자가 곧 **플레이어가 헛되이 쓰는 시간**이다. 판 채택 기준이다.
const E = require('./engine.js').SlimeEngine;

/// def = { grid, gravity }.  상태가 cap을 넘으면 null (재는 데 너무 걸린다)
function analyze(def, cap = 400000) {
  const L = E.parse(def);
  const idOf = new Map(), states = [], depth = [];
  const id = (st, d) => {
    const k = E.keyOf(st); let v = idOf.get(k);
    if (v === undefined) { v = states.length; idOf.set(k, v); states.push(st); depth.push(d); }
    return v;
  };
  id(E.startState(L), 0);

  const edges = [], win = [];
  for (let i = 0; i < states.length; i++) {
    if (states.length > cap) return null;
    const st = states[i], out = [];
    if (E.isWin(L, st)) { win.push(i); edges.push(out); continue; }
    for (let d = 0; d < 4; d++) { const ns = E.step(L, st, d); if (ns) out.push(id(ns, depth[i] + 1)); }
    edges.push(out);
  }
  if (!win.length) return null;

  const rev = states.map(() => []);
  edges.forEach((o, i) => o.forEach(k => rev[k].push(i)));

  // 이길 수 있는 상태 = 승리에서 거꾸로 퍼뜨린다
  const canWin = new Uint8Array(states.length);
  let q = [...win]; win.forEach(i => canWin[i] = 1);
  for (let h = 0; h < q.length; h++) for (const p of rev[q[h]]) if (!canWin[p]) { canWin[p] = 1; q.push(p); }

  const lostIdx = [];
  for (let i = 0; i < states.length; i++) if (!canWin[i]) lostIdx.push(i);

  // 제일 이른 패착 = 아직 이길 수 있는 상태에서 한 걸음에 넘어가 버리는 곳
  let earliest = Infinity;
  for (let i = 0; i < states.length; i++) {
    if (!canWin[i]) continue;
    for (const k of edges[i]) if (!canWin[k] && depth[k] < earliest) earliest = depth[k];
  }

  // 진 뒤 얼마나 더 돌아다닐 수 있나 — 못 이기는 영역 안에서만 잰 제일 먼 거리
  let wander = 0;
  if (lostIdx.length) {
    const seed = lostIdx.filter(i => rev[i].some(p => canWin[p]));
    const dist = new Int32Array(states.length).fill(-1);
    q = [];
    (seed.length ? seed : [lostIdx[0]]).forEach(i => { dist[i] = 0; q.push(i); });
    for (let h = 0; h < q.length; h++)
      for (const k of edges[q[h]]) if (!canWin[k] && dist[k] < 0) { dist[k] = dist[q[h]] + 1; q.push(k); }
    for (const i of lostIdx) if (dist[i] > wander) wander = dist[i];
  }

  return {
    states: states.length,
    lost: lostIdx.length / states.length,
    earliest: earliest === Infinity ? null : earliest,
    wander,
  };
}

module.exports = { analyze };
