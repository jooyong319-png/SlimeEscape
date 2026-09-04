// 🔴 **뻔한 사람이 이 판을 푸는가**를 잰다 (2026-09-05 밤).
//
// 사장님: 1-48 은 *"뭐부터 할지 바로 보였다"*, 옛 14/15/16 은 *"사람으로썬 상상하지 못한 패턴"*.
//
// 여태 내가 잰 자는 전부 **판이 얼마나 복잡한가**였다. 진짜는 **뻔한 수로 풀리는가**다.
// 그래서 *뻔한 플레이어*를 코드로 만들어 돌려본다:
//   · 몸이 짧으면 → 제일 가까운 조각으로
//   · 몸이 맞으면 → 지금 채울 홈의 심으로
//   · 그 방향으로 제일 가까워지는 수를 둔다
//
// 재는 것 둘:
//   🔴 **풀어버리나** — 풀면 그 판은 쉽다. 버린다.
//   🔴 **속는 길이** — 못 풀면, **몇 걸음 가서** 이길 수 없게 되나.
//       5걸음이면 바로 알아챈다. 30걸음이면 *"한참 가고 나서 아 망했네"* 다.
//       사장님이 말한 "상상하지 못한 패턴"은 이쪽일 것이다.
//
//   node tools/greedy.js                       판 자료를 잰다
//   node tools/greedy.js tools/trap-pool.json  못을 잰다
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const src = fs.readFileSync(path.join(__dirname, 'hard.js'), 'utf8');
const mod = { exports: {} };
new Function('module', 'exports', 'require', '__dirname',
             src.slice(0, src.indexOf('const fs = require')) +
             '\nmodule.exports = { measure, winnable };')(mod, mod.exports, require, __dirname);
const { winnable } = mod.exports;

const CAP = +(process.env.CAP4 || 900000);

/// 두 칸 사이 걸어야 하는 대충 거리
const man = (L, a, b) =>
  Math.abs((a % L.w) - (b % L.w)) + Math.abs(((a / L.w) | 0) - ((b / L.w) | 0));

/// 🔴 **뻔한 플레이어**. 사람이 처음 보고 두는 수를 흉내 낸다.
///    ⚠️ 완벽한 사람 흉내는 아니다. 다만 *"제일 먼저 떠오르는 수"* 는 이것에 가깝다.
function greedy(L, safe, idx, maxSteps) {
  let st = E.startState(L);
  const seen = new Set([E.keyOf(st)]);
  let fooled = -1;                 // 몇 걸음째에 이길 수 없게 됐나
  const acts = L.pads.length ? 5 : 4;

  for (let step = 0; step < maxSteps; step++) {
    if (E.isWin(L, st)) return { solved: true, steps: step, fooled };

    //  지금 노리는 것 — 아직 안 채운 첫 홈
    let door = null;
    for (let i = 0; i < L.doors.length; i++)
      if (!((st.dm || 0) & (1 << i))) { door = L.doors[i]; break; }
    if (!door) return { solved: false, steps: step, fooled, why: '홈이 다 찼는데 안 이겼다' };

    let target;
    if (st.body.length < door.cells.length) {
      //  몸이 짧다 → 제일 가까운 안 먹은 조각
      let bestD = Infinity;
      for (let i = 0; i < L.foods.length; i++) {
        if (st.fm & (1 << i)) continue;
        const d = man(L, st.body[0], L.foods[i]);
        if (d < bestD) { bestD = d; target = L.foods[i]; }
      }
      if (target === undefined) target = door.core >= 0 ? door.core : door.cells[0];
    } else {
      target = door.core >= 0 ? door.core : door.cells[0];
    }

    //  그 쪽으로 제일 가까워지는 수
    let pick = null, pickD = Infinity;
    for (let d = 0; d < acts; d++) {
      const ns = E.step(L, st, d);
      if (!ns) continue;
      const k = E.keyOf(ns);
      if (seen.has(k)) continue;               // 왔던 자리로는 안 간다 (사람도 안 그런다)
      const dd = man(L, ns.body[0], target);
      if (dd < pickD) { pickD = dd; pick = { ns, k }; }
    }
    if (!pick) return { solved: false, steps: step, fooled, why: '더 갈 데가 없다' };

    st = pick.ns;
    seen.add(pick.k);
    if (fooled < 0) {
      const j = idx.get(pick.k);
      if (j === undefined || !safe.has(j)) fooled = step + 1;   // 여기서 이길 수 없게 됐다
    }
  }
  return { solved: false, steps: maxSteps, fooled, why: '너무 오래 돈다' };
}

function judge(name, l) {
  const def = { grid: l.grid, gravity: true, clear: l.clear || 'all', id: name };
  let L; try { L = E.parse(def); } catch (e) { return console.log(name + ' 못 읽음'); }
  const W = winnable(L, CAP);
  if (!W) return console.log(name.padEnd(11) + '너무 커서 못 잼');
  const a = E.solve(def, { cap: CAP });
  const g = greedy(L, W.safe, W.idx, Math.max(60, (a.ok ? a.moves : 40) * 4));
  const tag = g.solved ? '🔴 뻔한 사람이 풀어버림'
            : g.fooled < 0 ? '🟡 안 풀리는데 지지도 않음(뱅뱅 돔)'
            : g.fooled <= 6 ? '🟡 ' + g.fooled + '걸음 만에 망함 (바로 알아챈다)'
            : '🟢 ' + g.fooled + '걸음 가서 망함 (한참 속는다)';
  console.log(name.padEnd(11) + String(a.ok ? a.moves : '?').padStart(3) + '걸음  ' + tag);
  return { solved: g.solved, fooled: g.fooled, moves: a.ok ? a.moves : 0 };
}

const file = process.argv[2];
if (!file) {
  const old = JSON.parse(fs.readFileSync(path.join(process.env.TEMP || '/tmp', 'old16.json'), 'utf8'));
  const d = JSON.parse(fs.readFileSync(path.join(__dirname, '../game/Assets/Resources/levels.json'), 'utf8'));
  console.log('=== 사장님이 어렵다고 하신 옛 판 ===');
  for (const id of ['1-14', '1-15', '1-16']) judge('옛 ' + id, old.levels.find(x => x.id === id));
  console.log('');
  console.log('=== 사장님이 쉽다고 하신 판 ===');
  judge('1-48', d.levels.find(x => x.id === '1-48'));
  console.log('');
  console.log('=== 지금 판 뒤쪽 ===');
  for (const id of ['1-40', '1-42', '1-43', '1-44', '1-45', '1-46', '1-47'])
    judge(id, d.levels.find(x => x.id === id));
  process.exit(0);
}
const raw = JSON.parse(fs.readFileSync(file, 'utf8'));
const list = (raw.levels || raw).filter(l => l.grid && !l.tutorial && !l.demo);
let solved = 0, quick = 0, deep = 0, skip = 0;
list.forEach((l, i) => {
  const r = judge(String(l.id || '#' + i), l);
  if (!r) { skip++; return; }
  if (r.solved) solved++; else if (r.fooled < 0 || r.fooled <= 8) quick++; else deep++;
});
console.log('\n🔴 뾔한 사람이 풀어버린 판 ' + solved +
            ' · 🟢 뾔한 수가 8걸음 이하만 통함(어렵다) ' + quick +
            ' · 🔴 오래 통함(쉽다) ' + deep + ' · 못 잰 ' + skip);
