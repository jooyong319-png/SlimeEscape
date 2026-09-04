// 🔴 판 자료를 **손봐서** 다시 쓴다 (2026-09-05 밤, 사장님 지적 넷 중 셋).
//
//   ① 세로로 긴 판(높이 13 초과)을 뺀다 — 카메라 배율이 그 판들 때문에 내려간다
//      ("27,28,31,41은 갑자기 카메라가 작아진다 — 맵이 작아보여")
//   ② 옛 1-14 · 1-15 · 1-16 을 되살려 넣는다 — 46~47걸음 · 홈 둘 · 제일 어려운 자리
//      ("그 예전에 만들었던 14,15,16 다시 가져와서 넣어놔 홈2개짜리 제일 어려운곳에")
//   ③ **걸음 순으로 다시 세운다** — 34번부터 짧은 판이 섞여 들어갔다
//      ("34 부터 는 왜 갑자기 다시 엄청쉬운 게 나온던데..?")
//
//   node tools/patch-levels.js            보여만 준다
//   WRITE=1 node tools/patch-levels.js    levels.json 에 쓴다
'use strict';
const fs = require('fs');
const path = require('path');
const E = require('./engine.js').SlimeEngine;
const { cutOf } = require('./star.js');

const LP = path.join(__dirname, '../game/Assets/Resources/levels.json');
const OLD = process.env.OLD || path.join(process.env.TEMP || '/tmp', 'old16.json');
const MAXH = +(process.env.MAXH || 13);

const d = JSON.parse(fs.readFileSync(LP, 'utf8'));
const tut = d.levels.find(l => l.tutorial);
let keep = d.levels.filter(l => !l.tutorial && !l.demo);
const before = keep.length;

// ① 세로로 긴 판을 뺀다
const tall = keep.filter(l => l.grid.length > MAXH);
keep = keep.filter(l => l.grid.length <= MAXH);
console.log('세로 ' + MAXH + '칸 넘어 뺀 판 ' + tall.length + '개: ' +
            tall.map(l => l.id + '(' + l.grid[0].length + 'x' + l.grid.length + ')').join(', '));

// ② 옛 판 셋을 되살린다
const old = JSON.parse(fs.readFileSync(OLD, 'utf8'));
const revived = [];
for (const id of ['1-14', '1-15', '1-16']) {
  const l = old.levels.find(x => x.id === id);
  if (!l) { console.log('🔴 옛 ' + id + ' 을 못 찾았다'); continue; }
  const a = E.solve({ grid: l.grid, gravity: true, clear: 'all', id }, { cap: 2000000 });
  const s = E.solve({ grid: l.grid, gravity: true, clear: 'all', id }, { needStar: true, cap: 2000000 });
  if (!a.ok || !s.ok) { console.log('🔴 옛 ' + id + ' 이 지금 엔진으로 안 풀린다'); continue; }
  revived.push({
    grid: l.grid, clear: 'all',
    best: a.moves, sol: a.path,
    bestStar: s.moves,
    //  🔴 커트는 **지금 식**으로 다시 박는다. 옛 커트(+40%)는 지금 것(+10%)보다 헐렁하다 —
    //     섞어 두면 어떤 판만 별 셋이 쉬워진다.
    cut: cutOf(s.moves),
    lost: 0, tight: 0, backtrack: 0, states: a.states,
    press: -1, traps: 0, gim: 'none', revived: true,
  });
  console.log('되살림 ' + id + ' · ' + a.moves + '걸음 · 별먹고 ' + s.moves + ' · 커트 ' + cutOf(s.moves));
}
keep = keep.concat(revived);

// ③ 걸음 순으로 다시 세운다 (같으면 압박이 높은 쪽을 뒤로)
keep.sort((a, b) => (a.best - b.best) || ((a.press || 0) - (b.press || 0)));

// 🔴 기믹 첫 등장은 여전히 뒤로 밀어둔다 — 걸음만으로 세우면 앞에 올 수 있다
{
  const SLOT = [['box', 5], ['zone', 9], ['portal', 16]];
  //  ⚠️ 한 번만 돌리면 **뒤 기믹을 밀 때 앞 기믹이 도로 앞으로 밀린다** (09-05 에 그랬다).
  //     두 번 돌려 자리를 굳힌다.
  for (let pass = 0; pass < 2; pass++)
    for (const [g, slot] of SLOT) {
      const early = [];
      for (let i = keep.length - 1; i >= 0; i--)
        if (i < slot && (keep[i].gim || 'none') === g) early.unshift(keep.splice(i, 1)[0]);
      //  🔴 한 자리에 몰아넣지 않는다 — 무중력 판 넷이 10~13번에 줄줄이 왔다.
      //     네 판씩 띄워 꽂는다. 같은 기믹이 연달아 나오면 배우는 재미가 없다.
      early.forEach((lv, k) => keep.splice(Math.min(slot + k * 4, keep.length), 0, lv));
    }
}

keep.forEach((l, i) => { l.id = '1-' + (i + 1); });
console.log('\n판 ' + before + ' → ' + keep.length);
console.log('걸음: ' + keep.map(l => l.best).join(' '));
const sz = keep.map(l => l.grid[0].length + 'x' + l.grid.length);
console.log('제일 큰 판: 가로 ' + Math.max(...keep.map(l => l.grid[0].length)) +
            ' · 세로 ' + Math.max(...keep.map(l => l.grid.length)));
for (const k of ['box', 'zone', 'portal']) {
  const at = []; keep.forEach((l, i) => { if (l.gim === k) at.push(i + 1); });
  console.log('  ' + k.padEnd(7) + at.join(', '));
}
console.log('  되살린 판: ' + keep.map((l, i) => l.revived ? i + 1 : 0).filter(Boolean).join(', '));

if (process.env.WRITE !== '1') { console.log('\n(보여주기만 했다. 쓰려면 WRITE=1)'); process.exit(0); }
fs.writeFileSync(path.join(__dirname, 'levels.bak.json'), fs.readFileSync(LP));
d.levels = [tut, ...keep];
fs.writeFileSync(LP, JSON.stringify(d, null, 2) + '\n');
console.log('\n판 자료에 썼다 · 판 ' + keep.length + '개 (+튜토리얼)');
