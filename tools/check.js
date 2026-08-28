// 판을 솔버에 넣기 전 값싼 검사 — 손으로 세다 틀리는 걸 막는다.
module.exports = function check(grid) {
  const e = [];
  const w = grid[0].length;
  grid.forEach((r, y) => { if (r.length !== w) e.push(y + '행 폭 ' + r.length + ' (기준 ' + w + ')'); });
  const flat = grid.join('');
  const n = c => flat.split(c).length - 1;
  const S = n('S'), F = n('+'), T = n('=') + n('*'), C = n('*');
  if (S !== 1) e.push('S가 ' + S + '개');
  if (C > 1) e.push('심(*)이 ' + C + '개');
  if (F + 1 !== T)
    e.push('길이 안 맞음: 조각 ' + F + ' -> 길이 ' + (F + 1) + ', 목표 ' + T + '칸  (목표를 ' + (F + 1) + '칸으로)');
  return { ok: e.length === 0, errors: e, foods: F, target: T, zone: n('~') };
};
