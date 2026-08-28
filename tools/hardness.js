// 🔬 "이미 진 상태" 비율 — 이게 진짜 난이도 지표다.
//
// 막다른 상태(더 못 움직임)는 이 게임에서 거의 안 생긴다(빈 방이라 어디로든 갈 수 있으니).
// 진짜 실패는 **"움직일 순 있는데 이미 틀렸다"**이고, 그건 승리 도달 가능성을 거꾸로 계산해야 안다.
// (설계 연구 4회차에서 예고했던 것)
const E3 = require('./engine.js').SlimeEngine;

function analyze(def) {
  const L = E3.parse(def);
  const s0 = E3.startState(L);
  const idOf = new Map(), states = [];
  const id = st => { const k = E3.keyOf(st); let v = idOf.get(k);
    if (v === undefined) { v = states.length; idOf.set(k, v); states.push(st); } return v; };

  // 1) 앞으로 훑어 도달 가능한 상태와 간선을 모은다
  id(s0);
  const edges = [];           // edges[i] = [다음 상태 id들]
  const win = [];
  for (let i = 0; i < states.length; i++) {
    const st = states[i];
    const out = [];
    if (E3.isWin(L, st)) { win.push(i); edges.push(out); continue; }   // 이기면 거기서 끝
    for (let d = 0; d < 4; d++) {
      const ns = E3.step(L, st, d);
      if (ns) out.push(id(ns));
    }
    edges.push(out);
  }

  // 2) 거꾸로 훑어 "아직 이길 수 있는" 상태를 표시한다
  const rev = states.map(() => []);
  edges.forEach((outs, i) => outs.forEach(j => rev[j].push(i)));
  const canWin = new Uint8Array(states.length);
  const q = [...win];
  win.forEach(i => canWin[i] = 1);
  for (let h = 0; h < q.length; h++)
    for (const p of rev[q[h]]) if (!canWin[p]) { canWin[p] = 1; q.push(p); }

  let alive = 0, dead = 0;
  for (let i = 0; i < states.length; i++) (canWin[i] ? alive++ : dead++);
  return { total: states.length, alive, dead, lostRatio: dead / states.length, winStates: win.length };
}

const W = 20, H = 12;
function blank(){const g=[];for(let y=0;y<H;y++)g.push(Array.from({length:W},(_,x)=>(x===0||y===0||x===W-1||y===H-1)?'#':'.'));return g;}
const box=(g,x0,y0,x1,y1,c)=>{for(let y=y0;y<=y1;y++)for(let x=x0;x<=x1;x++)g[y][x]=c;};
const S=g=>g.map(r=>r.join(''));

// A안
const g = blank();
box(g, 15, 8, 17, 9, '='); g[8][17] = '*';
g[9][3] = 'S';
[[5,3],[8,6],[11,3],[13,8],[6,9]].forEach(([x,y]) => g[y][x] = '+');
const grid = S(g);

const r = E3.solve({ grid });
const a = analyze({ grid });
console.log('A안 · 목표 6칸 · 조각 5개');
console.log(`  최단 ${r.moves}걸음 · 최단해 ${r.shortest}개`);
console.log(`  도달 상태 ${a.total.toLocaleString()}개`);
console.log(`  아직 이길 수 있는 상태  ${a.alive.toLocaleString()} (${(100-a.lostRatio*100).toFixed(1)}%)`);
console.log(`  🔴 이미 진 상태          ${a.dead.toLocaleString()} (${(a.lostRatio*100).toFixed(1)}%)`);
