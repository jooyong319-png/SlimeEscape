// 🔴 사람이 느끼는 난이도를 재본다 — 솔버 지표가 틀렸다는 게 드러났으므로.
//
//   node tools/human.js                  levels.json 전부
//   node tools/human.js cands.json       생성기가 뽑아둔 후보들
//   (모듈로도 쓴다: const { orderFreedom } = require('./human.js'))
//
// 왜 새로 만드나 (2026-08-28):
//   "이미 진 상태 40%"짜리 판 다섯을 사장님이 **평균 5초**에, 되돌리기 0회로,
//   세 판은 최단 걸음 그대로 푸셨다. — "딱 보면 풀리는 난이도였어."
//   그 지표는 **잘못 갈 여지**를 재지 **잘못 갈 이유**를 못 잰다.
//   함정은 많은데 미끼가 없으면 아무도 안 밟는다.
//
// 새 지표: 🔴 **통하는 순서의 비율.**
//   사람은 최적해를 안 찾는다. 조각 먹을 순서를 대충 정하고 밀어붙인다.
//   그래서 "아무 순서로나 되는 판"은 딱 보면 풀린다.
//     100%  = 순서가 아무 상관 없다 → 5초짜리
//      20%  = 다섯 중 하나만 통한다 → 생각해야 한다
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;

const CAP = 200000;

/// 지금 상태에서 조건을 만족하는 상태까지 간다. 못 가면 null
function bfsTo(L, from, ok) {
  const seen = new Set([E.keyOf(from)]);
  let frontier = [from];
  for (let depth = 0; depth < 400 && frontier.length; depth++) {
    const next = [];
    for (const st of frontier) {
      for (let d = 0; d < 4; d++) {
        const ns = E.step(L, st, d);
        if (!ns) continue;
        const k = E.keyOf(ns);
        if (seen.has(k)) continue;
        if (ok(ns)) return ns;
        seen.add(k);
        if (seen.size > CAP) return null;
        next.push(ns);
      }
    }
    frontier = next;
  }
  return null;
}

/// 이 순서대로 조각을 먹고 목표를 채울 수 있나
function orderWorks(L, order) {
  let st = E.startState(L);
  if (E.isWin(L, st)) return true;
  for (const f of order) {
    const i = L.foodIdx.get(f);
    if (st.fm & (1 << i)) continue;             // 오는 길에 이미 먹었다
    st = bfsTo(L, st, s => s.body[0] === f);
    if (!st) return false;
  }
  return !!bfsTo(L, st, s => E.isWin(L, s));
}

// 조각 7개면 순서가 5,040가지다 — 다 못 돌린다. 많으면 무작위로 골라 본다.
// 씨앗을 고정해서 같은 판은 늘 같은 결과가 나오게 한다.
let seed = 20260828;
const rnd = () => {
  seed ^= seed << 13; seed |= 0;
  seed ^= seed >>> 17;
  seed ^= seed << 5;  seed |= 0;
  return ((seed >>> 0) % 1000000) / 1000000;
};

function factorial(n) { let f = 1; for (let i = 2; i <= n; i++) f *= i; return f; }

function orders(cells, cap) {
  if (factorial(cells.length) <= cap) {
    const out = [];
    const walk = (cur, rest) => {
      if (!rest.length) { out.push(cur.slice()); return; }
      for (let i = 0; i < rest.length; i++)
        walk(cur.concat(rest[i]), rest.slice(0, i).concat(rest.slice(i + 1)));
    };
    walk([], cells);
    return { list: out, sampled: false };
  }
  const out = [];
  for (let k = 0; k < cap; k++) {
    const a = cells.slice();
    for (let i = a.length - 1; i > 0; i--) {
      const j = Math.floor(rnd() * (i + 1));
      const tmp = a[i]; a[i] = a[j]; a[j] = tmp;
    }
    out.push(a);
  }
  return { list: out, sampled: true };
}

/// 🔴 이 판이 "아무 순서로나 되는" 정도. 낮을수록 생각해야 한다.
function orderFreedom(L, cap = 60) {
  const { list, sampled } = orders(L.foods, cap);
  let ok = 0;
  for (const o of list) if (orderWorks(L, o)) ok++;
  return { ratio: list.length ? ok / list.length : 1, tried: list.length, ok, sampled };
}

function verdict(r) {
  if (r > 0.8) return '🔴 아무 순서로나 된다 — 딱 보면 풀린다';
  if (r > 0.5) return '⚠️  절반 넘게 통한다';
  if (r > 0.2) return '🟢 순서를 골라야 한다';
  return '🟢 순서가 거의 하나다';
}

module.exports = { orderFreedom, orderWorks, verdict };

if (require.main === module) {
  const arg = process.argv[2];
  const FILE = arg ? path.resolve(arg)
    : path.join(__dirname, '..', 'game', 'Assets', 'Resources', 'levels.json');
  const doc = JSON.parse(fs.readFileSync(FILE, 'utf8'));
  const items = doc.levels || doc;
  const grav = doc.gravity !== false;

  console.log('판         조각  순서시도  통함   비율  판정');
  items.forEach((j, i) => {
    const L = E.parse({ grid: j.grid, gravity: grav });
    const f = orderFreedom(L);
    console.log(
      String(j.id || ('#' + (i + 1))).padEnd(10) +
      String(L.foods.length).padStart(4) +
      String(f.tried).padStart(9) + (f.sampled ? '*' : ' ') +
      String(f.ok).padStart(6) +
      (f.ratio * 100).toFixed(0).padStart(6) + '%  ' + verdict(f.ratio)
    );
  });
}
